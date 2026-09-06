using gmd.Common;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IWorktreeCommands
{
    void ShowWorktrees();
    void CreateWorktree(string branchName);
    void OpenWorktree(string path);
    Task<R> OpenWorktreeAsync(string path);
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
                if (!Try(out var updated, out var e, await server.GetUpdatedWorktreesRepoAsync(current)))
                    return R.Error("Failed to read the worktrees", e);
                var worktrees = updated.Worktrees;
                if (worktrees.Count == 0)
                    return R.Error("No worktrees, this version of git does not list them");

                if (!Try(out var choice, worktreesDlg.Show(updated, worktrees, selected)))
                    return R.Ok;
                selected = worktrees.ToList().IndexOf(choice.Worktree);

                switch (choice.Action)
                {
                    case WorktreeAction.Open:
                        return await OpenWorktreeAsync(choice.Worktree.Path);
                    case WorktreeAction.CopyPath:
                        if (!Try(out e, clipboard.Set(choice.Worktree.Path)))
                            return R.Error("Failed to copy the path", e);
                        break;
                    case WorktreeAction.Prune:
                        if (!Try(out e, await PruneAsync(worktrees)))
                            return e;
                        break;
                    case WorktreeAction.Add:
                        if (
                            !Try(
                                out var isOpened,
                                out e,
                                await CreateWorktreeAsync(updated, updated.CurrentBranch().Name)
                            )
                        )
                            return e;
                        if (isOpened)
                            return R.Ok;
                        break;
                    case WorktreeAction.Remove:
                        if (!Try(out e, await RemoveWorktreeAsync(updated, choice.Worktree)))
                            return e;
                        break;
                }
            }
        });

    // From the branch menu: a worktree for that branch, or when it is checked out already, for a
    // new branch started from it
    public void CreateWorktree(string branchName) =>
        Do(async () =>
        {
            if (!Try(out var _, out var e, await CreateWorktreeAsync(repo.Repo, branchName)))
                return e;
            return R.Ok;
        });

    public void OpenWorktree(string path) => Do(() => OpenWorktreeAsync(path));

    public async Task<R> OpenWorktreeAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            return R.Error($"The worktree folder is missing:\n{path}\nPrune it in the Worktrees dialog.");
        }

        if (!Try(out var e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open worktree at {path}", e);
        }
        return R.Ok;
    }

    // Returns whether the new worktree was opened, i.e. whether this repo is still the one shown
    async Task<R<bool>> CreateWorktreeAsync(Repo current, string branchName)
    {
        var mainRoot = current.Worktrees.FirstOrDefault(w => w.IsMain)?.Path ?? current.Path;

        // A remote branch stands for its local branch, or failing one, for itself as a start point
        if (!current.BranchByName.TryGetValue(branchName, out var branch))
            return R.Error($"Unknown branch {branchName}");
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

        if (
            !Try(
                out var ignored,
                out var e,
                await server.GetIgnoredPathsAsync(WorktreeLocations.IgnoredFolders, mainRoot)
            )
        )
        {
            Log.Warn($"Failed to check ignored folders, {e}");
            ignored = [];
        }

        var location = WorktreeLocations.Parse(config.WorktreeLocation);
        if (
            !Try(
                out var rsp,
                addWorktreeDlg.Show(mainRoot, baseBranch, initialName, localBranches, heldBranches, ignored, location)
            )
        )
            return false;
        config.Set(c => c.WorktreeLocation = rsp.Location.ToString());

        if (rsp.IgnoreFolder != "" && !Try(out e, () => AppendToGitIgnore(mainRoot, rsp.IgnoreFolder)))
            return R.Error("Failed to add the folder to .gitignore", e);

        if (
            !Try(
                out e,
                await server.AddWorktreeAsync(rsp.Path, rsp.BranchName, rsp.IsNewBranch, rsp.StartPoint, current.Path)
            )
        )
            return R.Error($"Failed to create worktree at {rsp.Path}", e);

        if (rsp.IsOpen)
        {
            if (!Try(out e, await OpenWorktreeAsync(rsp.Path)))
                return e;
            return true;
        }

        await repoView.RefreshAsync(rsp.BranchName);
        return false;
    }

    // The folder line goes at the end of the main worktree's .gitignore, on a line of its own
    static void AppendToGitIgnore(string mainRoot, string folder)
    {
        var path = Path.Join(mainRoot, ".gitignore");
        var existing = File.Exists(path) ? File.ReadAllText(path) : "";
        var separator = existing == "" || existing.EndsWith('\n') ? "" : "\n";
        File.AppendAllText(path, $"{separator}{folder}/\n");
    }

    async Task<R> RemoveWorktreeAsync(Repo current, Worktree worktree)
    {
        var branch = worktree.Branch != "" && current.BranchByName.TryGetValue(worktree.Branch, out var b) ? b : null;
        var isUnmerged = branch != null && current.HasUnmergedCommits(branch);

        if (!Try(out var rsp, removeWorktreeDlg.Show(worktree, isUnmerged)))
            return R.Ok;

        if (!Try(out var e, await server.RemoveWorktreeAsync(worktree.Path, rsp.IsForce, current.Path)))
            return R.Error($"Failed to remove worktree {worktree.Path}", e);

        // A deliberately checked box on a branch named as unmerged is the consent to lose it
        if (rsp.IsDeleteBranch && branch != null)
        {
            if (!Try(out e, await server.DeleteLocalBranchAsync(branch.Name, isUnmerged, current.Path)))
                return R.Error($"Worktree removed, but failed to delete branch {branch.Name}", e);
        }

        await repoView.RefreshAsync();
        return R.Ok;
    }

    // Forgets the worktrees whose folders are gone, after saying which
    async Task<R> PruneAsync(IReadOnlyList<Worktree> worktrees)
    {
        var missing = worktrees.Where(w => w.IsPrunable).Select(w => w.Path).ToList();
        if (missing.Count == 0)
            return R.Ok;

        var message = $"Forget the worktrees whose folders are gone?\n\n{string.Join("\n", missing)}";
        if (UI.InfoMessage("Prune Worktrees", message, "OK", "Cancel") != 0)
            return R.Ok;

        if (!Try(out var e, await server.PruneWorktreesAsync(repo.Path)))
            return R.Error("Failed to prune worktrees", e);

        await repoView.RefreshAsync();
        return R.Ok;
    }

    void Do(Func<Task<R>> action) => CommandRunner.Do(progress, action);
}
