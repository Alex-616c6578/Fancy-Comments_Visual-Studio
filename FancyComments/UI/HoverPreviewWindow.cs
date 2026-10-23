using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FancyComments.Model;
using Microsoft.VisualStudio.PlatformUI;

namespace FancyComments.UI
{
    /// <summary>
    /// A borderless, tooltip-styled window showing a comment's rendered markdown next to its marker. Native WPF
    /// (see <see cref="MarkdownDocument"/>), so no browser process is started. It never takes focus from the editor.
    /// </summary>
    internal sealed class HoverPreviewWindow : Window
    {
        private const double PreviewWidth = 520;

        public HoverPreviewWindow(CommentStore store, FancyComment comment)
        {
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            this.ShowInTaskbar = false;
            this.ShowActivated = false;
            this.Owner = Application.Current.MainWindow;
            this.Width = PreviewWidth;
            this.MaxHeight = 420;
            this.SizeToContent = SizeToContent.Height;
            this.BorderThickness = new Thickness(1);
            this.SetResourceReference(BackgroundProperty, EnvironmentColors.ToolTipBrushKey);
            this.SetResourceReference(BorderBrushProperty, EnvironmentColors.ToolTipBorderBrushKey);

            // A comment with only a description has no markdown; show the description rather than an empty box.
            string markdown = string.IsNullOrWhiteSpace(comment.Markdown) ? comment.Summary : comment.Markdown;
            FlowDocument document = MarkdownDocument.Build(markdown, store.Root, Open);
            document.SetResourceReference(FlowDocument.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);
            document.SetResourceReference(FlowDocument.BackgroundProperty, EnvironmentColors.ToolTipBrushKey);

            this.Content = new FlowDocumentScrollViewer
            {
                Document = document,
                IsToolBarVisible = false,
                Focusable = false,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(10, 6, 10, 6),
            };
        }

        /// <summary>Shows the window just below <paramref name="anchor"/>, or above it if there's no room below.</summary>
        public void ShowAt(FrameworkElement anchor)
        {
            Matrix toDips = PresentationSource.FromVisual(anchor)?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;
            Point topLeft = toDips.Transform(anchor.PointToScreen(new Point(0, 0)));
            Point bottomLeft = toDips.Transform(anchor.PointToScreen(new Point(0, anchor.ActualHeight)));

            double screenRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
            double screenBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
            this.Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(bottomLeft.X, screenRight - PreviewWidth));
            this.Top = bottomLeft.Y;
            this.Show();

            // The height is only known once the content is laid out.
            if (this.Top + this.ActualHeight > screenBottom)
            {
                this.Top = Math.Max(SystemParameters.VirtualScreenTop, topLeft.Y - this.ActualHeight);
            }
        }

        private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }
}
