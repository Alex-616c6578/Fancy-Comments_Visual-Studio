using System.IO;
using System.Text.RegularExpressions;

namespace FancyComments.Model
{
    /// <summary>
    /// Finds which comments are still used: every id with a marker in a source file under the solution folder.
    /// </summary>
    internal static class CommentScanner
    {
        // Build output, dependencies and tool folders never hold markers, and can be huge.
        private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", "bin", "obj", "node_modules", "packages",
        };

        private const long MaxFileSize = 10 * 1024 * 1024;

        /// <summary>
        /// Ids of every marker in files under <see cref="CommentStore.SolutionDir"/> that can hold one (Appendix A of
        /// the format spec). Throws if a folder or file can't be read: callers must then treat nothing as unused.
        /// </summary>
        public static HashSet<string> FindUsedIds(CommentStore store)
        {
            HashSet<string> ids = new HashSet<string>();
            Stack<string> pending = new Stack<string>();
            pending.Push(store.SolutionDir);
            while (pending.Count > 0)
            {
                string dir = pending.Pop();
                foreach (DirectoryInfo sub in new DirectoryInfo(dir).EnumerateDirectories())
                {
                    // Skip links, which can loop back on themselves, and the comment store itself.
                    if (!SkippedFolders.Contains(sub.Name)
                        && !sub.Attributes.HasFlag(FileAttributes.ReparsePoint)
                        && !string.Equals(sub.FullName, store.Root, StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Push(sub.FullName);
                    }
                }

                foreach (FileInfo file in new DirectoryInfo(dir).EnumerateFiles())
                {
                    if (CommentLanguages.For(file.FullName).Length == 0 || file.Length > MaxFileSize)
                    {
                        continue;
                    }

                    string text = File.ReadAllText(file.FullName);
                    if (!text.Contains("[fc:"))
                    {
                        continue;
                    }

                    foreach (Match match in CommentMarker.Pattern.Matches(text))
                    {
                        ids.Add(match.Groups["id"].Value);
                    }
                }
            }

            return ids;
        }
    }
}
