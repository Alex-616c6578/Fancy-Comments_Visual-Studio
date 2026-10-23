using System.Text.RegularExpressions;
using FancyComments.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyComments.Tests
{
    [TestClass]
    public class CommentMarkerTests
    {
        [TestMethod]
        [DataRow("/*", "*/")]
        [DataRow("//", "")]
        [DataRow("#", "")]
        [DataRow("'", "")]
        [DataRow("--", "")]
        [DataRow("::", "")]
        [DataRow("<!--", "-->")]
        [DataRow("@*", "*@")]
        [DataRow("(*", "*)")]
        public void Markers_written_in_every_comment_syntax_read_back(string open, string close)
        {
            string line = "    " + TempSolution.Marker("1a2b3c4d", "Retry uses exponential backoff", open, close);

            Match match = CommentMarker.Pattern.Match(line);

            Assert.IsTrue(match.Success, line);
            Assert.AreEqual("1a2b3c4d", match.Groups["id"].Value);
            Assert.AreEqual("Retry uses exponential backoff", match.Groups["summary"].Value);
            Assert.IsTrue(match.Groups["body"].Value.StartsWith("[fc:1a2b3c4d"));
        }

        [TestMethod]
        public void Hash_comments_get_a_space_so_banners_dont_trigger()
        {
            CommentSyntax syntax = new CommentSyntax("#");

            Assert.AreEqual("# ##", syntax.Trigger);
            StringAssert.StartsWith(syntax.Format(CommentMarker.Tag("1a2b3c4d"), "s"), "# ## [fc:1a2b3c4d");
        }

        [TestMethod]
        public void Ordinary_comments_that_use_hashes_are_not_markers()
        {
            Assert.IsFalse(CommentMarker.Pattern.IsMatch("/* ## not a fancy comment ## */"));
            Assert.IsFalse(CommentMarker.Pattern.IsMatch("##### Section #####"));
            Assert.IsFalse(CommentMarker.Pattern.IsMatch("/*## [fc:XYZ12345 bad id] summary ##*/"));
        }

        [TestMethod]
        public void A_line_can_hold_several_markers_after_code()
        {
            string line = "Call(); " + TempSolution.Marker("aaaaaaaa", "first") + " " + TempSolution.Marker("bbbbbbbb", "second");

            IEnumerable<string> ids = CommentMarker.Pattern.Matches(line).Cast<Match>().Select(m => m.Groups["id"].Value);

            CollectionAssert.AreEqual(new[] { "aaaaaaaa", "bbbbbbbb" }, ids.ToArray());
        }

        [TestMethod]
        public void Tag_tells_readers_without_the_extension_where_the_comment_is()
        {
            StringAssert.Contains(CommentMarker.Tag("1a2b3c4d"), "FancyComments/Comments/1a2b3c4d.json");
        }

        [TestMethod]
        public void New_ids_are_8_lowercase_hex_characters()
        {
            for (int i = 0; i < 100; i++)
            {
                StringAssert.Matches(CommentMarker.NewId(), new System.Text.RegularExpressions.Regex("^[0-9a-f]{8}$"));
            }
        }

        [TestMethod]
        public void Summary_is_the_description_when_there_is_one()
        {
            Assert.AreEqual("Why retries are capped", CommentMarker.Summarize("  Why retries are capped  ", "# Something else"));
        }

        [TestMethod]
        public void Summary_never_contains_the_marker_delimiter()
        {
            Assert.AreEqual("Uses C# #regions", CommentMarker.Summarize("Uses C## ##regions", ""));
        }

        [TestMethod]
        [DataRow("### Heading\nbody", "Heading")]
        [DataRow("\n\n  > quoted text", "quoted text")]
        [DataRow("<h3>this is a fancy comment </h3>\n![x](Assets/x.png)", "this is a fancy comment")]
        [DataRow("![stonecold](Assets/245d2313-stonecold.JPEG)", "stonecold")]
        [DataRow("See [the wiki](https://wiki/page) first", "See the wiki first")]
        [DataRow("", "(empty comment)")]
        [DataRow("   \n  ", "(empty comment)")]
        public void Without_a_description_the_summary_is_the_markdowns_first_line(string markdown, string expected)
        {
            Assert.AreEqual(expected, CommentMarker.Summarize("", markdown));
        }

        [TestMethod]
        [DataRow("# Retry policy\n\nWe use **exponential** backoff with `jitter`.", "Retry policy\nWe use exponential backoff with jitter.")]
        [DataRow("See [the wiki](https://wiki/page) and ![the diagram](Assets/a-x.png).", "See the wiki and the diagram.")]
        [DataRow("- one\n- two\n\n\n\nafter", "one\ntwo\nafter")]
        [DataRow("<h3>this is a fancy comment </h3>\n\nbody", "this is a fancy comment\nbody")]
        [DataRow("<video controls src=\"Assets/a.mp4\"></video>\n\nAfter video.", "After video.")]
        [DataRow("<video controls src=\"Assets/a.mp4\"></video>", "")]
        [DataRow("", "")]
        public void Excerpts_are_the_markdowns_plain_text(string markdown, string expected)
        {
            Assert.AreEqual(expected, CommentMarker.Excerpt(markdown));
        }

        [TestMethod]
        public void Long_excerpts_are_cut_at_a_word_with_an_ellipsis()
        {
            string markdown = string.Join(" ", Enumerable.Repeat("backoff", 30));

            string excerpt = CommentMarker.Excerpt(markdown);

            Assert.IsTrue(excerpt.Length is > 90 and <= 100, excerpt.Length.ToString());
            StringAssert.EndsWith(excerpt, "backoff…");
        }

        [TestMethod]
        public void A_single_long_word_is_cut_mid_word()
        {
            Assert.AreEqual(new string('a', 99) + "…", CommentMarker.Excerpt(new string('a', 300)));
        }

        [TestMethod]
        public void Excerpts_that_fit_are_left_whole()
        {
            string markdown = new string('a', 100);

            Assert.AreEqual(markdown, CommentMarker.Excerpt(markdown));
        }

        [TestMethod]
        public void Long_first_lines_are_cut_to_80_characters()
        {
            string summary = CommentMarker.Summarize("", new string('a', 200));

            Assert.AreEqual(80, summary.Length);
            Assert.IsTrue(summary.EndsWith("…"));
        }
    }
}
