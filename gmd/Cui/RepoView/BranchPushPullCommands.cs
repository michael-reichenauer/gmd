using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// Pushing and pulling branches, and the predicates the menus use to enable those items.
interface IBranchPushPullCommands
{
    void PushCurrentBranch();
    void PushBranch(string name);
    void PushAllBranches();
    void PublishCurrentBranch();
    void PullCurrentBranch();
    void PullBranch(string name);
    void PullAllBranches();
    bool CanPushCurrentBranch();
    bool CanPush();
    bool CanPull();
    bool CanPullCurrentBranch();
}

class BranchPushPullCommands : IBranchPushPullCommands
{
    readonly IViewRepo repo;
    readonly IProgress progress;
    readonly IRepoView repoView;
    readonly IServer server;

    public BranchPushPullCommands(IViewRepo repo, IProgress progress, IRepoView repoView, IServer server)
    {
        this.repo = repo;
        this.progress = progress;
        this.repoView = repoView;
        this.server = server;
    }

    public void PushCurrentBranch() =>
        Do(async () =>
        {
            var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);

            if (!repo.Repo.Status.IsOk)
                return R.Error("Commit changes before pushing");
            if (branch == null)
                return R.Error("No current branch to push");
            if (!branch.HasLocalOnly)
                return R.Error($"No local changes to push on current branch:\n{branch.NiceNameUnique}");

            if (branch.RemoteName != "")
            { // Cannot push local branch if remote needs to be pulled first
                var remoteBranch = repo.Repo.BranchByName[branch.RemoteName];
                if (remoteBranch != null && remoteBranch.HasRemoteOnly)
                {
                    if (
                        0
                        != UI.ErrorMessage(
                            "Push Warning",
                            $"""
                            Branch '{branch.Name}' 
                            has remote commits not yet pulled.
                            Pull current remote branch first before pushing,
                            or do you want to force push?
                            NOTE: be careful!
                            """,
                            1,
                            "Force Push",
                            "Cancel"
                        )
                    )
                    {
                        RefreshAndFetch();
                        return R.Ok;
                    }
                }

                if (!Try(out var ee, await server.PushCurrentBranchAsync(true, repo.Path)))
                {
                    return R.Error($"Failed to push branch:\n{branch.Name}", ee);
                }
            }

            if (!Try(out var e, await server.PushBranchAsync(branch.Name, repo.Path)))
            {
                return R.Error($"Failed to push branch:\n{branch.Name}", e);
            }

            Refresh();
            return R.Ok;
        });

    public void PublishCurrentBranch() =>
        Do(async () =>
        {
            var branch = repo.Repo.ViewBranches.First(b => b.IsCurrent);

            if (!Try(out var e, await server.PushBranchAsync(branch.Name, repo.Path)))
            {
                return R.Error($"Failed to publish branch:\n{branch.Name}", e);
            }

            Refresh();
            return R.Ok;
        });

    public void PushBranch(string name) =>
        Do(async () =>
        {
            if (!Try(out var e, await server.PushBranchAsync(name, repo.Path)))
            {
                return R.Error($"Failed to push branch:\n{name}", e);
            }

            Refresh();
            return R.Ok;
        });

    public void PushAllBranches() =>
        Do(async () =>
        {
            if (!repo.Repo.Status.IsOk)
                return R.Error("Commit changes before pulling");
            if (!CanPush())
                return R.Error("No local changes to push");

            var branches = BranchesToPush(repo.Repo);

            foreach (var b in branches)
            {
                if (!Try(out var e, await server.PushBranchAsync(b.Name, repo.Path)))
                {
                    Refresh();
                    return R.Error($"Failed to push branch {b.Name}", e);
                }
            }

            Refresh();
            return R.Ok;
        });

    public void PullCurrentBranch() =>
        Do(async () =>
        {
            var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
            if (!repo.Repo.Status.IsOk)
                return R.Error("Commit changes before pulling");
            if (branch == null)
                return R.Error("No current branch to pull");
            if (branch.RemoteName == "")
                return R.Error("No current remote branch to pull");

            var remoteBranch = repo.Repo.BranchByName[branch.RemoteName];
            if (remoteBranch == null || !remoteBranch.HasRemoteOnly)
                return R.Error("No remote changes on current branch to pull");

            if (!Try(out var e, await server.PullCurrentBranchAsync(repo.Path)))
            {
                return R.Error($"Failed to pull current branch", e);
            }

            Refresh();
            return R.Ok;
        });

    public void PullBranch(string name) =>
        Do(async () =>
        {
            if (!Try(out var e, await server.PullBranchAsync(name, repo.Path)))
            {
                return R.Error($"Failed to pull branch {name}", e);
            }

            Refresh();
            return R.Ok;
        });

    public void PullAllBranches() =>
        Do(async () =>
        {
            var currentRemoteName = "";
            if (CanPullCurrentBranch())
            {
                Log.Info("Pull current");
                // Need to treat current branch separately
                if (!Try(out var e, await server.PullCurrentBranchAsync(repo.Path)))
                {
                    return R.Error($"Failed to pull current branch", e);
                }
                currentRemoteName = repo.Repo.CurrentBranch()?.RemoteName ?? "";
            }

            var branches = BranchesToPull(repo.Repo, currentRemoteName);

            Log.Info($"Pull {string.Join(", ", branches)}");
            foreach (var b in branches)
            {
                if (!Try(out var e, await server.PullBranchAsync(b.Name, repo.Path)))
                {
                    Refresh();
                    return R.Error($"Failed to pull branch {b.Name}", e);
                }
            }

            Refresh();
            return R.Ok;
        });

    public bool CanPush() => CanPush(repo.Repo);

    public bool CanPushCurrentBranch() => CanPushCurrentBranch(repo.Repo);

    public bool CanPull() => CanPull(repo.Repo);

    public bool CanPullCurrentBranch() => CanPullCurrentBranch(repo.Repo);

    // The rules for what can be pushed and pulled are plain functions of the shown repo, so they
    // are testable without a view. Note that a diverged branch (both local and remote only
    // commits) can neither be pushed nor be part of 'push all branches', since git would reject
    // it as non fast-forward.
    internal static bool CanPush(Repo repo) =>
        repo.Status.IsOk && repo.ViewBranches.Any(b => b.HasLocalOnly && !b.HasRemoteOnly);

    internal static bool CanPushCurrentBranch(Repo repo)
    {
        var branch = repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null)
            return false;

        if (branch.RemoteName != "")
        { // Cannot push local branch if remote needs to be pulled first
            var remoteBranch = repo.BranchByName[branch.RemoteName];
            if (remoteBranch != null && remoteBranch.HasRemoteOnly)
                return false;
        }

        return repo.Status.IsOk && branch != null && branch.HasLocalOnly;
    }

    internal static bool CanPull(Repo repo) => repo.Status.IsOk && repo.ViewBranches.Any(b => b.HasRemoteOnly);

    internal static bool CanPullCurrentBranch(Repo repo)
    {
        var branch = repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null)
            return false;

        if (branch.RemoteName == "")
            return false; // No remote branch to pull

        var remoteBranch = repo.BranchByName[branch.RemoteName];
        return repo.Status.IsOk && remoteBranch != null && remoteBranch.HasRemoteOnly;
    }

    // The branches 'push all branches' pushes, i.e. one row per branch (a branch and its remote
    // share their primary name)
    internal static IEnumerable<Branch> BranchesToPush(Repo repo) =>
        repo.ViewBranches.Where(b => b.HasLocalOnly && !b.HasRemoteOnly).DistinctBy(b => b.PrimaryName);

    // The branches 'pull all branches' pulls, i.e. the remote branches that are not the current
    // branch, which has already been pulled by then
    internal static IEnumerable<Branch> BranchesToPull(Repo repo, string currentRemoteName) =>
        repo
            .ViewBranches.Where(b =>
                b.Name != currentRemoteName && b.IsRemote && !b.IsLocalCurrent && !b.IsCurrent && b.HasRemoteOnly
            )
            .DistinctBy(b => b.PrimaryName);

    void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    void RefreshAndFetch(string addName = "", string commitId = "") => repoView.RefreshAndFetch(addName, commitId);

    void Do(Func<Task<R>> action) => CommandRunner.Do(progress, action);
}
