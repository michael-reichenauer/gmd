namespace gmd.Cui.Common;

// The questions asked before a command that throws work away with no way to get it back. They are
// here rather than beside each command because the log view's menus and the diff view's undo menu
// reach the same git commands by paths of their own, and the question should read the same on both.
//
// No is the default throughout. These commands are chosen from a menu, where the slip to guard
// against is an Enter on the wrong item, and an Enter on the question would only repeat it.
static class Confirm
{
    const int MaxListedPaths = 10;

    internal static bool UndoAllUncommitted() =>
        Ask(
            "Discard All Changes",
            """
            Discard all uncommitted changes?

            Every changed file is reset to the last commit,
            and every new file is deleted.
            This cannot be undone.
            """
        );

    internal static bool UndoFile(string path, bool isNew) =>
        isNew
            ? Ask(
                "Discard Changes in a File",
                $"""
                Delete the new file?

                  {path}

                It has never been committed, so it cannot be restored.
                """
            )
            : Ask(
                "Discard Changes in a File",
                $"""
                Discard the changes to the file?

                  {path}

                It is reset to the last commit. This cannot be undone.
                """
            );

    internal static bool UndoFiles(IReadOnlyList<string> paths)
    {
        var listed = paths.Take(MaxListedPaths).Select(p => $"  {p}");
        string[] more = paths.Count > MaxListedPaths ? [$"  ... and {paths.Count - MaxListedPaths} more"] : [];

        return Ask(
            "Discard Changes in Files",
            $"Discard the changes to {Files(paths.Count)}?\n\n"
                + string.Join("\n", listed.Concat(more))
                + "\n\nNew files are deleted, the others reset to the last commit.\nThis cannot be undone."
        );
    }

    // The folder as a fresh clone would have it, which is what this is for: no leftover file that
    // git ignores can then make a build or a test pass that would fail elsewhere. The ignored files
    // are also the ones a user may mean to keep, e.g. a .env file of secrets, and the ones least in
    // view, so the question lists what goes rather than only naming the kinds.
    internal static bool CleanWorkingFolder(IReadOnlyList<string> toDelete)
    {
        var listed = toDelete.Take(MaxListedPaths).Select(p => $"  {p}");
        string[] more = toDelete.Count > MaxListedPaths ? [$"  ... and {toDelete.Count - MaxListedPaths} more"] : [];
        var deleted =
            toDelete.Count == 0
                ? "There is no such file here now."
                : "These are deleted:\n\n" + string.Join("\n", listed.Concat(more));

        return Ask(
            "Discard All Changes and Ignored Files",
            "Make the working folder as a fresh clone of the last commit?\n\n"
                + "Every uncommitted change is undone, and every file git does\n"
                + "not track is deleted, the files it ignores too: secrets such\n"
                + "as .env files, local settings, dependencies, build output.\n\n"
                + deleted
                + "\n\nNone of it can be brought back, not by Undo, nor by Recover\n"
                + "Lost Commits, since git never had it."
        );
    }

    internal static bool DropStash(string message) =>
        Ask(
            "Drop Stash",
            $"""
            Drop the stash?

              {message}

            The changes in it are deleted. This cannot be undone.
            """
        );

    internal static bool RemoveTag(string name, bool isOnRemote) =>
        Ask(
            "Remove Tag",
            isOnRemote
                ? $"Remove the tag '{name}'?\n\nIt is deleted on origin as well, for everyone."
                : $"Remove the tag '{name}'?"
        );

    // Undo brings a dropped commit back, but a slip of the finger should not need it: the commit's
    // changes leave the branch with it, and later commits are rewritten
    internal static bool DropCommit(string sid, string subject, string branchName) =>
        Ask(
            "Drop Commit",
            $"""
            Drop the commit from '{branchName}'?

              {sid} {subject}

            Its changes leave the branch with it, and the commits
            after it are rewritten without it. Undo brings it back.
            """
        );

    // Putting a remote branch back as it was before a force push, which is one in turn, for everyone
    internal static bool RestoreOrigin(string remoteName, string question) => Ask($"Restore {remoteName}", question);

    static string Files(int count) => count == 1 ? "1 file" : $"{count} files";

    static bool Ask(string title, string message) => UI.InfoMessage(title, message, 1, ["Yes", "No"]) == 0;
}
