namespace gmd.Cui.Common;

// The questions asked before a command that throws work away with no way to get it back. They are
// here rather than beside each command because the log view's menus and the diff view's undo menu
// reach the same git commands by paths of their own, and the question should read the same on both.
//
// No is the default throughout. These commands are chosen from a menu, where the slip to guard
// against is an Enter on the wrong item, and an Enter on the question would only repeat it. What
// they throw away gmd keeps, for Restore Discarded Changes (Server.DiscardedChanges), which the
// questions say, and the status line once it is done.
// What the question before a reset of the current branch was answered with: its commits' changes kept
// as uncommitted changes, or the files taken back to the commit too
enum ResetChoice
{
    Cancel,
    KeepChanges,
    DiscardChanges,
}

// What the question before a rewrite of commits, a drop or a rebase, was answered with
enum RewriteChoice
{
    Cancel,
    Rewrite,
    RewriteAndPush,
}

// What the question before Delete All in Delete Merged Branches was answered with: the branches
// deleted here only, those on origin left as they are, or the ones on origin deleted there too
enum MergedDeleteChoice
{
    Cancel,
    HereOnly,
    AlsoOnOrigin,
}

static class Confirm
{
    const int MaxListedPaths = 10;

    const string WayBack = "Restore Discarded Changes, in the Undo menu, brings it back.";

    // What the status line says once something is discarded
    internal static string Discarded(string what) =>
        $"Discarded {what}: Restore Discarded Changes in the Undo menu brings it back";

    // Discard All Changes, with every changed file listed, each with its letter (ChangedFileText), so
    // that what is discarded can be checked: the files git ignores are left alone, and deleted only by
    // Clean Working Folder, a command of its own (DeleteIgnored). Cancel is the default.
    internal static bool DiscardAll(IReadOnlyList<Text> files) =>
        ConfirmListDlg.Show(
            "Discard All Changes",
            $"Discard the uncommitted changes of {Count(files.Count, "file")}?",
            files,
            "Changed files are reset to the last commit, and new files deleted.\n"
                + "Restore Discarded Changes, in the Undo menu, brings them back.",
            1,
            "_Discard",
            "Cancel"
        ) == 0;

    // Clean Working Folder, in the repo menu: the files git ignores deleted, every one listed, since
    // they are also files a user may mean to keep, e.g. a .env file of secrets, the ones least in view,
    // and nothing brings them back. The uncommitted changes stay, so that this question never has to be
    // read for them too; with none, the folder is then as a fresh clone of the last commit has it, and
    // no leftover file can make a build or a test pass that would fail elsewhere. Cancel is the default.
    internal static bool DeleteIgnored(IReadOnlyList<string> ignored) =>
        ConfirmListDlg.Show(
            "Clean Working Folder",
            $"Delete the {Count(ignored.Count, "file or folder", "files and folders")} git ignores?\n"
                + "The uncommitted changes are kept.",
            ignored.Select(p => (Text)Text.White(p)).ToList(),
            "They may be meant to stay: secrets such as .env files, local settings,\n"
                + "dependencies, build output. Nothing brings them back.",
            1,
            "_Delete",
            "Cancel"
        ) == 0;

    static string Count(int count, string one, string? many = null) =>
        count == 1 ? $"1 {one}" : $"{count} {many ?? one + "s"}";

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
    // changes leave the branch with it, and later commits are rewritten. One on origin can be pushed
    // with force once dropped, which replaces it there too, the default when offered as ticked (see
    // ForcePush), and Cancel the default button throughout.
    internal static RewriteChoice DropCommit(string sid, string subject, string branchName, ForcePush forcePush)
    {
        var question = $"""
            Drop the commit from '{branchName}'?

              {sid} {subject}

            Its changes leave the branch with it, and the commits
            after it are rewritten without it. Undo brings it back.
            """;
        if (forcePush == ForcePush.NotOffered)
            return Ask("Drop Commit", question) ? RewriteChoice.Rewrite : RewriteChoice.Cancel;

        var choice = UI.InfoMessage(
            "Drop Commit",
            question
                + "\n\nIt is on origin: Drop and Push replaces it there too,\nwith force, for everyone who has the branch.",
            2,
            ["Drop and Push", "Drop", "Cancel"]
        );
        return choice switch
        {
            0 => RewriteChoice.RewriteAndPush,
            1 => RewriteChoice.Rewrite,
            _ => RewriteChoice.Cancel,
        };
    }

