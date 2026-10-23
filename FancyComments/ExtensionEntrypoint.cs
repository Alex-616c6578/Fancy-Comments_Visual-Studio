using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

namespace FancyComments
{
    /// <summary>
    /// Extension entrypoint for the VisualStudio.Extensibility extension.
    /// </summary>
    [VisualStudioContribution]
    internal class ExtensionEntrypoint : Extension
    {
        /// <inheritdoc />
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            RequiresInProcessHosting = true,

            // Commands are only initialized when the extension loads, which is otherwise on first use of its menu.
            // Loading once the shell is up lets the Preview Comments on Hover checkmark show the saved setting at
            // startup. The GUID is VSConstants.UICONTEXT.ShellInitialized; it must be a literal for the build.
            LoadedWhen = ActivationConstraint.UIContext(new Guid("E80EF1CB-6D64-4609-8FAA-FEACFD3BC89F")),
        };

        /// <summary>Extensions → Fancy Comments, which holds the extension's commands.</summary>
        [VisualStudioContribution]
        public static MenuConfiguration FancyCommentsMenu => new("%FancyComments.Menu.DisplayName%")
        {
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
            Children = [MenuChild.Command<HoverPreviewCommand>(), MenuChild.Separator, MenuChild.Command<CleanupCommand>()],
        };

        /// <inheritdoc />
        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);

            // You can configure dependency injection here by adding services to the serviceCollection.
        }
    }
}
