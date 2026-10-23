using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FancyComments.Model;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

namespace FancyComments.Editor
{
    /// <summary>
    /// The one-line preview shown in place of a marker's body: summary text plus "view" and "edit" buttons.
    /// </summary>
    internal sealed class CommentPreview : Border
    {
        public CommentPreview(string id, string summary, FancyCommentController controller)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            TextBlock text = new TextBlock
            {
                Text = summary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };

            // Match the editor font so the preview sits naturally in the line.
            if (controller.View.FormattedLineSource?.DefaultTextProperties is { } properties)
            {
                text.FontFamily = properties.Typeface.FontFamily;
                text.FontSize = properties.FontRenderingEmSize;
                text.Foreground = properties.ForegroundBrush;
            }

            bool missing = controller.IsMissing(id);
            this.Background = missing
                ? new SolidColorBrush(Color.FromArgb(0x40, 0xE5, 0xA0, 0x00))
                : new SolidColorBrush(Color.FromArgb(0x30, 0x56, 0x9C, 0xD6));
            this.CornerRadius = new CornerRadius(3);
            this.Padding = new Thickness(4, 0, 2, 0);
            if (!missing)
            {
                this.ToolTip = "Fancy Comment";

                // With hover previews on, the preview replaces this tooltip; otherwise it shows the start of the
                // comment, read each time so it's never stale. The buttons' tooltips bubble through here too and are
                // left alone.
                this.ToolTipOpening += (_, e) =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    if (e.Source != this)
                    {
                        return;
                    }

                    if (ExtensionSettings.HoverPreview)
                    {
                        e.Handled = true;
                    }
                    else
                    {
                        this.ToolTip = TooltipText(controller, id);
                    }
                };
                HoverPreview.Attach(text, () => controller.TryLoad(id));
            }

            StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
            if (missing)
            {
                content.Children.Add(new CrispImage
                {
                    Moniker = KnownMonikers.StatusWarning,
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            content.Children.Add(text);
            if (missing)
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"— Comment data missing (FancyComments/Comments/{id}.json). It may not be committed, or was deleted.",
                    FontFamily = text.FontFamily,
                    FontSize = text.FontSize,
                    FontStyle = FontStyles.Italic,
                    Foreground = text.Foreground,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0),
                });
            }

            // Without the JSON there's nothing to read, edit or safely delete, so the actions are disabled.
            content.Children.Add(IconButton(KnownMonikers.View, "Read comment", !missing, () => controller.ShowComment(id)));
            content.Children.Add(IconButton(KnownMonikers.Edit, "Edit comment", !missing, () => controller.Edit(id, this.Location)));
            content.Children.Add(IconButton(KnownMonikers.Delete, "Delete comment", !missing, () => controller.Delete(id, this.Location)));
            this.Child = content;
        }

        /// <summary>Where this preview's marker is, set by the tagger once it's tracking it.</summary>
        public ITrackingSpan? Location { get; set; }

        /// <summary>The comment's opening text, or its summary or a generic label if it has none or can't be read.</summary>
        private static string TooltipText(FancyCommentController controller, string id)
        {
            if (controller.TryLoad(id) is not { } loaded)
            {
                return "Fancy Comment";
            }

            string excerpt = CommentMarker.Excerpt(loaded.Comment.Markdown);
            return excerpt.Length > 0 ? excerpt
                : loaded.Comment.Summary.Length > 0 ? loaded.Comment.Summary
                : "Fancy Comment";
        }

        private static Button IconButton(ImageMoniker icon, string tooltip, bool enabled, Action onClick)
        {
            Button button = new Button
            {
                Content = new CrispImage { Moniker = icon, Width = 14, Height = 14, Grayscale = !enabled },
                ToolTip = enabled ? tooltip : "Unavailable: comment data is missing",
                IsEnabled = enabled,
                Opacity = enabled ? 1 : 0.5,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(1),
                Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Disabled buttons don't show tooltips by default, which would leave no explanation.
            ToolTipService.SetShowOnDisabled(button, true);
            button.Click += (_, _) => onClick();
            return button;
        }
    }
}
