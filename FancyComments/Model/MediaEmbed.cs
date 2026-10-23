using System.IO;

namespace FancyComments.Model
{
    /// <summary>
    /// The Markdown inserted for an attached file: images embed, video and audio get players (Markdown has no syntax
    /// for them, so raw HTML), and anything else (PDFs etc.) becomes a link that opens the file.
    /// </summary>
    internal static class MediaEmbed
    {
        // What the preview (WebView2) can show inline; anything else is inserted as a link.
        private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".bmp" };
        private static readonly HashSet<string> VideoTypes = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };
        private static readonly HashSet<string> AudioTypes = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".ogg", ".m4a" };

        public static string Markdown(string name, string path)
        {
            // Parentheses would end a Markdown link early; the cleanup unescapes them when matching files.
            string url = path.Replace("(", "%28").Replace(")", "%29");
            string extension = Path.GetExtension(path);
            return ImageTypes.Contains(extension) ? $"![{name}]({url})"
                : VideoTypes.Contains(extension) ? $"<video controls src=\"{url}\"></video>"
                : AudioTypes.Contains(extension) ? $"<audio controls src=\"{url}\"></audio>"
                : $"[{name}]({url})";
        }
    }
}
