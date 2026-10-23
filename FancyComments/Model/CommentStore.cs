using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FancyComments.Model
{
    /// <summary>
    /// A fancy comment as stored in FancyComments/Comments/{id}.json.
    /// </summary>
    internal sealed class FancyComment
    {
        public string Id { get; set; } = "";

        /// <summary>Source file relative to the solution folder, with forward slashes.</summary>
        public string File { get; set; } = "";

        /// <summary>1-based line. Only a hint: the id inside the source marker is the real link.</summary>
        public int Line { get; set; }

        /// <summary>Plain text the author wrote to be shown inline in the code.</summary>
        public string Description { get; set; } = "";

        /// <summary>What is actually written into the source marker: the description, or the markdown's first line if it's empty.</summary>
        public string Summary { get; set; } = "";

        public string Markdown { get; set; } = "";

        /// <summary>Paths relative to the FancyComments folder, e.g. "Assets/1a2b3c4d-diagram.png".</summary>
        public List<string> Assets { get; set; } = [];

        public DateTime Created { get; set; }

        public DateTime Modified { get; set; }
    }

    /// <summary>
    /// Reads and writes the FancyComments folder that sits next to the solution file.
    /// </summary>
    internal sealed class CommentStore
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        private static readonly Regex AssetReference = new(@"Assets/[^\s)""'>]+", RegexOptions.Compiled);

        // Asset files are named "{comment id}-{name}", so the id says which comment added them.
        private static readonly Regex AssetOwner = new(@"^([0-9a-f]{8})-", RegexOptions.Compiled);

        private CommentStore(string solutionDir)
        {
            this.SolutionDir = solutionDir;
        }

        public string SolutionDir { get; }

        public string Root => Path.Combine(this.SolutionDir, "FancyComments");

        public string CommentsDir => Path.Combine(this.Root, "Comments");

        public string AssetsDir => Path.Combine(this.Root, "Assets");

        /// <summary>
        /// Walks up from the document to the nearest folder with a .sln/.slnx, an existing FancyComments store,
        /// or a .git folder (for Open Folder mode). Null if the file isn't inside any of those.
        /// </summary>
        public static CommentStore? ForDocument(string documentPath)
        {
            for (string? dir = Path.GetDirectoryName(documentPath); dir is not null; dir = Path.GetDirectoryName(dir))
            {
                if (Directory.EnumerateFiles(dir, "*.sln").Any()
                    || Directory.EnumerateFiles(dir, "*.slnx").Any()
                    || Directory.Exists(Path.Combine(dir, "FancyComments", "Comments"))
                    || Directory.Exists(Path.Combine(dir, ".git")))
                {
                    return new CommentStore(dir);
                }
            }

            return null;
        }

        public string RelativePath(string fullPath)
        {
            string root = this.SolutionDir.TrimEnd('\\') + "\\";
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(root.Length).Replace('\\', '/')
                : fullPath;
        }

        public FancyComment? Load(string id)
        {
            string path = this.CommentPath(id);
            return File.Exists(path) ? JsonConvert.DeserializeObject<FancyComment>(File.ReadAllText(path), JsonSettings) : null;
        }

        /// <summary>True if the comment's JSON exists. A marker in the code without one means the data was lost or never committed.</summary>
        public bool Exists(string id) => File.Exists(this.CommentPath(id));

        /// <summary>Writes the comment's JSON, then deletes media in Assets that is no longer used.</summary>
        public void Save(FancyComment comment)
        {
            // Rebuilt from the markdown alone: carrying the old list over would keep removed media "in use" forever.
            comment.Assets = MarkdownReferences(comment.Markdown).ToList();
            Directory.CreateDirectory(this.CommentsDir);
            File.WriteAllText(this.CommentPath(comment.Id), JsonConvert.SerializeObject(comment, JsonSettings));
            this.DeleteUnusedAssets(comment.Id);
        }

        /// <summary>
        /// Saves a copy of a comment under a new id, for a marker that was copied. The copy links the same media
        /// files rather than duplicating them.
        /// </summary>
        public FancyComment Duplicate(FancyComment original, string file, int line)
        {
            string id = CommentMarker.NewId();
            while (this.Exists(id))
            {
                id = CommentMarker.NewId();
            }

            FancyComment copy = new FancyComment
            {
                Id = id,
                File = file,
                Line = line,
                Description = original.Description,
                Summary = original.Summary,
                Markdown = original.Markdown,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
            };
            this.Save(copy);
            return copy;
        }

        /// <summary>Ids of every comment with a JSON file, readable or not.</summary>
        public IEnumerable<string> AllIds() =>
            Directory.Exists(this.CommentsDir)
                ? Directory.EnumerateFiles(this.CommentsDir, "*.json").Select(Path.GetFileNameWithoutExtension)
                : [];

        public int AssetCount() => Directory.Exists(this.AssetsDir) ? Directory.EnumerateFiles(this.AssetsDir).Count() : 0;

        /// <summary>Deletes the comment's JSON, then media in Assets that is no longer used.</summary>
        public void Delete(string id)
        {
            if (this.Exists(id))
            {
                File.Delete(this.CommentPath(id));
            }

            this.DeleteUnusedAssets(id);
        }

        /// <summary>
        /// Deletes files in Assets that no saved comment references, but only those whose owning comment is known:
        /// its JSON exists, or it's <paramref name="currentId"/> (just saved, cancelled or deleted). A file whose
        /// comment's JSON is missing is kept, since that's more likely damage (deleted files, a branch switch, an
        /// incomplete commit) than a deliberate delete. If any JSON can't be read, nothing is deleted.
        /// </summary>
        public void DeleteUnusedAssets(string currentId)
        {
            if (!Directory.Exists(this.AssetsDir))
            {
                return;
            }

            HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentId };
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (Directory.Exists(this.CommentsDir))
                {
                    foreach (string path in Directory.EnumerateFiles(this.CommentsDir, "*.json"))
                    {
                        known.Add(Path.GetFileNameWithoutExtension(path));
                        if (JsonConvert.DeserializeObject<FancyComment>(File.ReadAllText(path), JsonSettings) is { } comment)
                        {
                            used.UnionWith(References(comment));
                        }
                    }
                }
            }
            catch (Exception e) when (e is JsonException or IOException)
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(this.AssetsDir))
            {
                string name = Path.GetFileName(file);
                Match owner = AssetOwner.Match(name);
                if (owner.Success && known.Contains(owner.Groups[1].Value) && !used.Contains("Assets/" + name))
                {
                    File.Delete(file);
                }
            }
        }

        /// <summary>
        /// Writes a prepared file into Assets and returns its path relative to the FancyComments folder.
        /// If Assets already holds an identical file (from any comment), links to that instead of writing a copy.
        /// </summary>
        public string AddAsset(string commentId, PreparedAsset asset)
        {
            Directory.CreateDirectory(this.AssetsDir);
            if (this.FindIdenticalAsset(asset.Bytes) is { } existing)
            {
                return "Assets/" + existing;
            }

            string name = Path.GetFileNameWithoutExtension(asset.FileName).Replace(' ', '-');
            string extension = Path.GetExtension(asset.FileName);
            string fileName = $"{commentId}-{name}{extension}";
            for (int i = 2; File.Exists(Path.Combine(this.AssetsDir, fileName)); i++)
            {
                fileName = $"{commentId}-{name}-{i}{extension}";
            }

            File.WriteAllBytes(Path.Combine(this.AssetsDir, fileName), asset.Bytes);
            return "Assets/" + fileName;
        }

        private string CommentPath(string id) => Path.Combine(this.CommentsDir, id + ".json");

        /// <summary>Assets a saved comment uses: those linked in its markdown, plus any listed in its JSON (it may have been edited by hand).</summary>
        private static IEnumerable<string> References(FancyComment comment) =>
            MarkdownReferences(comment.Markdown).Concat(comment.Assets).Distinct(StringComparer.OrdinalIgnoreCase);

        private static IEnumerable<string> MarkdownReferences(string markdown) =>
            AssetReference.Matches(markdown).Cast<Match>().Select(m => Uri.UnescapeDataString(m.Value)).Distinct(StringComparer.OrdinalIgnoreCase);

        private string? FindIdenticalAsset(byte[] bytes)
        {
            string? hash = null;
            foreach (FileInfo file in new DirectoryInfo(this.AssetsDir).EnumerateFiles())
            {
                // Only hash files that could match.
                if (file.Length == bytes.Length && (hash ??= Hash(bytes)) == Hash(File.ReadAllBytes(file.FullName)))
                {
                    return file.Name;
                }
            }

            return null;
        }

        private static string Hash(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }
    }
}
