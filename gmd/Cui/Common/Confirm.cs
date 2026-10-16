namespace gmd.Cui.Common;

// The questions asked before a command that throws work away with no way to get it back. They are
// here rather than beside each command because the log view's menus and the diff view's undo menu
// reach the same git commands by paths of their own, and the question should read the same on both.
//
// No is the default throughout. These commands are chosen from a menu, where the slip to guard
// against is an Enter on the wrong item, and an Enter on the question would only repeat it. What
// they throw away gmd keeps, for Restore Discarded Changes (Server.DiscardedChanges), which the
// questions say, and the status line once it is done.
static class Confirm
{
    const int MaxListedPaths = 10;

    const string WayBack = "Restore Discarded Changes, in the Undo menu, brings it back.";

    // What the status line says once something is discarded
    internal static string Discarded(string what) =>
        $"Discarded {what}: Restore Discarded Changes in the Undo menu brings it back";

    internal static bool UndoAllUncommitted() =>
        Ask(
            "Discard All Changes",
            """
            Discard all uncommitted changes?

            Every changed file is reset to the last commit,
            and every new file is deleted.
            Restore Discarded Changes, in the Undo menu,
            brings them back.
            """
        );

    internal static bool UndoFile(string path, bool isNew) =>
        isNew
            ? Ask(
                "Discard Changes in a File",
                $"""
                Delete the new file?

                  {path}

                {WayBack}
                """
            )
            : Ask(
                "Discard Changes in a File",
                $"""
                Discard the changes to the file?

                  {path}

                It is reset to the last commit.
                {WayBack}
                """
            );

    // Some lines of a file, 'what' saying which, e.g. "this change in a.txt", 'lines' the lines with
    // '-' or '+' before them. The rest of the file's changes stay, which is the difference from
    // discarding the file. All the lines of a new file are the file, which has never been committed.
    internal static bool DiscardLines(string what, IReadOnlyList<string> lines, bool isNewFileWhole)
    {
        var listed = lines.Take(MaxListedPaths).Select(l => $"  {l.Max(60)}");
        string[] more = lines.Count > MaxListedPaths ? [$"  ... and {lines.Count - MaxListedPaths} more lines"] : [];
        var after = isNewFileWhole
            ? $"These are all the lines of a new file, which is deleted.\n{WayBack}"
            : $"Removed lines are put back, added lines taken out, and the\nfile's other changes stay as they are.\n{WayBack}";

        return Ask("Discard Lines", $"Discard {what}?\n\n{string.Join("\n", listed.Concat(more))}\n\n{after}");
    }

    internal static bool UndoFiles(IReadOnlyList<string> paths)
    {
        var listed = paths.Take(MaxListedPaths).Select(p => $"  {p}");
        string[] more = paths.Count > MaxListedPaths ? [$"  ... and {paths.Count - MaxListedPaths} more"] : [];

        return Ask(
            "Discard Changes in Files",
            $"Discard the changes to {Files(paths.Count)}?\n\n"
                + string.Join("\n", listed.Concat(more))
                + $"\n\nNew files are deleted, the others reset to the last commit.\n{WayBack}"
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
                + "\n\nThe changes and the new files can be brought back with Restore\n"
                + "Discarded Changes, in the Undo menu, but none of the ignored\n"
                + "files, which git never had."
        );
    }

    internal static bool DropStash(string message) =>
        Ask(
            "Drop Stash",
            $"""
            Drop the stash?

              {message}

            The changes in it are deleted.
            {WayBack}
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

    // A rebase of a branch on origin is pushed with force, which replaces origin's copy for everyone,
    // as Force Push and Restore origin do, and both ask first
    internal static bool RebaseAndPush(string branchName, string ontoName, string remoteName) =>
        Ask(
            "Rebase and Push",
            $"""
            Rebase '{branchName}' onto '{ontoName}', and push it with force?

            The rebase rewrites the commits of '{branchName}', and the push
            replaces {remoteName} with them, for everyone who has it.
            """
        );

    // Putting a remote branch back as it was before a force push, which is one in turn, for everyone
    internal static bool RestoreOrigin(string remoteName, string question) => Ask($"Restore {remoteName}", question);

    static string Files(int count) => count == 1 ? "1 file" : $"{count} files";

    static bool Ask(string title, string message) => UI.InfoMessage(title, message, 1, ["Yes", "No"]) == 0;
}
