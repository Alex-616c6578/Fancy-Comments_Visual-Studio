using FancyComments.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyComments.Tests
{
    [TestClass]
    public class CommentScannerTests
    {
        [TestMethod]
        public void Finds_markers_in_every_kind_of_source_file()
        {
            using TempSolution solution = new TempSolution();
            solution.Write(@"src\Login.cs", "class C {\n    " + TempSolution.Marker("aaaaaaaa") + "\n}");
            solution.Write(@"scripts\deploy.py", TempSolution.Marker("bbbbbbbb", open: "#", close: ""));
            solution.Write(@"web\Index.cshtml", TempSolution.Marker("cccccccc", open: "@*", close: "*@"));

            HashSet<string> used = CommentScanner.FindUsedIds(solution.Store);

            CollectionAssert.AreEquivalent(new[] { "aaaaaaaa", "bbbbbbbb", "cccccccc" }, used.ToArray());
        }

        [TestMethod]
        [DataRow(@"src\obj\Generated.cs")]
        [DataRow(@"src\bin\Debug\Copy.cs")]
        [DataRow(@".git\COMMIT.cs")]
        [DataRow(@".vs\cache.cs")]
        [DataRow(@"web\node_modules\lib\index.js")]
        [DataRow(@"packages\Some.Package\tool.cs")]
        [DataRow(@"FancyComments\Comments\notes.cs")]
        [DataRow(@"src\notes.txt")]
        public void Ignores_build_output_tools_and_files_that_cant_hold_comments(string path)
        {
            using TempSolution solution = new TempSolution();
            solution.Write(path, TempSolution.Marker("aaaaaaaa"));

            Assert.AreEqual(0, CommentScanner.FindUsedIds(solution.Store).Count);
        }

        [TestMethod]
        public void A_project_folder_named_FancyComments_is_still_scanned()
        {
            // Only the store itself is skipped, not any folder that happens to share its name.
            using TempSolution solution = new TempSolution();
            solution.Write(@"src\FancyComments\Code.cs", TempSolution.Marker("aaaaaaaa"));

            CollectionAssert.AreEqual(new[] { "aaaaaaaa" }, CommentScanner.FindUsedIds(solution.Store).ToArray());
        }
    }
}
