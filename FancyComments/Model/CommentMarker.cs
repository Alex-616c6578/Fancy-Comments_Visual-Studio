using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;

namespace FancyComments.Model
{
    /// <summary>
    /// The language-agnostic part of a fancy comment, e.g.
    /// <c>## [fc:1a2b3c4d · Fancy Comment: …] Retry uses exponential backoff ##</c>.
    /// The bracketed tag is what teammates without the extension see first.
    /// </summary>
    internal static class CommentMarker
    {
        /// <summary>"body" is everything between the opening and closing "##", which the editor replaces with the preview.</summary>
        public static readonly Regex Pattern = new(
            @"##\s(?<body>\[fc:(?<id>[0-9a-f]{8})[^\]]*\]\s(?<summary>.*?))\s##",
            RegexOptions.Compiled);

        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 8);

        public static string Tag(string id) =>
            $"[fc:{id} · Fancy Comment: install the Fancy Comments extension, or read FancyComments/Comments/{id}.json]";

        /// <summary>
        /// The text written into the marker: the description if there is one, otherwise the markdown's
        /// first non-empty line, flattened to fit on one line.
        /// </summary>
        public static string Summarize(string description, string markdown)
        {
            description = description.Trim();
            if (description.Length > 0)
            {
                // "##" would end the marker early.
                return description.Replace("##", "#");
            }

            string? first = markdown
                .Split('\n')
                .Select(l => Regex.Replace(l, "<[^>]+>", ""))
                .Select(l => Regex.Replace(l, @"!?\[([^\]]*)\]\([^)]*\)", "$1"))
                .Select(l => l.Trim().TrimStart('#', '>', '-', '*', ' ').Trim())
                .FirstOrDefault(l => l.Length > 0);

            if (first is null)
            {
                return "(empty comment)";
            }

            // "##" would end the marker early.
            first = first.Replace("##", "#");
            return first.Length <= 80 ? first : first.Substring(0, 79) + "…";
        }

        /// <summary>
        /// The start of the markdown as plain text, for a tooltip: formatting removed, one line per paragraph or list
        /// item, and at most <paramref name="maxLength"/> characters, cut at a word and ending in "…" if shortened.
        /// Empty if the markdown has no text (e.g. only a video).
        /// </summary>
        public static string Excerpt(string markdown, int maxLength = 100)
        {
            // Markdig leaves raw HTML out of plain text, which would lose e.g. "<h3>Heading</h3>". Keep its text instead;
            // video and audio tags have none, so they still disappear.
            StringBuilder source = new StringBuilder(markdown);
            foreach (HtmlBlock html in Markdown.Parse(markdown, Pipeline).Descendants<HtmlBlock>().Reverse())
            {
                source.Remove(html.Span.Start, html.Span.Length);
                source.Insert(html.Span.Start, Regex.Replace(markdown.Substring(html.Span.Start, html.Span.Length), "<[^>]+>", ""));
            }

            string text = string.Join("\n", Markdown.ToPlainText(source.ToString(), Pipeline)
                .Split('\n')
                .Select(l => Regex.Replace(l, @"\s+", " ").Trim())
                .Where(l => l.Length > 0));
            if (text.Length <= maxLength)
            {
                return text;
            }

            // Cut at the last space or line break that fits with the "…"; a single very long word is cut mid-word.
            int cut = text.LastIndexOfAny([' ', '\n'], maxLength - 1);
            if (cut < maxLength / 2)
            {
                cut = maxLength - 1;
            }

            if (char.IsHighSurrogate(text[cut - 1]))
            {
                cut--;
            }

            return text.Substring(0, cut).TrimEnd(' ', '\n', ',', ';', ':', '-') + "…";
        }
    }
}
