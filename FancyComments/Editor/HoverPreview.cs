using System.Windows;
using System.Windows.Threading;
using FancyComments.Model;
using FancyComments.UI;
using Microsoft.VisualStudio.Shell;

namespace FancyComments.Editor
{
    /// <summary>
    /// Opens a <see cref="HoverPreviewWindow"/> when the mouse rests on a comment's summary, and closes it once the
    /// mouse has left both the summary and the window. One preview at a time; UI thread only. Nothing is kept
    /// between hovers: each preview builds a new window and closing it releases everything.
    /// </summary>
    internal static class HoverPreview
    {
        // Like Visual Studio's own tooltips, so moving the mouse across the code doesn't flash previews open.
        private static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(400);

        // Long enough to move the mouse from the summary into the preview without it closing.
        private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(250);

        private static DispatcherTimer? openTimer;
        private static DispatcherTimer? closeTimer;
        private static FrameworkElement? pending;
        private static Func<(CommentStore Store, FancyComment Comment)?>? pendingLoad;
        private static FrameworkElement? anchor;
        private static HoverPreviewWindow? window;

        /// <summary>Shows the preview when hovering <paramref name="target"/>; <paramref name="load"/> returns null if there's nothing to show.</summary>
        public static void Attach(FrameworkElement target, Func<(CommentStore Store, FancyComment Comment)?> load)
        {
            if (openTimer is null)
            {
                openTimer = new DispatcherTimer { Interval = OpenDelay };
                openTimer.Tick += (_, _) => Open();
                closeTimer = new DispatcherTimer { Interval = CloseDelay };
                closeTimer.Tick += (_, _) => CloseIfMouseLeft();

                // Switching to another app shouldn't leave a preview floating over it.
                Application.Current.Deactivated += (_, _) => Close();
            }

            target.MouseEnter += (_, _) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (target == anchor)
                {
                    closeTimer!.Stop();
                }
                else if (ExtensionSettings.HoverPreview)
                {
                    pending = target;
                    pendingLoad = load;
                    Restart(openTimer);
                }
            };
            target.MouseLeave += (_, _) =>
            {
                if (target == pending)
                {
                    CancelPending();
                }

                if (target == anchor)
                {
                    Restart(closeTimer!);
                }
            };

            // A click means the user is doing something else with the comment; the marker leaving the editor
            // (edited, scrolled out of a collapsed region, file closed) means the preview no longer points anywhere.
            target.PreviewMouseDown += (_, _) => Forget(target);
            target.Unloaded += (_, _) => Forget(target);
        }

        public static void Close()
        {
            CancelPending();
            closeTimer?.Stop();
            window?.Close();
            window = null;
            anchor = null;
        }

        private static void Open()
        {
            FrameworkElement? target = pending;
            Func<(CommentStore Store, FancyComment Comment)?>? load = pendingLoad;
            CancelPending();
            if (target is null || load is null || !target.IsMouseOver || !target.IsVisible || load() is not { } loaded)
            {
                return;
            }

            Close();
            window = new HoverPreviewWindow(loaded.Store, loaded.Comment);
            window.MouseEnter += (_, _) => closeTimer!.Stop();
            window.MouseLeave += (_, _) => Restart(closeTimer!);
            anchor = target;
            window.ShowAt(target);
        }

        private static void CloseIfMouseLeft()
        {
            closeTimer!.Stop();
            if (anchor?.IsMouseOver != true && window?.IsMouseOver != true)
            {
                Close();
            }
        }

        private static void Forget(FrameworkElement target)
        {
            if (target == pending)
            {
                CancelPending();
            }

            if (target == anchor)
            {
                Close();
            }
        }

        private static void CancelPending()
        {
            openTimer?.Stop();
            pending = null;
            pendingLoad = null;
        }

        private static void Restart(DispatcherTimer timer)
        {
            timer.Stop();
            timer.Start();
        }
    }
}
