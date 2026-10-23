using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;

namespace FancyComments.UI
{
    /// <summary>
    /// Renders markdown as a WPF <see cref="FlowDocument"/>, for the hover preview. Unlike <see cref="MarkdownPreview"/>
    /// it needs no browser: images are drawn natively, and video, audio and other media become links that open the file.
    /// Colors come from the host, so the document follows the Visual Studio theme.
    /// </summary>
    internal static class MarkdownDocument
    {
        public const double MaxImageWidth = 480;

        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        // What WPF can decode without extra codecs; other image types (SVG, WebP) are shown as links.
        private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

        private static readonly Regex MediaTag = new(@"<(?<kind>video|audio)\b[^>]*\bsrc\s*=\s*""(?<src>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);

        private static readonly FontFamily CodeFont = new("Consolas");
        private static readonly Brush CodeBackground = Frozen(new SolidColorBrush(Color.FromArgb(0x26, 0x80, 0x80, 0x80)));
        private static readonly Brush RuleBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x80)));
        private static readonly Brush LinkBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x37, 0x94, 0xFF)));

        /// <summary>
        /// Builds the document. Relative links and images resolve against <paramref name="root"/> (the FancyComments
        /// folder); clicking a link calls <paramref name="open"/> with the URL or local path.
        /// </summary>
        public static FlowDocument Build(string markdown, string root, Action<string> open)
        {
            FlowDocument document = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                PagePadding = new Thickness(0),
                TextAlignment = TextAlignment.Left,
            };

            Renderer renderer = new Renderer(root, open);
            foreach (MdBlock block in Markdown.Parse(markdown, Pipeline))
            {
                if (renderer.Block(block) is { } rendered)
                {
                    document.Blocks.Add(rendered);
                }
            }

            return document;
        }

        /// <summary>
        /// The file a relative link points to, e.g. "Assets/a-chart%281%29.png" under <paramref name="root"/>.
        /// Null for absolute URLs and for paths that would escape the root.
        /// </summary>
        public static string? LocalPath(string root, string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? _) || url.StartsWith("/", StringComparison.Ordinal) || url.StartsWith("\\", StringComparison.Ordinal))
            {
                return null;
            }

            string fullRoot = Path.GetFullPath(root);
            string relative = Uri.UnescapeDataString(url.Split('?', '#')[0]);
            try
            {
                string path = Path.GetFullPath(Path.Combine(fullRoot, relative));
                return path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        private static T Frozen<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }

        private sealed class Renderer
        {
            private readonly string root;
            private readonly Action<string> open;

            public Renderer(string root, Action<string> open)
            {
                this.root = root;
                this.open = open;
            }

            public WpfBlock? Block(MdBlock block)
            {
                switch (block)
                {
                    case HeadingBlock heading:
                        Paragraph title = this.Paragraph(heading.Inline);
                        title.FontWeight = FontWeights.SemiBold;
                        title.FontSize = heading.Level switch { 1 => 20, 2 => 17, 3 => 15, _ => 13 };
                        title.Margin = new Thickness(0, 8, 0, 4);
                        return title;

                    case ParagraphBlock paragraph:
                        return this.Paragraph(paragraph.Inline);

                    case CodeBlock code:
                        return new Paragraph(new Run(code.Lines.ToString()))
                        {
                            FontFamily = CodeFont,
                            Background = CodeBackground,
                            Padding = new Thickness(6),
                            Margin = new Thickness(0, 4, 0, 8),
                        };

                    case HtmlBlock html:
                        return this.Html(html.Lines.ToString());

                    case ThematicBreakBlock:
                        return new Paragraph
                        {
                            BorderBrush = RuleBrush,
                            BorderThickness = new Thickness(0, 1, 0, 0),
                            Margin = new Thickness(0, 8, 0, 8),
                        };

                    case QuoteBlock quote:
                        Section section = this.Section(quote);
                        section.BorderBrush = RuleBrush;
                        section.BorderThickness = new Thickness(3, 0, 0, 0);
                        section.Padding = new Thickness(10, 0, 0, 0);
                        return section;

                    case ListBlock list:
                        return this.List(list);

                    case MdTable table:
                        return this.Table(table);

                    case ContainerBlock container:
                        // Anything else with children (footnote groups, custom containers): render what's inside.
                        Section inner = this.Section(container);
                        return inner.Blocks.Count > 0 ? inner : null;

                    case LeafBlock leaf when leaf.Inline is not null:
                        return this.Paragraph(leaf.Inline);

                    default:
                        return null;
                }
            }

            private Section Section(ContainerBlock container)
            {
                Section section = new Section();
                foreach (MdBlock child in container)
                {
                    if (this.Block(child) is { } rendered)
                    {
                        section.Blocks.Add(rendered);
                    }
                }

                return section;
            }

            private List List(ListBlock list)
            {
                List result = new List
                {
                    MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(20, 0, 0, 0),
                };
                if (list.IsOrdered && int.TryParse(list.OrderedStart, out int start))
                {
                    result.StartIndex = start;
                }

                foreach (MdBlock child in list)
                {
                    ListItem item = new ListItem();
                    foreach (MdBlock content in (ContainerBlock)child)
                    {
                        if (this.Block(content) is { } rendered)
                        {
                            // Paragraph spacing inside a list item would spread the list out.
                            rendered.Margin = new Thickness(0, 0, 0, 2);
                            item.Blocks.Add(rendered);
                        }
                    }

                    result.ListItems.Add(item);
                }

                return result;
            }

            private WpfTable Table(MdTable table)
            {
                WpfTable result = new WpfTable { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 8) };
                TableRowGroup rows = new TableRowGroup();
                foreach (MdTableRow row in table.OfType<MdTableRow>())
                {
                    WpfTableRow rendered = new WpfTableRow();
                    foreach (MdTableCell cell in row.OfType<MdTableCell>())
                    {
                        WpfTableCell renderedCell = new WpfTableCell(this.Section(cell))
                        {
                            BorderBrush = RuleBrush,
                            BorderThickness = new Thickness(0.5),
                            Padding = new Thickness(6, 2, 6, 2),
                        };
                        if (row.IsHeader)
                        {
                            renderedCell.FontWeight = FontWeights.SemiBold;
                        }

                        rendered.Cells.Add(renderedCell);
                    }

                    rows.Rows.Add(rendered);
                }

                result.RowGroups.Add(rows);
                return result;
            }

            private Paragraph Paragraph(ContainerInline? inlines)
            {
                Paragraph paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                this.AddInlines(paragraph.Inlines, inlines);
                return paragraph;
            }

            private void AddInlines(InlineCollection target, ContainerInline? inlines)
            {
                if (inlines is null)
                {
                    return;
                }

                foreach (MdInline inline in inlines)
                {
                    if (this.Inline(inline) is { } rendered)
                    {
                        target.Add(rendered);
                    }
                }
            }

            private WpfInline? Inline(MdInline inline)
            {
                switch (inline)
                {
                    case LiteralInline literal:
                        return new Run(literal.Content.ToString());

                    case CodeInline code:
                        return new Run(code.Content) { FontFamily = CodeFont, Background = CodeBackground };

                    case LineBreakInline lineBreak:
                        return lineBreak.IsHard ? new LineBreak() : new Run(" ");

                    case HtmlEntityInline entity:
                        return new Run(entity.Transcoded.ToString());

                    case TaskList task:
                        return new Run(task.Checked ? "☑ " : "☐ ");

                    case AutolinkInline autolink:
                        return this.Link(autolink.Url, new Run(autolink.Url));

                    case LinkInline { IsImage: true } image:
                        return this.Image(image);

                    case LinkInline link:
                        Span label = new Span();
                        this.AddInlines(label.Inlines, link);
                        return this.Link(link.Url ?? "", label);

                    case EmphasisInline emphasis:
                        Span span = new Span();
                        this.AddInlines(span.Inlines, emphasis);
                        if (emphasis.DelimiterChar == '~')
                        {
                            span.TextDecorations = TextDecorations.Strikethrough;
                        }
                        else if (emphasis.DelimiterCount >= 2)
                        {
                            span.FontWeight = FontWeights.Bold;
                        }
                        else
                        {
                            span.FontStyle = FontStyles.Italic;
                        }

                        return span;

                    case HtmlInline html:
                        return MediaTag.Match(html.Tag) is { Success: true } media ? this.MediaLink(media) : null;

                    case ContainerInline container:
                        Span children = new Span();
                        this.AddInlines(children.Inlines, container);
                        return children;

                    default:
                        return null;
                }
            }

            /// <summary>Video and audio players can't be shown without a browser, so each becomes a link that opens the file.</summary>
            private WpfBlock? Html(string html)
            {
                Paragraph paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                foreach (Match media in MediaTag.Matches(html))
                {
                    if (paragraph.Inlines.Count > 0)
                    {
                        paragraph.Inlines.Add(new LineBreak());
                    }

                    paragraph.Inlines.Add(this.MediaLink(media));
                }

                if (paragraph.Inlines.Count == 0)
                {
                    string text = System.Net.WebUtility.HtmlDecode(HtmlTag.Replace(html, "")).Trim();
                    if (text.Length == 0)
                    {
                        return null;
                    }

                    paragraph.Inlines.Add(new Run(text));
                }

                return paragraph;
            }

            private WpfInline MediaLink(Match media)
            {
                string src = media.Groups["src"].Value;
                string kind = media.Groups["kind"].Value.Equals("video", StringComparison.OrdinalIgnoreCase) ? "Video" : "Audio";
                return this.Link(src, new Run($"▶ {kind}: {Uri.UnescapeDataString(Path.GetFileName(src))}"));
            }

            private WpfInline Image(LinkInline image)
            {
                string alt = string.Concat(image.Descendants<LiteralInline>().Select(l => l.Content.ToString()));
                string url = image.Url ?? "";
                string? path = LocalPath(this.root, url);
                if (path is null || !ImageTypes.Contains(Path.GetExtension(path)))
                {
                    // Remote images aren't fetched (a hover shouldn't make network requests), and SVG/WebP need a browser.
                    return this.Link(url, new Run("🖼 " + (alt.Length > 0 ? alt : Path.GetFileName(url))));
                }

                if (LoadImage(path) is not { } source)
                {
                    return new Run($"[image not found: {(alt.Length > 0 ? alt : Path.GetFileName(path))}]") { FontStyle = FontStyles.Italic };
                }

                return new InlineUIContainer(new Image
                {
                    Source = source,
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    MaxWidth = MaxImageWidth,
                    ToolTip = alt.Length > 0 ? alt : null,
                });
            }

            private Hyperlink Link(string url, WpfInline label)
            {
                Hyperlink link = new Hyperlink(label) { Foreground = LinkBrush, ToolTip = url };
                if (LocalPath(this.root, url) is { } path)
                {
                    link.Click += (_, _) => this.open(path);
                }
                else if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https" or "mailto")
                {
                    link.Click += (_, _) => this.open(uri.AbsoluteUri);
                }

                return link;
            }

            /// <summary>Decodes at most <see cref="MaxImageWidth"/> pixels wide, so a large photo doesn't cost a full-size bitmap.</summary>
            private static BitmapSource? LoadImage(string path)
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                try
                {
                    int width;
                    using (FileStream stream = File.OpenRead(path))
                    {
                        width = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).PixelWidth;
                    }

                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(path);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = (int)Math.Min(width, MaxImageWidth);
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or InvalidOperationException)
                {
                    return null;
                }
            }
        }
    }
}
