using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using FancyComments.Model;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace FancyComments.Editor
{
    /// <summary>
    /// Keeps comment ids unique. Two markers with one id share one JSON file, so editing one would rewrite the other's
    /// text and deleting one would delete the other's data. When a marker is copied, the new one gets its own id and
    /// its own copy of the JSON (media stays shared). Cut and paste is a move and keeps the id.
    /// </summary>
    internal sealed class DuplicateGuard
    {
        // Ids in the text most recently copied (not cut) from any editor. Pasting one of these is a copy.
        private static HashSet<string> copiedIds = [];

        private readonly ITextBuffer buffer;
        private readonly ITextDocumentFactoryService documents;
        private readonly ITextUndoHistoryRegistry undoHistories;

        private DuplicateGuard(ITextBuffer buffer, ITextDocumentFactoryService documents, ITextUndoHistoryRegistry undoHistories)
        {
            this.buffer = buffer;
            this.documents = documents;
            this.undoHistories = undoHistories;
            buffer.Changed += this.OnChanged;
        }

        /// <summary>One guard per buffer, however many views show it.</summary>
        public static DuplicateGuard For(ITextBuffer buffer, ITextDocumentFactoryService documents, ITextUndoHistoryRegistry undoHistories) =>
            buffer.Properties.GetOrCreateSingletonProperty(() => new DuplicateGuard(buffer, documents, undoHistories));

        public static void OnCopied(string text) => copiedIds = [.. Markers(text).Select(m => m.Groups["id"].Value)];

        public static void OnCut() => copiedIds = [];

        /// <summary>Called after a paste with the markers it inserted. Any that came from a copy get their own id.</summary>
        public void OnPasted(IEnumerable<(ITrackingSpan Span, string Id)> pasted)
        {
            foreach ((ITrackingSpan span, string id) in pasted.Where(p => copiedIds.Contains(p.Id)))
            {
                this.Separate(span, id, onlyIfDuplicated: false);
            }
        }

        public static IEnumerable<Match> Markers(string text) =>
            text.Contains("[fc:") ? CommentMarker.Pattern.Matches(text).Cast<Match>() : [];

        /// <summary>
        /// Catches copies made any other way (duplicate line, Ctrl+drag): a marker inserted into a buffer that already
        /// has that id. Undo and redo are left alone, so they can always restore exactly what was there.
        /// </summary>
        private void OnChanged(object sender, TextContentChangedEventArgs e)
        {
            if (this.undoHistories.TryGetHistory(this.buffer, out ITextUndoHistory history) && history.State != TextUndoHistoryState.Idle)
            {
                return;
            }

            List<(ITrackingSpan Span, string Id)> inserted = (from change in e.Changes
                                                              from match in Markers(change.NewText)
                                                              select (Span: e.After.CreateTrackingSpan(change.NewPosition + match.Index, match.Length, SpanTrackingMode.EdgeExclusive),
                                                                      Id: match.Groups["id"].Value)).ToList();
            if (inserted.Count == 0)
            {
                return;
            }

            // Can't edit the buffer from inside its Changed event. By the time this runs, a paste of a copy has
            // already been separated by OnPasted, so the duplicate check below finds nothing to do.
            ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                foreach ((ITrackingSpan span, string id) in inserted)
                {
                    this.Separate(span, id, onlyIfDuplicated: true);
                }
            }).FileAndForget("FancyComments/DuplicateGuard");
        }

        /// <summary>Gives the marker at <paramref name="span"/> a new id, with a copy of the original comment's JSON.</summary>
        private void Separate(ITrackingSpan span, string id, bool onlyIfDuplicated)
        {
            ITextSnapshot snapshot = this.buffer.CurrentSnapshot;
            SnapshotSpan current = span.GetSpan(snapshot);
            Match match = CommentMarker.Pattern.Match(current.GetText());

            // The text changed in the meantime, or there's nothing to separate it from.
            if (!match.Success || match.Groups["id"].Value != id || (onlyIfDuplicated && Occurrences(snapshot.GetText(), id) < 2))
            {
                return;
            }

            // Without the original's JSON there's nothing to copy; the marker shows as missing data instead.
            string? path = this.documents.TryGetTextDocument(this.buffer, out ITextDocument document) ? document.FilePath : null;
            if (path is null || CommentStore.ForDocument(path) is not { } store || store.Load(id) is not { } original)
            {
                return;
            }

            FancyComment copy = store.Duplicate(original, store.RelativePath(path), current.Start.GetContainingLine().LineNumber + 1);
            Group body = match.Groups["body"];
            int tagLength = body.Value.IndexOf(']') + 1;
            this.buffer.Replace(new Span(current.Start + body.Index, tagLength), CommentMarker.Tag(copy.Id));
        }

        private static int Occurrences(string text, string id)
        {
            int count = 0;
            for (int i = text.IndexOf("[fc:" + id, StringComparison.Ordinal); i >= 0; i = text.IndexOf("[fc:" + id, i + 1, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }
    }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class DuplicateGuardListener : IWpfTextViewCreationListener
    {
        [Import]
        internal ITextDocumentFactoryService Documents { get; set; } = null!;

        [Import]
        internal ITextUndoHistoryRegistry UndoHistories { get; set; } = null!;

        public void TextViewCreated(IWpfTextView view) => DuplicateGuard.For(view.TextBuffer, this.Documents, this.UndoHistories);
    }
}
