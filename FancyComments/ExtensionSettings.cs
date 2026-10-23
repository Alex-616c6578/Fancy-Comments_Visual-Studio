using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace FancyComments
{
    /// <summary>
    /// The extension's per-user settings, kept in Visual Studio's settings store so they survive restarts.
    /// Read and written on the UI thread only.
    /// </summary>
    internal static class ExtensionSettings
    {
        private const string Collection = "FancyComments";
        private const string HoverPreviewName = "HoverPreview";

        private static bool? hoverPreview;

        /// <summary>Show a comment's rendered markdown when hovering its summary. Off by default.</summary>
        public static bool HoverPreview
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return hoverPreview ??= Store().GetBoolean(Collection, HoverPreviewName, false);
            }

            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                WritableSettingsStore store = Store();
                if (!store.CollectionExists(Collection))
                {
                    store.CreateCollection(Collection);
                }

                store.SetBoolean(Collection, HoverPreviewName, value);
                hoverPreview = value;
            }
        }

        private static WritableSettingsStore Store() =>
            new ShellSettingsManager(ServiceProvider.GlobalProvider).GetWritableSettingsStore(SettingsScope.UserSettings);
    }
}
