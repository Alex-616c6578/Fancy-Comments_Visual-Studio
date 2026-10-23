using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using FancyComments.Model;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace FancyComments.Editor
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class FancyCommentTaggerProvider : IViewTaggerProvider
    {
        [Import]
        internal ITextDocumentFactoryService Documents { get; set; } = null!;

        public ITagger<T>? CreateTagger<T>(ITextView textView, ITextBuffer buffer)
            where T : ITag
        {
            if (buffer != textView.TextBuffer || textView is not IWpfTextView view)
            {
                return null;
            }

            FancyCommentController controller = FancyCommentController.For(view, this.Documents);
            return view.Properties.GetOrCreateSingletonProperty(() => new FancyCommentTagger(controller)) as ITagger<T>;
        }
    }

    /// <summary>
    /// Hides the body of each "## [fc:…] summary ##" marker behind a <see cref="CommentPreview"/>,
    /// leaving the language's comment delimiters and the "##"s visible.
    /// </summary>
    internal sealed class FancyCommentTagger : ITagger<IntraTextAdornmentTag>
    {
        private readonly FancyCommentController controller;

        // The editor treats a new adornment element as a changed line and lays it out again, which asks for tags again.
        // Handing back the same element until the comment actually changes stops that loop.
        private readonly List<CachedPreview> cache = [];

        public FancyCommentTagger(FancyCommentController controller)
        {
            this.controller = controller;
            IWpfTextView view = controller.View;
            view.TextBuffer.Changed += this.OnBufferChanged;
            view.Closed += (_, _) =>
            {
                view.TextBuffer.Changed -= this.OnBufferChanged;
                this.cache.Clear();
            };
        }

        public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;
            HashSet<int> seen = new HashSet<int>();
            foreach (SnapshotSpan span in spans)
            {
                int last = snapshot.GetLineNumberFromPosition(span.End);
                for (int number = snapshot.GetLineNumberFromPosition(span.Start); number <= last; number++)
                {
                    if (!seen.Add(number))
                    {
                        continue;
                    }

                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(number);
                    string text = line.GetText();
                    if (!text.Contains("[fc:"))
                    {
                        continue;
                    }

                    foreach (Match match in CommentMarker.Pattern.Matches(text))
                    {
                        Group body = match.Groups["body"];
                        SnapshotSpan bodySpan = new SnapshotSpan(line.Start + body.Index, body.Length);
                        CommentPreview preview = this.GetPreview(bodySpan, match.Groups["id"].Value, match.Groups["summary"].Value);
                        yield return new TagSpan<IntraTextAdornmentTag>(bodySpan, new IntraTextAdornmentTag(preview, null, PositionAffinity.Successor));
                    }
                }
            }
        }

        /// <summary>
        /// The cached preview for the comment at this position, or a new one if there isn't one yet or the comment
        /// changed (different text, or its JSON appeared or disappeared). Keyed by position too, so a comment
        /// pasted twice gets two elements; one element can't be shown in two places.
        /// </summary>
        private CommentPreview GetPreview(SnapshotSpan span, string id, string summary)
        {
            string key = $"{id}|{summary}|{this.controller.IsMissing(id)}";
            foreach (CachedPreview cached in this.cache)
            {
                if (cached.Key == key && cached.Span.GetStartPoint(span.Snapshot) == span.Start)
                {
                    return cached.Preview;
                }
            }

            ITrackingSpan tracking = span.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeExclusive);
            CommentPreview preview = new CommentPreview(id, summary, this.controller) { Location = tracking };
            this.cache.RemoveAll(c => c.Span.GetStartPoint(span.Snapshot) == span.Start);
            this.cache.Add(new CachedPreview(tracking, key, preview));
            return preview;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (e.Changes.Count == 0)
            {
                return;
            }

            // Comments touched by the edit get fresh previews.
            this.cache.RemoveAll(c => e.Changes.Any(change => c.Span.GetSpan(e.After).IntersectsWith(change.NewSpan)));

            // A marker is always on one line, so re-tagging the touched lines is enough. One event for the whole
            // edit: something like Format Document can make hundreds of changes at once.
            SnapshotPoint start = e.After.GetLineFromPosition(e.Changes[0].NewPosition).Start;
            SnapshotPoint end = e.After.GetLineFromPosition(e.Changes[e.Changes.Count - 1].NewEnd).End;
            this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(start, end)));
        }

        private sealed class CachedPreview
        {
            public CachedPreview(ITrackingSpan span, string key, CommentPreview preview)
            {
                this.Span = span;
                this.Key = key;
                this.Preview = preview;
            }

            public ITrackingSpan Span { get; }

            public string Key { get; }

            public CommentPreview Preview { get; }
        }
    }
}
