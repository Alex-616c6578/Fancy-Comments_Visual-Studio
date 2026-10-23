using FancyComments.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyComments.Tests
{
    [TestClass]
    public class CommentLanguagesTests
    {
        [TestMethod]
        [DataRow(@"C:\src\Login.cs", "/*##", "//##")]
        [DataRow(@"C:\src\LOGIN.CS", "/*##", "//##")]
        [DataRow(@"C:\src\app.ts", "/*##", "//##")]
        [DataRow(@"C:\src\site.css", "/*##")]
        [DataRow(@"C:\src\build.py", "# ##")]
        [DataRow(@"C:\src\Dockerfile", "# ##")]
        [DataRow(@"C:\src\Module.vb", "'##")]
        [DataRow(@"C:\src\query.sql", "/*##", "--##")]
        [DataRow(@"C:\src\View.xaml", "<!--##")]
        [DataRow(@"C:\src\Page.razor", "@*##", "<!--##")]
        [DataRow(@"C:\src\Script.fs", "(*##", "//##")]
        public void Each_file_type_has_its_own_triggers(string path, params string[] triggers)
        {
            CollectionAssert.AreEqual(triggers, CommentLanguages.For(path).Select(s => s.Trigger).ToArray());
        }

        [TestMethod]
        [DataRow(@"C:\src\notes.txt")]
        [DataRow(@"C:\src\data.json")]
        [DataRow(@"C:\src\noextension")]
        [DataRow(null)]
        public void Files_that_cant_hold_comments_have_no_triggers(string? path)
        {
            Assert.AreEqual(0, CommentLanguages.For(path).Length);
        }
    }
}
