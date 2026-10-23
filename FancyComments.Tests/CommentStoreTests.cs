using System.IO;
using System.Text;
using FancyComments.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FancyComments.Tests
{
    [TestClass]
    public class CommentStoreTests
    {
        [TestMethod]
        [DataRow("Test.sln")]
        [DataRow("Test.slnx")]
        [DataRow(@"FancyComments\Comments\")]
        [DataRow(@".git\")]
        public void The_store_lives_next_to_the_nearest_solution_or_repository(string marker)
        {
            string root = Path.Combine(Path.GetTempPath(), "fc-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "src", "deep"));
                if (marker.EndsWith(@"\"))
                {
                    Directory.CreateDirectory(Path.Combine(root, marker));
                }
                else
                {
                    File.WriteAllText(Path.Combine(root, marker), "");
                }

                CommentStore? store = CommentStore.ForDocument(Path.Combine(root, "src", "deep", "Code.cs"));

                Assert.AreEqual(Path.Combine(root, "FancyComments"), store?.Root);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void The_nearest_solution_wins()
        {
            using TempSolution solution = new TempSolution();
            solution.Write(@"inner\Inner.sln", "");

            CommentStore? store = CommentStore.ForDocument(solution.Path("inner", "Code.cs"));

            Assert.AreEqual(solution.Path("inner", "FancyComments"), store?.Root);
        }

        [TestMethod]
        public void Paths_are_stored_relative_with_forward_slashes()
        {
            using TempSolution solution = new TempSolution();

            Assert.AreEqual("src/Login/Login.cs", solution.Store.RelativePath(solution.Path("src", "Login", "Login.cs")));
        }

        [TestMethod]
        public void Saved_comments_round_trip_as_camel_case_json()
        {
            using TempSolution solution = new TempSolution();
            FancyComment comment = new FancyComment { Id = "1a2b3c4d", Description = "Why", Summary = "Why", Markdown = "# Body", File = "src/Code.cs", Line = 12 };

            solution.Store.Save(comment);

            JObject json = JObject.Parse(File.ReadAllText(solution.Path("FancyComments", "Comments", "1a2b3c4d.json")));
            Assert.AreEqual("Why", (string?)json["description"]);
            Assert.AreEqual(12, (int?)json["line"]);
            Assert.IsTrue(solution.Store.Exists("1a2b3c4d"));
            Assert.AreEqual("# Body", solution.Store.Load("1a2b3c4d")?.Markdown);
        }

        [TestMethod]
        public void Saving_rebuilds_the_asset_list_from_the_markdown()
        {
            // Regression: the old list used to be carried over, so removed media stayed "in use" forever.
            using TempSolution solution = new TempSolution();
            FancyComment comment = new FancyComment
            {
                Id = "1a2b3c4d",
                Markdown = "![kept](Assets/1a2b3c4d-kept.png)",
                Assets = ["Assets/1a2b3c4d-kept.png", "Assets/1a2b3c4d-removed.png"],
            };

            solution.Store.Save(comment);

            CollectionAssert.AreEqual(new[] { "Assets/1a2b3c4d-kept.png" }, solution.Store.Load("1a2b3c4d")!.Assets);
        }

        [TestMethod]
        public void Media_removed_from_a_comment_is_deleted_on_save()
        {
            using TempSolution solution = new TempSolution();
            solution.AddAsset("1a2b3c4d-kept.png");
            solution.AddAsset("1a2b3c4d-removed.png");
            // As saved by the extension: both files linked in the markdown and listed in "assets".
            solution.AddComment(
                "1a2b3c4d",
                "![a](Assets/1a2b3c4d-kept.png) ![b](Assets/1a2b3c4d-removed.png)",
                "Assets/1a2b3c4d-kept.png",
                "Assets/1a2b3c4d-removed.png");
            FancyComment comment = solution.Store.Load("1a2b3c4d")!;

            comment.Markdown = "![a](Assets/1a2b3c4d-kept.png)";
            solution.Store.Save(comment);

            CollectionAssert.AreEqual(new[] { "1a2b3c4d-kept.png" }, solution.Assets());
        }

        [TestMethod]
        public void Cleanup_only_deletes_unused_files_whose_owner_is_known()
        {
            using TempSolution solution = new TempSolution();
            solution.AddComment("aaaaaaaa", "![x](Assets/aaaaaaaa-used.png)");
            solution.AddComment("bbbbbbbb", "no media");
            solution.AddAsset("aaaaaaaa-used.png");      // referenced: kept
            solution.AddAsset("bbbbbbbb-orphan.png");    // owner b exists, nothing links it: deleted
            solution.AddAsset("cccccccc-lost.png");      // owner c's JSON is missing, maybe damage: kept
            solution.AddAsset("notes.txt");              // not named by Fancy Comments: kept

            solution.Store.DeleteUnusedAssets("bbbbbbbb");

            CollectionAssert.AreEqual(new[] { "aaaaaaaa-used.png", "cccccccc-lost.png", "notes.txt" }, solution.Assets());
        }

        [TestMethod]
        public void Cleanup_deletes_the_current_comments_files_even_without_its_json()
        {
            // A cancelled new comment never got a JSON file, but its inserted media should still go.
            using TempSolution solution = new TempSolution();
            solution.AddAsset("dddddddd-cancelled.png");

            solution.Store.DeleteUnusedAssets("dddddddd");

            Assert.AreEqual(0, solution.Assets().Length);
        }

        [TestMethod]
        public void Cleanup_deletes_nothing_if_any_json_is_unreadable()
        {
            using TempSolution solution = new TempSolution();
            solution.AddComment("aaaaaaaa", "no media");
            solution.Write(@"FancyComments\Comments\bbbbbbbb.json", "{ this is not json");
            solution.AddAsset("aaaaaaaa-orphan.png");

            solution.Store.DeleteUnusedAssets("aaaaaaaa");

            CollectionAssert.AreEqual(new[] { "aaaaaaaa-orphan.png" }, solution.Assets());
        }

        [TestMethod]
        [DataRow("![x](Assets/aaaaaaaa-my%20file.png)", "aaaaaaaa-my file.png")]
        [DataRow("![x](Assets/aaaaaaaa-chart%281%29.png)", "aaaaaaaa-chart(1).png")]
        [DataRow("<video controls src=\"Assets/aaaaaaaa-clip.mp4\"></video>", "aaaaaaaa-clip.mp4")]
        [DataRow("<audio controls src='Assets/aaaaaaaa-voice.mp3'></audio>", "aaaaaaaa-voice.mp3")]
        [DataRow("[spec](Assets/aaaaaaaa-spec.pdf)", "aaaaaaaa-spec.pdf")]
        public void Every_way_of_linking_media_counts_as_in_use(string markdown, string file)
        {
            using TempSolution solution = new TempSolution();
            solution.AddComment("aaaaaaaa", markdown);
            solution.AddAsset(file);

            solution.Store.DeleteUnusedAssets("aaaaaaaa");

            CollectionAssert.AreEqual(new[] { file }, solution.Assets());
        }

        [TestMethod]
        public void Media_listed_only_in_hand_edited_json_counts_as_in_use()
        {
            using TempSolution solution = new TempSolution();
            solution.AddComment("aaaaaaaa", "no links", "Assets/aaaaaaaa-listed.png");
            solution.AddAsset("aaaaaaaa-listed.png");

            solution.Store.DeleteUnusedAssets("aaaaaaaa");

            CollectionAssert.AreEqual(new[] { "aaaaaaaa-listed.png" }, solution.Assets());
        }

        [TestMethod]
        public void Deleting_a_comment_deletes_its_json_and_its_unshared_media()
        {
            using TempSolution solution = new TempSolution();
            solution.AddComment("aaaaaaaa", "![x](Assets/aaaaaaaa-own.png) ![y](Assets/aaaaaaaa-shared.png)");
            solution.AddComment("bbbbbbbb", "![y](Assets/aaaaaaaa-shared.png)");
            solution.AddAsset("aaaaaaaa-own.png");
            solution.AddAsset("aaaaaaaa-shared.png");

            solution.Store.Delete("aaaaaaaa");

            Assert.IsFalse(solution.Store.Exists("aaaaaaaa"));
            CollectionAssert.AreEqual(new[] { "aaaaaaaa-shared.png" }, solution.Assets());
        }

        [TestMethod]
        public void Deleting_works_when_the_comments_folder_is_already_gone()
        {
            using TempSolution solution = new TempSolution();
            solution.AddAsset("aaaaaaaa-x.png");

            solution.Store.Delete("aaaaaaaa");

            Assert.AreEqual(0, solution.Assets().Length);
        }

        [TestMethod]
        public void New_media_is_named_after_its_comment()
        {
            using TempSolution solution = new TempSolution();

            string path = solution.Store.AddAsset("1a2b3c4d", Asset("my diagram.png", "png bytes"));

            Assert.AreEqual("Assets/1a2b3c4d-my-diagram.png", path);
            Assert.IsTrue(File.Exists(solution.Path("FancyComments", "Assets", "1a2b3c4d-my-diagram.png")));
        }

        [TestMethod]
        public void Different_files_with_the_same_name_get_numbered()
        {
            using TempSolution solution = new TempSolution();

            string first = solution.Store.AddAsset("1a2b3c4d", Asset("chart.png", "one"));
            string second = solution.Store.AddAsset("1a2b3c4d", Asset("chart.png", "two"));

            Assert.AreEqual("Assets/1a2b3c4d-chart.png", first);
            Assert.AreEqual("Assets/1a2b3c4d-chart-2.png", second);
        }

        [TestMethod]
        public void Identical_files_are_stored_once_even_across_comments_and_names()
        {
            using TempSolution solution = new TempSolution();

            string first = solution.Store.AddAsset("aaaaaaaa", Asset("logo.png", "same bytes"));
            string second = solution.Store.AddAsset("bbbbbbbb", Asset("copy of logo.png", "same bytes"));

            Assert.AreEqual(first, second);
            Assert.AreEqual(1, solution.Assets().Length);
        }

        [TestMethod]
        public void Duplicating_a_comment_gives_it_a_new_id_and_shares_its_media()
        {
            using TempSolution solution = new TempSolution();
            solution.AddAsset("aaaaaaaa-x.png");
            solution.AddComment("aaaaaaaa", "![x](Assets/aaaaaaaa-x.png)");
            FancyComment original = solution.Store.Load("aaaaaaaa")!;

            FancyComment copy = solution.Store.Duplicate(original, "src/Other.cs", 7);

            Assert.AreNotEqual("aaaaaaaa", copy.Id);
            Assert.IsTrue(solution.Store.Exists(copy.Id));
            Assert.IsTrue(solution.Store.Exists("aaaaaaaa"));
            Assert.AreEqual(original.Markdown, copy.Markdown);
            Assert.AreEqual("src/Other.cs", copy.File);
            Assert.AreEqual(7, copy.Line);
            CollectionAssert.AreEqual(new[] { "aaaaaaaa-x.png" }, solution.Assets());
        }

        [TestMethod]
        public void All_ids_lists_every_comment_file()
        {
            using TempSolution solution = new TempSolution();
            solution.AddComment("aaaaaaaa");
            solution.Write(@"FancyComments\Comments\bbbbbbbb.json", "unreadable");

            CollectionAssert.AreEquivalent(new[] { "aaaaaaaa", "bbbbbbbb" }, solution.Store.AllIds().ToArray());
        }

        private static PreparedAsset Asset(string fileName, string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            return new PreparedAsset { FileName = fileName, Bytes = bytes, OriginalSize = bytes.Length };
        }
    }
}
