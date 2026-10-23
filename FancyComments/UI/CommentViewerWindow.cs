using System.Windows;
using FancyComments.Model;
using Microsoft.VisualStudio.PlatformUI;

namespace FancyComments.UI
{
    /// <summary>
    /// Read-only, non-modal window showing a comment's rendered markdown.
    /// </summary>
    internal sealed class CommentViewerWindow : DialogWindow
    {
        public CommentViewerWindow(CommentStore store, FancyComment comment)
        {
            this.Title = comment.Summary;
            this.Width = 720;
            this.Height = 560;
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.ShowInTaskbar = false;
            this.HasMinimizeButton = false;

            MarkdownPreview preview = new MarkdownPreview(store.Root);
            preview.Render(comment.Markdown);
            this.Content = preview;
        }
    }
}
