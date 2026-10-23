using System.IO;
using System.Windows;
using System.Windows.Threading;
using FancyComments.Model;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Win32;

namespace FancyComments.UI
{
    /// <summary>
    /// Modal markdown editor with a live preview. Fills in the comment's markdown and summary on save.
    /// </summary>
    internal partial class CommentEditorDialog : DialogWindow
    {
        private const long LargeFileWarning = 1024 * 1024;

        private readonly CommentStore store;
        private readonly FancyComment comment;
        private readonly MarkdownPreview preview;
        private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(300) };

        private CommentEditorDialog(CommentStore store, FancyComment comment)
        {
            this.InitializeComponent();
            this.store = store;
            this.comment = comment;
            this.preview = new MarkdownPreview(store.Root);
            this.PreviewHost.Child = this.preview;

            this.refresh.Tick += (_, _) =>
            {
                this.refresh.Stop();
                this.UpdatePreview();
            };
            this.Editor.TextChanged += (_, _) =>
            {
                this.refresh.Stop();
                this.refresh.Start();
            };
            this.DescriptionBox.TextChanged += (_, _) => this.UpdateSummary();
            this.Editor.Text = comment.Markdown;

            // Comments saved before descriptions existed only have the summary.
            this.DescriptionBox.Text = comment.Description.Length > 0 ? comment.Description : comment.Summary;
            this.Loaded += (_, _) =>
            {
                this.UpdatePreview();
                this.DescriptionBox.Focus();
            };
        }

        /// <summary>Shows the dialog; returns true if the user saved.</summary>
        public static bool Edit(CommentStore store, FancyComment comment)
        {
            bool saved = new CommentEditorDialog(store, comment).ShowModal() == true;

            // A saved comment is cleaned up when its JSON is written; a cancelled one may have left inserted media behind.
            if (!saved)
            {
                store.DeleteUnusedAssets(comment.Id);
            }

            return saved;
        }

        private void UpdatePreview()
        {
            this.preview.Render(this.Editor.Text);
            this.UpdateSummary();
        }

        private void UpdateSummary() =>
            this.SummaryText.Text = "Shown in code as: " + CommentMarker.Summarize(this.DescriptionBox.Text, this.Editor.Text);

        private void InsertMedia_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Insert media",
                Filter = "Media|*.png;*.jpg;*.jpeg;*.gif;*.svg;*.webp;*.bmp;*.mp4;*.webm;*.mp3;*.wav;*.ogg;*.m4a;*.pdf"
                    + "|Images|*.png;*.jpg;*.jpeg;*.gif;*.svg;*.webp;*.bmp"
                    + "|Video and audio|*.mp4;*.webm;*.mp3;*.wav;*.ogg;*.m4a"
                    + "|All files|*.*",
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            PreparedAsset asset = ImageOptimizer.Prepare(dialog.FileName, this.OptimizeImages.IsChecked == true);
            if (asset.Bytes.Length > LargeFileWarning && !this.ConfirmLargeFile(asset))
            {
                return;
            }

            string path = this.store.AddAsset(this.comment.Id, asset);
            this.InsertBlock(MediaEmbed.Markdown(Path.GetFileNameWithoutExtension(dialog.FileName), path));
            this.AttachStatus.Text = asset.Optimized
                ? $"{Path.GetFileName(dialog.FileName)}: {FormatSize(asset.OriginalSize)} → {FormatSize(asset.Bytes.Length)}"
                : $"{Path.GetFileName(dialog.FileName)}: {FormatSize(asset.Bytes.Length)}";
        }

        private bool ConfirmLargeFile(PreparedAsset asset)
        {
            string size = FormatSize(asset.Bytes.Length) + (asset.Optimized ? " even after optimizing" : "");
            string message = $"{asset.FileName} is {size}.\n\n"
                + "Files in FancyComments/Assets are committed to the repository, and git keeps every version forever, "
                + "even after the file is deleted.\n\nAttach it anyway?";
            return MessageBox.Show(this, message, "Large file", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                == MessageBoxResult.Yes;
        }

        private static string FormatSize(long bytes) =>
            bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024):0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";

        /// <summary>
        /// Inserts text as its own paragraph. Markdown treats a line after raw HTML (e.g. "&lt;h3&gt;…") as more HTML
        /// until a blank line, so media inserted directly below one would show as plain text.
        /// </summary>
        private void InsertBlock(string block)
        {
            string before = this.Editor.Text.Substring(0, this.Editor.CaretIndex).Replace("\r\n", "\n");
            string after = this.Editor.Text.Substring(this.Editor.CaretIndex);
            string prefix = before.Length == 0 || before.EndsWith("\n\n") ? "" : before.EndsWith("\n") ? "\r\n" : "\r\n\r\n";
            string suffix = after.Length == 0 ? "" : "\r\n\r\n";
            this.Insert(prefix + block + suffix, 0, 0);
        }

        private void InsertLink_Click(object sender, RoutedEventArgs e) =>
            this.Insert("[link text](https://)", 1, "link text".Length);

        /// <summary>Inserts at the caret, then selects <paramref name="selectLength"/> chars from <paramref name="selectOffset"/>.</summary>
        private void Insert(string text, int selectOffset, int selectLength)
        {
            int start = this.Editor.CaretIndex;
            this.Editor.Text = this.Editor.Text.Insert(start, text);
            this.Editor.Focus();
            if (selectLength > 0)
            {
                this.Editor.Select(start + selectOffset, selectLength);
            }
            else
            {
                this.Editor.CaretIndex = start + text.Length;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            this.comment.Description = this.DescriptionBox.Text.Trim();
            this.comment.Markdown = this.Editor.Text;
            this.comment.Summary = CommentMarker.Summarize(this.comment.Description, this.Editor.Text);
            this.comment.Modified = DateTime.UtcNow;
            this.DialogResult = true;
        }
    }
}
