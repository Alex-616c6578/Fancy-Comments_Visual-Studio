using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using FancyComments.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyComments.Tests
{
    [TestClass]
    public class MarkdownDocumentTests
    {
        private string root = "";

        [TestInitialize]
        public void CreateFolder()
        {
            this.root = Path.Combine(Path.GetTempPath(), "fc-markdown-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(this.root, "Assets"));
        }

        [TestCleanup]
        public void DeleteFolder() => Directory.Delete(this.root, recursive: true);

        [TestMethod]
        public void Headings_are_bold_and_larger_than_body_text() => TestImages.Sta(() =>
        {
            FlowDocument document = this.Build("# Title\n\nBody");

            Paragraph heading = (Paragraph)document.Blocks.FirstBlock;
            Paragraph body = (Paragraph)document.Blocks.LastBlock;
            Assert.AreEqual("Title", Text(heading));
            Assert.AreEqual(FontWeights.SemiBold, heading.FontWeight);
            Assert.IsTrue(heading.FontSize > body.FontSize);
        });

        [TestMethod]
        public void Emphasis_bold_strikethrough_and_code_are_styled() => TestImages.Sta(() =>
        {
            FlowDocument document = this.Build("*it* **bold** ~~gone~~ `code`");

            List<Inline> inlines = ((Paragraph)document.Blocks.FirstBlock).Inlines.ToList();
            Assert.AreEqual(FontStyles.Italic, Find(inlines, "it").FontStyle);
            Assert.AreEqual(FontWeights.Bold, Find(inlines, "bold").FontWeight);
            Assert.AreEqual(TextDecorations.Strikethrough, Find(inlines, "gone").TextDecorations);
            Assert.AreEqual("Consolas", Find(inlines, "code").FontFamily.Source);
        });

        [TestMethod]
        public void Lists_keep_their_items_and_numbering() => TestImages.Sta(() =>
        {
            FlowDocument document = this.Build("3. three\n4. four\n\n- a\n- b\n- c");

            List ordered = (List)document.Blocks.FirstBlock;
            List bullets = (List)document.Blocks.LastBlock;
            Assert.AreEqual(TextMarkerStyle.Decimal, ordered.MarkerStyle);
            Assert.AreEqual(3, ordered.StartIndex);
            Assert.AreEqual(2, ordered.ListItems.Count);
            Assert.AreEqual(TextMarkerStyle.Disc, bullets.MarkerStyle);
            Assert.AreEqual(3, bullets.ListItems.Count);
        });

        [TestMethod]
        public void Code_blocks_keep_their_lines() => TestImages.Sta(() =>
        {
            FlowDocument document = this.Build("```cs\nint a = 1;\nint b = 2;\n```");

            Paragraph code = (Paragraph)document.Blocks.FirstBlock;
            Assert.AreEqual("int a = 1;\nint b = 2;", Text(code).Replace("\r\n", "\n"));
            Assert.AreEqual("Consolas", code.FontFamily.Source);
        });

        [TestMethod]
        public void Tables_become_tables_with_a_bold_header() => TestImages.Sta(() =>
        {
            FlowDocument document = this.Build("| Name | Value |\n|---|---|\n| retries | 3 |\n| delay | 2s |");

            Table table = (Table)document.Blocks.FirstBlock;
            TableRowCollection rows = table.RowGroups[0].Rows;
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(FontWeights.SemiBold, rows[0].Cells[0].FontWeight);
            Assert.AreEqual("delay", Text(rows[2].Cells[0].Blocks.FirstBlock));
        });

        [TestMethod]
        public void Large_images_are_decoded_at_the_preview_width() => TestImages.Sta(() =>
        {
            TestImages.Photo(Path.Combine(this.root, "Assets", "a-photo.png"), 2000, 1000);

            FlowDocument document = this.Build("![photo](Assets/a-photo.png)");

            BitmapSource source = (BitmapSource)Images(document).Single().Source;
            Assert.AreEqual((int)MarkdownDocument.MaxImageWidth, source.PixelWidth);
        });

        [TestMethod]
        public void Small_images_are_not_scaled_up() => TestImages.Sta(() =>
        {
            TestImages.Screenshot(Path.Combine(this.root, "Assets", "a-small chart(1).png"), 200, 100);

            FlowDocument document = this.Build("![chart](Assets/a-small%20chart%281%29.png)");

            Assert.AreEqual(200, ((BitmapSource)Images(document).Single().Source).PixelWidth);
        });

        [TestMethod]
        public void Missing_or_corrupt_images_show_their_alt_text() => TestImages.Sta(() =>
        {
            File.WriteAllBytes(Path.Combine(this.root, "Assets", "a-broken.png"), [1, 2, 3]);

            FlowDocument document = this.Build("![gone](Assets/a-gone.png) ![broken](Assets/a-broken.png)");

            Assert.AreEqual(0, Images(document).Count());
            StringAssert.Contains(Text(document.Blocks.FirstBlock), "[image not found: gone]");
            StringAssert.Contains(Text(document.Blocks.FirstBlock), "[image not found: broken]");
        });

        [TestMethod]
        [DataRow("![svg](Assets/a-diagram.svg)", "🖼 svg")]
        [DataRow("![remote](https://example.com/x.png)", "🖼 remote")]
        [DataRow("<video controls src=\"Assets/a-demo%20clip.mp4\"></video>", "▶ Video: a-demo clip.mp4")]
        [DataRow("<audio controls src=\"Assets/a-note.mp3\"></audio>", "▶ Audio: a-note.mp3")]
        public void Media_that_needs_a_browser_becomes_a_link(string markdown, string label) => TestImages.Sta(() =>
        {
            FlowDocument document = this.Build(markdown);

            Hyperlink link = Hyperlinks(document).Single();
            Assert.AreEqual(label, Text(link));
            Assert.AreEqual(0, Images(document).Count());
        });

        [TestMethod]
        public void Clicking_a_link_opens_the_url_or_the_local_file() => TestImages.Sta(() =>
        {
            List<string> opened = [];
            FlowDocument document = MarkdownDocument.Build(
                "[wiki](https://example.com/wiki) [spec](Assets/a-spec.pdf) [bad](javascript:alert(1)) [escape](../../secret.txt)",
                this.root,
                opened.Add);

            foreach (Hyperlink link in Hyperlinks(document))
            {
                link.DoClick();
            }

            CollectionAssert.AreEqual(new[] { "https://example.com/wiki", Path.Combine(this.root, "Assets", "a-spec.pdf") }, opened);
        });

        [TestMethod]
        [DataRow("Assets/a-x.png", true)]
        [DataRow("Assets/a-chart%281%29.png", true)]
        [DataRow("../outside.png", false)]
        [DataRow("Assets/../../outside.png", false)]
        [DataRow("C:/Windows/win.ini", false)]
        [DataRow("/etc/passwd", false)]
        [DataRow("https://example.com/x.png", false)]
        public void Local_paths_never_escape_the_root(string url, bool allowed)
        {
            string? path = MarkdownDocument.LocalPath(this.root, url);

            Assert.AreEqual(allowed, path is not null, path);
            if (path is not null)
            {
                StringAssert.StartsWith(path, Path.GetFullPath(this.root) + Path.DirectorySeparatorChar);
            }
        }

        [TestMethod]
        public void Other_html_is_shown_as_text() => TestImages.Sta(() =>
        {
            FlowDocument document = this.Build("<div>Kept &amp; decoded</div>");

            Assert.AreEqual("Kept & decoded", Text(document.Blocks.FirstBlock));
        });

        private static string Text(TextElement element) => new TextRange(element.ContentStart, element.ContentEnd).Text;

        private static Inline Find(IEnumerable<Inline> inlines, string text) => inlines.Single(i => Text(i) == text);

        private static IEnumerable<Hyperlink> Hyperlinks(FlowDocument document) => Descendants(document).OfType<Hyperlink>();

        private static IEnumerable<Image> Images(FlowDocument document) =>
            Descendants(document).OfType<InlineUIContainer>().Select(c => c.Child).OfType<Image>();

        private static IEnumerable<object> Descendants(object node)
        {
            foreach (object child in LogicalTreeHelper.GetChildren((DependencyObject)node))
            {
                yield return child;
                if (child is DependencyObject)
                {
                    foreach (object descendant in Descendants(child))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        private FlowDocument Build(string markdown) => MarkdownDocument.Build(markdown, this.root, _ => { });
    }
}
