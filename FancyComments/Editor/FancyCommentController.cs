using System.IO;
using System.Text.RegularExpressions;
using FancyComments.Model;
using FancyComments.UI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json;

namespace FancyComments.Editor
{
    /// <summary>
    /// Per-view actions shared by the typing trigger and the preview buttons: create, view and edit.
    /// </summary>
    internal sealed class FancyCommentController
    {
        private static readonly object CreatingKey = new();

        private readonly IWpfTextView view;
        private readonly ITextDocumentFactoryService documents;
        private CommentStore? store;

        private FancyCommentController(IWpfTextView view, ITextDocumentFactoryService documents)
        {
            this.view = view;
            this.documents = documents;
        }

        public IWpfTextView View => this.view;

        public string? FilePath => this.documents.TryGetTextDocument(this.view.TextBuffer, out ITextDocument document) ? document.FilePath : null;

        /// <summary>The store for this document; null if it isn't in a solution folder or git repository. Looked up once, then cached.</summary>
        private CommentStore? Store => this.store ??= this.FilePath is { } path ? CommentStore.ForDocument(path) : null;

        public static FancyCommentController For(IWpfTextView view, ITextDocumentFactoryService documents) =>
            view.Properties.GetOrCreateSingletonProperty(() => new FancyCommentController(view, documents));

        /// <summary>Opens the editor for a new comment and, on save, replaces the typed trigger (e.g. "/*##") with the marker.</summary>
        public void Create(SnapshotSpan trigger, CommentSyntax syntax)
        {
            if (this.GetStore() is not { } store)
            {
                return;
            }

            ITrackingSpan tracking = trigger.Snapshot.CreateTrackingSpan(trigger, SpanTrackingMode.EdgeInclusive);

            // The trigger may already be gone, e.g. replaced by an earlier dialog or undone before we got here.
            if (tracking.GetText(this.view.TextBuffer.CurrentSnapshot) != syntax.Trigger)
            {
                return;
            }
            FancyComment comment = new FancyComment
            {
                Id = CommentMarker.NewId(),
                File = store.RelativePath(this.FilePath!),
                Created = DateTime.UtcNow,
            };

            // The modal pumps messages, so a queued second request could otherwise open a dialog on top of this one.
            PropertyCollection properties = this.view.TextBuffer.Properties;
            if (properties.ContainsProperty(CreatingKey))
            {
                return;
            }

            properties.AddProperty(CreatingKey, true);
            try
            {
                if (!CommentEditorDialog.Edit(store, comment))
                {
                    return;
                }
            }
            finally
            {
                properties.RemoveProperty(CreatingKey);
            }

            SnapshotSpan span = tracking.GetSpan(this.view.TextBuffer.CurrentSnapshot);
            comment.Line = span.Start.GetContainingLine().LineNumber + 1;
            store.Save(comment);
            this.view.TextBuffer.Replace(span, syntax.Format(CommentMarker.Tag(comment.Id), comment.Summary));
        }

        public void ShowComment(string id)
        {
            if (this.GetStore() is not { } store)
            {
                return;
            }

            if (store.Load(id) is not { } comment)
            {
                Prompt.Show($"FancyComments/Comments/{id}.json wasn't found. It may not have been committed yet.");
                return;
            }

            new CommentViewerWindow(store, comment).Show();
        }

