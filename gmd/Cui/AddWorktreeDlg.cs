using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

// What the user asked for: the folder, the branch (an existing one, or a new one started from
// 'StartPoint'), whether to open the worktree, and the folder to add to .gitignore, if any
record AddWorktreeResult(
    string Path,
    string BranchName,
    bool IsNewBranch,
    string StartPoint,
    bool IsOpen,
    string IgnoreFolder,
    WorktreeLocation Location
);

interface IAddWorktreeDlg
{
    R<AddWorktreeResult> Show(
        string mainRoot,
        string baseBranch,
        string branchName,
        IReadOnlyList<string> localBranches,
        IReadOnlyDictionary<string, string> heldBranches,
        IReadOnlyList<string> alreadyIgnored,
        WorktreeLocation location
    );
}

// The create worktree dialog. The branch is either one of the local branches (the drop down) or
// a new one, which is told from the text as it is typed; the path follows the branch name and the
// picked location until browsed for, and is numbered if the folder is taken.
class AddWorktreeDlg : IAddWorktreeDlg
{
    const int width = 76;

    public R<AddWorktreeResult> Show(
        string mainRoot,
        string baseBranch,
        string branchName,
        IReadOnlyList<string> localBranches,
        IReadOnlyDictionary<string, string> heldBranches,
        IReadOnlyList<string> alreadyIgnored,
        WorktreeLocation location
    )
    {
        var dlg = new UIDialog("Create Worktree", width + 4, 16);

        // Read by the button actions below, which are wired before these are added
        var browsedParent = "";
        CheckBox ignore = null!;

        dlg.AddLabel(1, 0, "Branch:");
        var branch = dlg.AddComboTextField(9, 0, width - 10, 8, () => localBranches, branchName);
        var hint = dlg.AddLabel(9, 2, "");

        dlg.AddLabel(1, 4, "Path:");
        var path = dlg.AddInputField(9, 4, width - 20, "", InputMarkers.None);
        dlg.AddButton(width - 9, 4, "Browse", () => Browse());

        dlg.AddLabel(1, 6, "Put in:");
        var sibling = dlg.AddButton(9, 6, "Beside repo", () => Pick(WorktreeLocation.Sibling));
        var claude = dlg.AddButton(25, 6, ".claude/worktrees", () => Pick(WorktreeLocation.Claude));
        var local = dlg.AddButton(47, 6, ".worktrees", () => Pick(WorktreeLocation.Local));

        ignore = dlg.AddCheckBox(1, 8, "Add to .gitignore", true);
        var open = dlg.AddCheckBox(1, 9, "Open worktree after creating", true);

        string BranchText() => branch.Text.ToString()?.Trim() ?? "";
        bool IsNew() => !localBranches.Contains(BranchText());

        void UpdatePath()
        {
            var name = BranchText();
            if (name == "")
            {
                path.Text = "";
                return;
            }
            path.Text =
                browsedParent != ""
                    ? Files.UniqueFolderPath(browsedParent, WorktreeLocations.FolderName(name))
                    : UniquePath(WorktreeLocations.PathFor(location, mainRoot, name));
            path.SetNeedsDisplay();
        }

        void UpdateHint()
        {
            var name = BranchText();
            hint.Text = (
                name == "" ? Text.Dark("A branch to check out, or a new one to create")
                : heldBranches.TryGetValue(name, out var heldAt)
                    ? Text.Red($"Already checked out at {ShortPath(heldAt)}")
                : IsNew() ? Text.Dark($"New branch from '{baseBranch}'")
                : Text.Dark("Existing branch")
            ).ToText();
        }

        void UpdateIgnore()
        {
            var folder = WorktreeLocations.IgnoreFolder(location);
            ignore.Visible = folder != "" && !alreadyIgnored.Contains(folder) && browsedParent == "";
            ignore.Text = $"Add {folder}/ to .gitignore";
        }

        void Pick(WorktreeLocation picked)
        {
            location = picked;
            browsedParent = "";
            UpdatePath();
            UpdateIgnore();
        }

        void Browse()
        {
            if (!Try(out var folder, new FolderBrowseDlg().Show([mainRoot])) || folder == "")
                return;
            browsedParent = folder.Trim();
            UpdatePath();
            UpdateIgnore();
        }

        branch.KeyUp += _ =>
        {
            UpdatePath();
            UpdateHint();
        };

        dlg.Validate(() => BranchText() != "", "Empty branch name");
        dlg.Validate(
            () => !heldBranches.ContainsKey(BranchText()),
            $"The branch is already checked out in another worktree.\nGit allows a branch in one worktree only."
        );
        dlg.Validate(() => path.Text.Trim() != "", "Empty path");
        dlg.Validate(
            () => !Directory.Exists(path.Text.Trim()) && !File.Exists(path.Text.Trim()),
            "The folder already exists"
        );

        UpdatePath();
        UpdateHint();
        UpdateIgnore();

        if (!dlg.ShowOkCancel(branch))
            return R.Error();

        var isNew = IsNew();
        var ignoreFolder = ignore.Visible && ignore.Checked ? WorktreeLocations.IgnoreFolder(location) : "";
        return new AddWorktreeResult(
            path.Text.Trim(),
            BranchText(),
            isNew,
            isNew ? baseBranch : "",
            open.Checked,
            ignoreFolder,
            location
        );
    }

    static string UniquePath(string fullPath) =>
        Files.UniqueFolderPath(Path.GetDirectoryName(fullPath) ?? "", Path.GetFileName(fullPath));

    static string ShortPath(string path) => path.Length <= 40 ? path : $"┅{path[^40..]}";
}
