using FancyComments.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyComments.Tests
{
    [TestClass]
    public class MediaEmbedTests
    {
        [TestMethod]
        [DataRow("Assets/a-x.png", "![x](Assets/a-x.png)")]
        [DataRow("Assets/a-x.JPEG", "![x](Assets/a-x.JPEG)")]
        [DataRow("Assets/a-x.svg", "![x](Assets/a-x.svg)")]
        [DataRow("Assets/a-x.mp4", "<video controls src=\"Assets/a-x.mp4\"></video>")]
        [DataRow("Assets/a-x.webm", "<video controls src=\"Assets/a-x.webm\"></video>")]
        [DataRow("Assets/a-x.mp3", "<audio controls src=\"Assets/a-x.mp3\"></audio>")]
        [DataRow("Assets/a-x.m4a", "<audio controls src=\"Assets/a-x.m4a\"></audio>")]
        [DataRow("Assets/a-x.pdf", "[x](Assets/a-x.pdf)")]
        [DataRow("Assets/a-x.zip", "[x](Assets/a-x.zip)")]
        public void Each_kind_of_media_gets_markup_the_preview_can_show(string path, string expected)
        {
            Assert.AreEqual(expected, MediaEmbed.Markdown("x", path));
        }

        [TestMethod]
        public void Parentheses_are_escaped_so_the_link_isnt_cut_short()
        {
            Assert.AreEqual("![x](Assets/a-chart%281%29.png)", MediaEmbed.Markdown("x", "Assets/a-chart(1).png"));
        }
    }
}
