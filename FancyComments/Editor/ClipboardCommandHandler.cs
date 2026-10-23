using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace FancyComments.Editor
{
    /// <summary>
    /// Watches Copy, Cut and Paste so <see cref="DuplicateGuard"/> can tell a copied marker (needs a new id) from a
    /// moved one (keeps its id). It never changes what the commands themselves do.
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [ContentType("text")]
    [Name("FancyComments.Clipboard")]
    internal sealed class ClipboardCommandHandler :
        ICommandHandler<CopyCommandArgs>,
        ICommandHandler<CutCommandArgs>,
        IChainedCommandHandler<PasteCommandArgs>
    {
        [Import]
        internal ITextDocumentFactoryService Documents { get; set; } = null!;

        [Import]
        internal ITextUndoHistoryRegistry UndoHistories { get; set; } = null!;

        public string DisplayName => "Fancy Comments clipboard";

        public CommandState GetCommandState(CopyCommandArgs args) => CommandState.Unspecified;

        public CommandState GetCommandState(CutCommandArgs args) => CommandState.Unspecified;

        public CommandState GetCommandState(PasteCommandArgs args, Func<CommandState> nextCommandHandler) => nextCommandHandler();

        public bool ExecuteCommand(CopyCommandArgs args, CommandExecutionContext executionContext)
        {
            DuplicateGuard.OnCopied(SelectedText(args.TextView));

            // Not handled: let Visual Studio do the actual copy.
            return false;
        }

        public bool ExecuteCommand(CutCommandArgs args, CommandExecutionContext executionContext)
        {
            DuplicateGuard.OnCut();
            return false;
        }

        public void ExecuteCommand(PasteCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            ITextBuffer buffer = args.SubjectBuffer;
            ITextVersion before = buffer.CurrentSnapshot.Version;
            nextCommandHandler();
            ITextVersion after = buffer.CurrentSnapshot.Version;

            // Collect the markers the paste inserted, across however many versions it took.
            List<(ITrackingSpan Span, string Id)> pasted = new List<(ITrackingSpan Span, string Id)>();
            for (ITextVersion version = before; version != after && version.Next is { } next; version = next)
            {
                foreach (ITextChange change in version.Changes)
                {
                    foreach (Match match in DuplicateGuard.Markers(change.NewText))
                    {
                        ITrackingSpan span = next.CreateTrackingSpan(new Span(change.NewPosition + match.Index, match.Length), SpanTrackingMode.EdgeExclusive);
                        pasted.Add((span, match.Groups["id"].Value));
                    }
                }
            }

            if (pasted.Count > 0)
            {
                DuplicateGuard.For(buffer, this.Documents, this.UndoHistories).OnPasted(pasted);
            }
        }

        /// <summary>What Copy puts on the clipboard: the selection, or the whole line when nothing is selected.</summary>
        private static string SelectedText(ITextView view) =>
            view.Selection.IsEmpty
                ? view.Caret.Position.BufferPosition.GetContainingLine().GetTextIncludingLineBreak()
                : string.Join("\n", view.Selection.SelectedSpans.Select(s => s.GetText()));
    }
}
