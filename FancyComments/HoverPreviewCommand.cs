using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Shell;

namespace FancyComments
{
    /// <summary>
    /// Extensions → Fancy Comments → Preview Comments on Hover: a checkable item that turns the hover preview
    /// (see <see cref="Editor.HoverPreview"/>) on and off.
    /// </summary>
    [VisualStudioContribution]
    internal class HoverPreviewCommand : ToggleCommand
    {
        /// <inheritdoc />
        /// <remarks>Placed in the Extensions → Fancy Comments menu (see <see cref="ExtensionEntrypoint.FancyCommentsMenu"/>).</remarks>
        public override CommandConfiguration CommandConfiguration => new("%FancyComments.HoverPreviewCommand.DisplayName%")
        {
            Flags = CommandFlags.CanToggle,
        };

        /// <inheritdoc />
        /// <remarks>Runs at startup (see <see cref="ExtensionEntrypoint.ExtensionConfiguration"/>), so the checkmark matches the saved setting.</remarks>
        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await base.InitializeAsync(cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            this.IsChecked = ExtensionSettings.HoverPreview;
        }

        /// <inheritdoc />
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            ExtensionSettings.HoverPreview = !ExtensionSettings.HoverPreview;
            this.IsChecked = ExtensionSettings.HoverPreview;
            if (!ExtensionSettings.HoverPreview)
            {
                Editor.HoverPreview.Close();
            }
        }
    }
}
