using gmd.Common;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IWorktreeCommands
{
    void ShowWorktrees();
    void CreateWorktree(string branchName);
    void OpenWorktree(string path);
    Task<Result> OpenWorktreeAsync(string path);
}

// The worktree commands: the dialog listing them, and opening, adding, removing and pruning
// them. Opening a worktree is opening its folder as the repo, i.e. what opening a repo does —
// gmd shows one worktree at a time, and everything then acts on that folder.
class WorktreeCommands : IWorktreeCommands
{
    readonly IViewRepo repo;
    readonly IRepoView repoView;
    readonly IProgress progress;
    readonly IServer server;
    readonly Config config;
    readonly IWorktreesDlg worktreesDlg;
    readonly IAddWorktreeDlg addWorktreeDlg;
    readonly IRemoveWorktreeDlg removeWorktreeDlg;
    readonly IClipboardService clipboard;

    public WorktreeCommands(
        IViewRepo repo,
        IRepoView repoView,
        IProgress progress,
        IServer server,
        Config config,
        IWorktreesDlg worktreesDlg,
        IAddWorktreeDlg addWorktreeDlg,
        IRemoveWorktreeDlg removeWorktreeDlg,
        IClipboardService clipboard
    )
    {
        this.repo = repo;
        this.repoView = repoView;
        this.progress = progress;
        this.server = server;
        this.config = config;
        this.worktreesDlg = worktreesDlg;
        this.addWorktreeDlg = addWorktreeDlg;
        this.removeWorktreeDlg = removeWorktreeDlg;
        this.clipboard = clipboard;
    }

    // The dialog is shown again after every action but opening, with the worktrees re-read so
    // what it shows is what is, not what the last reload saw
    public void ShowWorktrees() =>
        Do(async () =>
        {
            var selected = 0;
            while (true)
            {
                var current = repoView.ViewRepo.Repo;
                var updatedResult = await server.GetUpdatedWorktreesRepoAsync(current);
                if (updatedResult is not Repo updated)
                    return new Error("Failed to read the worktrees", updatedResult.Error);
                var worktrees = updated.Worktrees;
                if (worktrees.Count == 0)
                    return new Error("No worktrees, this version of git does not list them");

                if (worktreesDlg.Show(updated, worktrees, selected) is not WorktreeChoice choice)
                    return Result.Ok;
                selected = worktrees.ToList().IndexOf(choice.Worktree);

                switch (choice.Action)
                {
                    case WorktreeAction.Open:
                        return await OpenWorktreeAsync(choice.Worktree.Path);
                    case WorktreeAction.CopyPath:
                        if (clipboard.Set(choice.Worktree.Path) is Error copyError)
                            return new Error("Failed to copy the path", copyError);
                        break;
                    case WorktreeAction.Prune:
                        if (await PruneAsync(worktrees) is Error pruneError)
                            return pruneError;
                        break;
                    case WorktreeAction.Add:
                        var created = await CreateWorktreeAsync(updated, updated.CurrentBranch().Name);
                        if (created is not bool isOpened)
                            return created.Error;
                        if (isOpened)
                            return Result.Ok;
                        break;
                    case WorktreeAction.Remove:
                        if (await RemoveWorktreeAsync(updated, choice.Worktree) is Error removeError)
                            return removeError;
                        break;
                }
            }
        });

    // From the branch menu: a worktree for that branch, or when it is checked out already, for a
    // new branch started from it
    public void CreateWorktree(string branchName) =>
        Do(async () =>
        {
            if (await CreateWorktreeAsync(repo.Repo, branchName) is Error e)
                return e;
            return Result.Ok;
        });

    public void OpenWorktree(string path) => Do(() => OpenWorktreeAsync(path));