        /// <summary>The comment and its store, for the hover preview. Null if it can't be read; never prompts.</summary>
        public (CommentStore Store, FancyComment Comment)? TryLoad(string id)
        {
            if (this.Store is not { } store)
            {
                return null;
            }

            try
            {
                return store.Load(id) is { } comment ? (store, comment) : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        /// <param name="id">The comment to edit.</param>
        /// <param name="location">The marker that was clicked, so the right one is updated if an id appears twice.</param>
        public void Edit(string id, ITrackingSpan? location)
        {
            if (this.GetStore() is not { } store)
            {
                return;
            }

            (ITextSnapshotLine Line, Match Match)? found = this.Find(id, location);

            // If the JSON is missing (e.g. not committed), start again from the summary in the source.
            FancyComment comment = store.Load(id) ?? new FancyComment
            {
                Id = id,
                Description = found?.Match.Groups["summary"].Value ?? "",
                Created = DateTime.UtcNow,
            };

            if (!CommentEditorDialog.Edit(store, comment))
            {
                return;
            }

            comment.File = store.RelativePath(this.FilePath!);
            if (this.Find(id, location) is { } current)
            {
                comment.Line = current.Line.LineNumber + 1;
                Group summary = current.Match.Groups["summary"];
                this.view.TextBuffer.Replace(new Span(current.Line.Start + summary.Index, summary.Length), comment.Summary);
            }

            store.Save(comment);
        }

        /// <summary>Removes the comment from the code, deletes its JSON, and deletes its media unless another comment uses it.</summary>
        /// <param name="id">The comment to delete.</param>
        /// <param name="location">The marker that was clicked, so the right one is removed if an id appears twice.</param>
        public void Delete(string id, ITrackingSpan? location)
        {
            if (this.GetStore() is not { } store)
            {
                return;
            }

            if (!Prompt.Confirm("Delete this Fancy Comment?\n\nThis removes it from the code and deletes its JSON file, along with any media no other comment uses. The files can't be restored with Undo."))
            {
                return;
            }

            if (this.Find(id, location) is { } found)
            {
                this.view.TextBuffer.Delete(this.WholeComment(found.Line, found.Match));
            }

            store.Delete(id);
        }

        /// <summary>
        /// The marker plus the language's comment delimiters around it (e.g. "/*## … ##*/"),
        /// or the whole line if the comment was the only thing on it.
        /// </summary>
        private Span WholeComment(ITextSnapshotLine line, Match match)
        {
            string text = line.GetText();
            int start = match.Index;
            int end = match.Index + match.Length;
            foreach (CommentSyntax syntax in CommentLanguages.For(this.FilePath))
            {
                string open = syntax.Open + syntax.Separator;
                if (start >= open.Length
                    && string.CompareOrdinal(text, start - open.Length, open, 0, open.Length) == 0
                    && string.CompareOrdinal(text, end, syntax.Close, 0, syntax.Close.Length) == 0)
                {
                    start -= open.Length;
                    end += syntax.Close.Length;
                    break;
                }
            }

            if (text.Substring(end).Trim().Length == 0)
            {
                if (text.Substring(0, start).Trim().Length == 0)
                {
                    return line.ExtentIncludingLineBreak.Span;
                }

                // A trailing comment: don't leave the space before it behind.
                start = text.Substring(0, start).TrimEnd().Length;
            }

            return new Span(line.Start + start, end - start);
        }

        /// <summary>The marker with this id: the one on the same line as <paramref name="location"/> if given, otherwise the first.</summary>
        private (ITextSnapshotLine Line, Match Match)? Find(string id, ITrackingSpan? location = null)
        {
            ITextSnapshot snapshot = this.view.TextBuffer.CurrentSnapshot;
            if (location is not null)
            {
                ITextSnapshotLine line = location.GetStartPoint(snapshot).GetContainingLine();
                foreach (Match match in CommentMarker.Pattern.Matches(line.GetText()))
                {
                    if (match.Groups["id"].Value == id)
                    {
                        return (line, match);
                    }
                }
            }

            foreach (ITextSnapshotLine line in snapshot.Lines)
            {
                string text = line.GetText();
                if (!text.Contains("[fc:" + id))
                {
                    continue;
                }

                foreach (Match match in CommentMarker.Pattern.Matches(text))
                {
                    if (match.Groups["id"].Value == id)
                    {
                        return (line, match);
                    }
                }
            }

            return null;
        }

        /// <summary>True if the comment has a marker in the code but its JSON is gone.</summary>
        public bool IsMissing(string id) => this.Store is { } store && !store.Exists(id);

        private CommentStore? GetStore()
        {
            CommentStore? store = this.Store;
            if (store is null)
            {
                Prompt.Show("Fancy Comments only works on files inside a solution folder or git repository.");
            }

            return store;
        }
    }
}
