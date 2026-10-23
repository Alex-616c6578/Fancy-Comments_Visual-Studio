using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Controls;
using Markdig;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace FancyComments.UI
{
    /// <summary>
    /// Renders markdown with Markdig into a WebView2. The page and the FancyComments folder are both served from
    /// https://fancycomments.local/ (read from disk per request), so "Assets/…" links and media resolve.
    /// </summary>
    internal sealed class MarkdownPreview : UserControl
    {
        private const string Host = "fancycomments.local";
        private const string PageUrl = "https://" + Host + "/__preview__.html";

        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml", [".webp"] = "image/webp", [".bmp"] = "image/bmp", [".pdf"] = "application/pdf",
            [".mp4"] = "video/mp4", [".webm"] = "video/webm",
            [".mp3"] = "audio/mpeg", [".wav"] = "audio/wav", [".ogg"] = "audio/ogg", [".m4a"] = "audio/mp4",
        };

        private readonly WebView2 web = new();
        private string? pending;
        private string html = "";

        public MarkdownPreview(string root)
        {
            this.Root = root;
            this.Content = this.web;
            // FileAndForget is the documented way to fire and forget a JoinableTask, but VSSDK007 doesn't recognize it.
#pragma warning disable VSSDK007
            this.Loaded += (_, _) => ThreadHelper.JoinableTaskFactory.RunAsync(this.InitializeAsync).FileAndForget("FancyComments/MarkdownPreview");
#pragma warning restore VSSDK007

            // Shut the browser down with the window, or its processes linger after the dialog closes.
            this.Unloaded += (_, _) => this.web.Dispose();
        }

        public string Root { get; }

        public void Render(string markdown)
        {
            if (this.web is null || this.web.CoreWebView2 is null)
            {
                this.pending = markdown;
                return;
            }

            this.html = ToHtml(Markdown.ToHtml(markdown, Pipeline));
            this.web.CoreWebView2.Navigate(PageUrl);
        }

        private async Task InitializeAsync()
        {
            if (this.web.CoreWebView2 is not null)
            {
                return;
            }

            // VS's install folder isn't writable, so WebView2 needs its own profile folder.
            string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FancyComments", "WebView2");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await this.web.EnsureCoreWebView2Async(environment);

            CoreWebView2 core = this.web.CoreWebView2!;
            core.Settings.IsScriptEnabled = false;
            core.AddWebResourceRequestedFilter($"https://{Host}/*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += this.OnWebResourceRequested;
            core.NavigationStarting += this.OnNavigationStarting;
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                this.Open(e.Uri);
            };

            if (this.pending is not null)
            {
                this.Render(this.pending);
            }
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Our own Render navigates to the preview page; anything else is a clicked link.
            if (e.Uri == PageUrl)
            {
                return;
            }

            e.Cancel = true;
            this.Open(e.Uri);
        }

        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            CoreWebView2Environment environment = this.web.CoreWebView2!.Environment;
            Uri uri = new Uri(e.Request.Uri);
            if (uri.GetLeftPart(UriPartial.Path) == PageUrl)
            {
                e.Response = environment.CreateWebResourceResponse(
                    new MemoryStream(Encoding.UTF8.GetBytes(this.html)), 200, "OK", "Content-Type: text/html; charset=utf-8\nCache-Control: no-store");
                return;
            }

            string? path = this.LocalPath(uri);
            if (path is null || !File.Exists(path))
            {
                e.Response = environment.CreateWebResourceResponse(null, 404, "Not Found", "");
                return;
            }

            string type = MimeTypes.TryGetValue(Path.GetExtension(path), out string mime) ? mime : "application/octet-stream";
            e.Response = environment.CreateWebResourceResponse(new MemoryStream(File.ReadAllBytes(path)), 200, "OK", "Content-Type: " + type);
        }

        /// <summary>Maps https://fancycomments.local/Assets/x.png to the file under Root; null if it would escape Root.</summary>
        private string? LocalPath(Uri uri) => MarkdownDocument.LocalPath(this.Root, uri.AbsolutePath.TrimStart('/'));

        private void Open(string uri)
        {
            // Links to attached assets open the local file; everything else (wikis etc.) opens in the browser.
            Uri target = new Uri(uri);
            if (target.Host != Host)
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            else if (this.LocalPath(target) is { } path && File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }

        private static string ToHtml(string body)
        {
            string background = Css(VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey));
            string foreground = Css(VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey));
            return $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8"">
<style>
body {{ font-family: 'Segoe UI', sans-serif; font-size: 14px; line-height: 1.5; background: {background}; color: {foreground}; padding: 0 12px; }}
a {{ color: #3794ff; }}
img {{ max-width: 100%; }}
code, pre {{ font-family: Consolas, monospace; background: rgba(127,127,127,.15); }}
code {{ padding: 1px 4px; }}
pre {{ padding: 8px; overflow: auto; }}
pre code {{ padding: 0; background: none; }}
blockquote {{ margin: 0; padding-left: 12px; border-left: 3px solid rgba(127,127,127,.5); }}
table {{ border-collapse: collapse; }}
td, th {{ border: 1px solid rgba(127,127,127,.4); padding: 4px 8px; }}
</style></head><body>{body}</body></html>";
        }

        private static string Css(System.Drawing.Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";
    }
}