    // A rebase of a branch on origin is pushed with force too, when asked, which replaces origin's copy
    // for everyone, as Force Push and Restore origin do, and both ask first; or it is left to 'p'
    internal static RewriteChoice Rebase(string branchName, string ontoName, string remoteName, ForcePush forcePush)
    {
        if (forcePush == ForcePush.NotOffered)
            return RewriteChoice.Rewrite;

        var choice = UI.InfoMessage(
            "Rebase",
            $"""
            Rebase '{branchName}' onto '{ontoName}'?

            The rebase rewrites the commits of '{branchName}', which are on
            origin: Rebase and Push replaces {remoteName} with them, with force,
            for everyone who has it. Rebase leaves that to p.
            """,
            2,
            ["Rebase and Push", "Rebase", "Cancel"]
        );
        return choice switch
        {
            0 => RewriteChoice.RewriteAndPush,
            1 => RewriteChoice.Rewrite,
            _ => RewriteChoice.Cancel,
        };
    }

    // Rewriting commits on origin that others may have built on: the branch is shared, or some of them
    // are someone else's (CommitRewrite.PushedWarning)
    internal static bool RewritePushed(string question) => Ask("Rewrite Pushed Commits", question);

    // A reset of the current branch to a commit, whose commits after it leave the branch, Undo the way
    // back, with Cancel the default: what they changed kept as uncommitted changes ('reset --mixed'),
    // or the files taken back to the commit too ('reset --keep', which git refuses rather than
    // overwrite an uncommitted change). What it says of the commits is CommitCommands'.
    internal static ResetChoice ResetBranch(string branchName, string sid, string subject, string what)
    {
        var choice = UI.InfoMessage(
            "Reset Branch",
            $"Reset '{branchName}' to {sid}?\n\n  {sid} {subject}\n\n{what}\n\n"
                + "Keep Changes leaves what it takes off as uncommitted changes,\n"
                + "Discard Changes takes the files back to the commit too, where no\n"
                + "uncommitted change is in the way. Undo takes the reset back.",
            2,
            ["Keep Changes", "Discard Changes", "Cancel"]
        );
        return choice switch
        {
            0 => ResetChoice.KeepChanges,
            1 => ResetChoice.DiscardChanges,
            _ => ResetChoice.Cancel,
        };
    }

    // Deleting every merged branch listed, which Restore Deleted Branch brings back one by one, with
    // No the default. Those on origin are deleted there only by a button of its own, which is never
    // the default, since deleting a branch on origin deletes it for everyone: Here Only leaves them.
    internal static MergedDeleteChoice DeleteMergedBranches(string question, bool isHere, bool isOnOrigin)
    {
        if (!isOnOrigin)
            return Ask("Delete Merged Branches", question) ? MergedDeleteChoice.HereOnly : MergedDeleteChoice.Cancel;
        if (!isHere)
            return UI.InfoMessage("Delete Merged Branches", question, 1, ["Delete on Origin", "Cancel"]) == 0
                ? MergedDeleteChoice.AlsoOnOrigin
                : MergedDeleteChoice.Cancel;

        return UI.InfoMessage("Delete Merged Branches", question, 2, ["Here Only", "Also on Origin", "Cancel"]) switch
        {
            0 => MergedDeleteChoice.HereOnly,
            1 => MergedDeleteChoice.AlsoOnOrigin,
            _ => MergedDeleteChoice.Cancel,
        };
    }

    // Deleting a merged branch on origin, for everyone, with Cancel the default
    internal static bool DeleteMergedOnOrigin(string question) =>
        UI.InfoMessage("Delete on Origin", question, 1, ["Delete on Origin", "Cancel"]) == 0;

    // Putting a remote branch back as it was before a force push, which is one in turn, for everyone
    internal static bool RestoreOrigin(string remoteName, string question) => Ask($"Restore {remoteName}", question);

    static string Files(int count) => count == 1 ? "1 file" : $"{count} files";

    static bool Ask(string title, string message) => UI.InfoMessage(title, message, 1, ["Yes", "No"]) == 0;
}
