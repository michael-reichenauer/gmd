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

    internal static bool CleanWorkingFolder() =>
        Ask(
            "Clean Working Folder",
            """
            Reset the working folder to the last commit?

            Every uncommitted change is undone, and every untracked
            file is deleted, including the files git ignores,
            such as build output and local settings.
            This cannot be undone.
            """
        );

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

    static string Files(int count) => count == 1 ? "1 file" : $"{count} files";

    static bool Ask(string title, string message) => UI.InfoMessage(title, message, 1, ["Yes", "No"]) == 0;
}
