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

            var branches = BranchesToPull(repo.Repo, currentRemoteName).ToList();
            // Read before the refresh below, so both lists come from the same shown repo
            var diverged = DivergedBranchesToPull(repo.Repo, currentRemoteName).ToList();

            Log.Info($"Pull {string.Join(", ", branches)}");

            // Every branch is tried and what failed is reported once at the end. Stopping at the
            // first failure left every branch after it unpulled, with nothing to say they had
            // been skipped.
            var failed = new List<string>();
            foreach (var b in branches)
            {
                if (!Try(out var e, await server.PullBranchAsync(b.Name, repo.Path)))
                {
                    failed.Add($"{b.NiceNameUnique}: {e.AllErrorMessages()}");
                }
            }

            Refresh();

            if (failed.Any())
                return R.Error($"Failed to pull:\n{string.Join("\n", failed)}");
            if (diverged.Any())
                ShowDivergedMessage(diverged);

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

    // The branches 'pull all branches' pulls, i.e. the remote branches that are behind only and
    // are not the current branch, which has already been pulled by then. A diverged branch is left
    // out for the same reason BranchesToPush leaves one out: a branch that is not current is
    // pulled with 'git fetch origin <b>:<b>', which git rejects as non fast-forward. Merging the
    // two sides means switching to the branch, so DivergedBranchesToPull reports those instead.
    internal static IEnumerable<Branch> BranchesToPull(Repo repo, string currentRemoteName) =>
        repo
            .ViewBranches.Where(b =>
                b.Name != currentRemoteName
                && b.IsRemote
                && !b.IsLocalCurrent
                && !b.IsCurrent
                && b.HasRemoteOnly
                && !b.HasLocalOnly
            )
            .DistinctBy(b => b.PrimaryName);

    // The branches 'pull all branches' has to leave alone, i.e. the ones BranchesToPull drops for
    // being diverged. Same predicate, opposite on that one flag.
    internal static IEnumerable<Branch> DivergedBranchesToPull(Repo repo, string currentRemoteName) =>
        repo
            .ViewBranches.Where(b =>
                b.Name != currentRemoteName
                && b.IsRemote
                && !b.IsLocalCurrent
                && !b.IsCurrent
                && b.HasRemoteOnly
                && b.HasLocalOnly
            )
            .DistinctBy(b => b.PrimaryName);

    // Said out loud rather than passed over in silence: a diverged branch keeps its '▼' marker
    // after an update of all branches, which without this looks like the update having failed.
    static void ShowDivergedMessage(IReadOnlyList<Branch> diverged)
    {
        var names = string.Join("\n", diverged.Select(b => $"  {b.NiceNameUnique}"));
        UI.InfoMessage(
            "Pull/Update All Branches",
            "These branches have both local and remote commits, which an update of all\n"
                + "branches cannot merge, since it only fast-forwards a branch it is not on.\n"
                + $"Switch to the branch and pull it to merge:\n\n{names}"
        );
    }

    void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    void RefreshAndFetch(string addName = "", string commitId = "") => repoView.RefreshAndFetch(addName, commitId);

    void Do(Func<Task<R>> action) => CommandRunner.Do(progress, action);
}