    public async Task<Result> OpenWorktreeAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            return new Error($"The worktree folder is missing:\n{path}\nPrune it in the Worktrees dialog.");
        }

        if (await repoView.ShowRepoAsync(path) is Error e)
        {
            return new Error($"Failed to open worktree at {path}", e);
        }
        return Result.Ok;
    }

    // Returns whether the new worktree was opened, i.e. whether this repo is still the one shown
    async Task<Result<bool>> CreateWorktreeAsync(Repo current, string branchName)
    {
        var mainRoot = current.Worktrees.FirstOrDefault(w => w.IsMain)?.Path ?? current.Path;

        // A remote branch stands for its local branch, or failing one, for itself as a start point
        if (!current.BranchByName.TryGetValue(branchName, out var branch))
            return new Error($"Unknown branch {branchName}");
        var baseBranch = branch.LocalName != "" ? branch.LocalName : branch.Name;

        var localBranches = current
            .AllBranches.Where(b => !b.IsRemote && b.IsGitBranch && !b.IsDetached)
            .Select(b => b.Name)
            .OrderBy(n => n)
            .ToList();

        // Git allows a branch in one worktree only, and the current branch is in this one
        var heldBranches = current
            .AllBranches.Where(b => !b.IsRemote && (b.IsCurrent || b.WorktreePath != ""))
            .ToDictionary(b => b.Name, b => b.WorktreePath != "" ? b.WorktreePath : current.Path);
        var initialName = heldBranches.ContainsKey(baseBranch) || !localBranches.Contains(baseBranch) ? "" : baseBranch;

        var ignoredResult = await server.GetIgnoredPathsAsync(WorktreeLocations.IgnoredFolders, mainRoot);
        if (ignoredResult is not IReadOnlyList<string> ignored)
        {
            Log.Warn($"Failed to check ignored folders, {ignoredResult.Error}");
            ignored = [];
        }

        var location = WorktreeLocations.Parse(config.WorktreeLocation);
        var added = addWorktreeDlg.Show(
            mainRoot,
            baseBranch,
            initialName,
            localBranches,
            heldBranches,
            ignored,
            location
        );
        if (added is not AddWorktreeResult rsp)
            return false;
        config.Set(c => c.WorktreeLocation = rsp.Location.ToString());

        var addedAndIgnored = await AddAndIgnoreAsync(
            rsp,
            mainRoot,
            () => server.AddWorktreeAsync(rsp.Path, rsp.BranchName, rsp.IsNewBranch, rsp.StartPoint, current.Path)
        );
        if (addedAndIgnored is Error addError)
            return addError;

        if (rsp.IsOpen)
        {
            if (await OpenWorktreeAsync(rsp.Path) is Error openError)
                return openError;
            return true;
        }

        await repoView.RefreshAsync(rsp.BranchName);
        return false;
    }

    // The worktree first, and the line in .gitignore only once it exists: a worktree git refuses
    // then leaves no line behind for a folder that never came to be, and a line that cannot be
    // written afterwards is reported as that, since the worktree is there to be used. The add is
    // a delegate so the order is testable without the server.
    internal static async Task<Result> AddAndIgnoreAsync(
        AddWorktreeResult rsp,
        string mainRoot,
        Func<Task<Result>> addWorktree
    )
    {
        if (await addWorktree() is Error e)
            return new Error($"Failed to create worktree at {rsp.Path}", e);

        if (
            rsp.IgnoreFolder != ""
            && Result.Catch(() => AppendToGitIgnore(mainRoot, rsp.IgnoreFolder)) is Error ignoreError
        )
            return new Error(
                $"Worktree created at {rsp.Path},\nbut failed to add {rsp.IgnoreFolder}/ to .gitignore",
                ignoreError
            );

        return Result.Ok;
    }

    // The folder line goes at the end of the main worktree's .gitignore, on a line of its own
    static void AppendToGitIgnore(string mainRoot, string folder)
    {
        var path = Path.Join(mainRoot, ".gitignore");
        var existing = File.Exists(path) ? File.ReadAllText(path) : "";
        var separator = existing == "" || existing.EndsWith('\n') ? "" : "\n";
        File.AppendAllText(path, $"{separator}{folder}/\n");
    }

    async Task<Result> RemoveWorktreeAsync(Repo current, Worktree worktree)
    {
        var branch = worktree.Branch != "" && current.BranchByName.TryGetValue(worktree.Branch, out var b) ? b : null;
        var isUnmerged = branch != null && current.HasUnmergedCommits(branch);

        if (removeWorktreeDlg.Show(worktree, isUnmerged) is not RemoveWorktreeResult rsp)
            return Result.Ok;

        if (await server.RemoveWorktreeAsync(worktree.Path, rsp.IsForce, current.Path) is Error e)
            return new Error($"Failed to remove worktree {worktree.Path}", e);

        // A deliberately checked box on a branch named as unmerged is the consent to lose it
        if (rsp.IsDeleteBranch && branch != null)
        {
            if (await server.DeleteLocalBranchAsync(branch.Name, isUnmerged, current.Path) is Error deleteError)
                return new Error($"Worktree removed, but failed to delete branch {branch.Name}", deleteError);
        }

        await repoView.RefreshAsync();
        return Result.Ok;
    }

    // Forgets the worktrees whose folders are gone, after saying which
    async Task<Result> PruneAsync(IReadOnlyList<Worktree> worktrees)
    {
        var missing = worktrees.Where(w => w.IsPrunable).Select(w => w.Path).ToList();
        if (missing.Count == 0)
            return Result.Ok;

        var message = $"Forget the worktrees whose folders are gone?\n\n{string.Join("\n", missing)}";
        if (UI.InfoMessage("Prune Worktrees", message, "OK", "Cancel") != 0)
            return Result.Ok;

        if (await server.PruneWorktreesAsync(repo.Path) is Error e)
            return new Error("Failed to prune worktrees", e);

        await repoView.RefreshAsync();
        return Result.Ok;
    }

    void Do(Func<Task<Result>> action) => CommandRunner.Do(progress, action);
}
