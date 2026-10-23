using System.ComponentModel.Composition;
using FancyComments.Model;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace FancyComments.Editor
{
    /// <summary>
    /// Opens the comment editor when the user types a trigger such as "/*##" or "# ##".
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class TriggerListener : IWpfTextViewCreationListener
    {
        [Import]
        internal ITextDocumentFactoryService Documents { get; set; } = null!;

        public void TextViewCreated(IWpfTextView view)
        {
            FancyCommentController controller = FancyCommentController.For(view, this.Documents);
            EventHandler<TextContentChangedEventArgs> onChanged = (_, e) => OnChanged(controller, e);
            view.TextBuffer.Changed += onChanged;
            view.Closed += (_, _) => view.TextBuffer.Changed -= onChanged;
        }

        private static void OnChanged(FancyCommentController controller, TextContentChangedEventArgs e)
        {
            // Only react to typing: one short insertion, not a paste or our own marker insert.
            // Every view on this buffer (split windows, hidden views) gets this event; only the focused one acts.
            if (e.Changes.Count != 1 || e.Changes[0].NewLength is 0 or > 2 || !controller.View.HasAggregateFocus)
            {
                return;
            }

            int end = e.Changes[0].NewEnd;
            ITextSnapshotLine line = e.After.GetLineFromPosition(end);
            string before = e.After.GetText(line.Start, end - line.Start);

            foreach (CommentSyntax syntax in CommentLanguages.For(controller.FilePath))
            {
                if (before.EndsWith(syntax.Trigger))
                {
                    // However many times this edit reaches us (several views, listeners), handle each buffer version once.
                    PropertyCollection properties = e.After.TextBuffer.Properties;
                    if (properties.TryGetProperty(typeof(TriggerListener), out ITextVersion handled) && handled == e.AfterVersion)
                    {
                        return;
                    }

                    properties[typeof(TriggerListener)] = e.AfterVersion;
                    SnapshotSpan trigger = new SnapshotSpan(e.After, end - syntax.Trigger.Length, syntax.Trigger.Length);

                    // Don't open a modal from inside a buffer change event.
                    ThreadHelper.JoinableTaskFactory.StartOnIdle(() => controller.Create(trigger, syntax)).FileAndForget("FancyComments/Create");
                    return;
                }
            }
        }
    }
}
