using System.IO;
using FancyComments.Model;
using FancyComments.UI;
using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using Document = EnvDTE.Document;
using DTE2 = EnvDTE80.DTE2;

namespace FancyComments
{
    /// <summary>
    /// Extensions → Fancy Comments → Clean Up Unused Fancy Comments: deletes comments whose marker no longer appears anywhere in the
    /// solution (e.g. the code around it was deleted), after listing them and asking. Manual on purpose: a branch
    /// switch can make a marker look deleted when it isn't.
    /// </summary>
    [VisualStudioContribution]
    internal class CleanupCommand : Command
    {
        private const int MaxListed = 15;

        /// <inheritdoc />
        /// <remarks>Placed in the Extensions → Fancy Comments menu (see <see cref="ExtensionEntrypoint.FancyCommentsMenu"/>).</remarks>
        public override CommandConfiguration CommandConfiguration => new("%FancyComments.CleanupCommand.DisplayName%");

        /// <inheritdoc />
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            DTE2? dte = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SDTE)) as DTE2;
            Assumes.Present(dte);

            string? solutionPath = dte.Solution?.FullName;
            if (string.IsNullOrEmpty(solutionPath) || CommentStore.ForDocument(solutionPath!) is not { } store)
            {
                Prompt.Show("Open a solution first. The cleanup scans the solution's folder for comments that are still used.");
                return;
            }

            // Unsaved edits aren't on disk, so a marker that was just pasted would look unused.
            bool unsaved = false;
            foreach (Document document in dte.Documents)
            {
                unsaved |= !document.Saved;
            }

            if (unsaved)
            {
                if (!Prompt.Confirm("Some files have unsaved changes. Unsaved comments would look unused, so everything needs saving first.\n\nSave all files and continue?"))
                {
                    return;
                }

                dte.Documents.SaveAll();
            }

            await TaskScheduler.Default;
            HashSet<string> used;
            try
            {
                used = CommentScanner.FindUsedIds(store);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                Prompt.Show($"Couldn't read part of the solution, so nothing was deleted:\n\n{e.Message}");
                return;
            }

            List<string> unused = store.AllIds().Where(id => !used.Contains(id)).OrderBy(id => id).ToList();
            int missing = used.Count(id => !store.Exists(id));
            string missingNote = missing == 0 ? "" : $"\n\n{missing} comment(s) in the code have missing data. Those are shown with a warning in the editor; restore their files from git.";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (unused.Count == 0)
            {
                Prompt.Show("No unused comments found. Every comment in FancyComments/Comments is still used in the code." + missingNote);
                return;
            }

            string listed = string.Join("\n", unused.Take(MaxListed).Select(id => "• " + Describe(store, id)));
            string more = unused.Count > MaxListed ? $"\n…and {unused.Count - MaxListed} more" : "";
            string message = $"Delete {unused.Count} unused Fancy Comment(s)?\n\n"
                + $"Their markers no longer appear anywhere in this solution:\n\n{listed}{more}\n\n"
                + "This deletes their JSON files and any media no other comment uses. If a comment is still used on another "
                + "branch, deleting it here will make it show as missing data there after a merge."
                + missingNote;
            if (!Prompt.Confirm(message))
            {
                return;
            }

            await TaskScheduler.Default;
            int assetsBefore = store.AssetCount();
            foreach (string id in unused)
            {
                store.Delete(id);
            }

            int assetsDeleted = assetsBefore - store.AssetCount();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Prompt.Show($"Deleted {unused.Count} comment(s) and {assetsDeleted} media file(s).");
        }

        /// <summary>"1a2b3c4d  Retry uses exponential backoff  (src/Login.cs)", or the id alone if the JSON can't be read.</summary>
        private static string Describe(CommentStore store, string id)
        {
            try
            {
                return store.Load(id) is { } comment ? $"{id}  {comment.Summary}  ({comment.File})" : id;
            }
            catch (JsonException)
            {
                return $"{id}  (unreadable)";
            }
        }
    }
}
