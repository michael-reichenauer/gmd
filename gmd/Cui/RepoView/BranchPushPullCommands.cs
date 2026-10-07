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
    void RestoreOrigin(string name);
    bool CanPushCurrentBranch();
    bool CanPush();
    bool CanPull();
    bool CanPullCurrentBranch();
}

class BranchPushPullCommands : IBranchPushPullCommands
{
    readonly IViewRepo repo;
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly IRepoView repoView;
    readonly IServer server;

    public BranchPushPullCommands(
        IViewRepo repo,
        IProgress progress,
        IStatusLine status,
        IRepoView repoView,
        IServer server
    )
    {
        this.repo = repo;
        this.progress = progress;
        this.status = status;
        this.repoView = repoView;
        this.server = server;
    }

    public void PushCurrentBranch() =>
        Do(async () =>
        {
            var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);

            // Why nothing was pushed is said on the status line, see Notice: none of these is an error.
            // Changes are no reason, a push sends commits and leaves them where they are.
            if (repo.Repo.Status.IsMerging)
                return new Notice(Why.InProgress);
            if (branch == null)
                return new Notice("No branch is checked out to push");
            if (!branch.HasLocalOnly)
            {
                return new Notice(
                    branch.RemoteName == ""
                        ? $"'{branch.NiceNameUnique}' is not on origin yet: Push in its branch menu publishes it"
                        : $"Nothing to push on '{branch.NiceNameUnique}'"
                );
            }

            if (branch.RemoteName != "")
            { // Cannot push local branch if remote needs to be pulled first
                var remoteBranch = repo.Repo.BranchByName[branch.RemoteName];
                if (remoteBranch != null && remoteBranch.HasRemoteOnly)
                {
                    // Said differently when origin was rewritten by a force push, since a force push
                    // from here would then put the old version back for everyone
                    var warning = ForcePushes.RewriteOf(repo.Repo, branch) is RemoteRewrite rewrite
                        ? ForcePushes.PushWarning(rewrite)
                        : $"""
                            Branch '{branch.Name}' 
                            has remote commits not yet pulled.
                            Pull current remote branch first before pushing,
                            or do you want to force push?
                            NOTE: be careful!
                            """;
                    if (0 != UI.ErrorMessage("Push Warning", warning, 1, "Force Push", "Cancel"))
                    {
                        RefreshAndFetch();
                        return Result.Ok;
                    }

                    // Force Push was chosen. Only then: this call used to sit after the question
                    // rather than inside it, so every push of a branch with a remote was a force push.
                    using (status.Progress($"Force pushing '{branch.NiceNameUnique}'"))
                    {
                        if (await server.PushCurrentBranchAsync(true, repo.Path) is Error ee)
                            return new Error($"Failed to push branch:\n{branch.Name}", ee);
                    }

                    // And that is the push: the plain one below would go to the remote a second
                    // time, and fail if anyone pushed in between, after the force push had worked
                    Refresh();
                    status.Info(Pushed($"'{branch.NiceNameUnique}'"));
                    return Result.Ok;
                }
            }
            if (!IsPushConfirmed(branch))
                return Result.Ok;

            using (status.Progress($"Pushing '{branch.NiceNameUnique}'"))
            {
                if (await server.PushBranchAsync(branch.Name, repo.Path) is Error e)
                    return new Error($"Failed to push branch:\n{branch.Name}", e);
            }

            Refresh();
            status.Info(Pushed($"'{branch.NiceNameUnique}'"));
            return Result.Ok;
        });

    public void PublishCurrentBranch() =>
        Do(async () =>
        {
            var branch = repo.Repo.ViewBranches.First(b => b.IsCurrent);

            using (status.Progress($"Publishing '{branch.NiceNameUnique}'"))
            {
                if (await server.PushBranchAsync(branch.Name, repo.Path) is Error e)
                    return new Error($"Failed to publish branch:\n{branch.Name}", e);
            }

            Refresh();
            status.Info($"Published '{branch.NiceNameUnique}'");
            return Result.Ok;
        });

    public void PushBranch(string name) =>
        Do(async () =>
        {
            if (repo.Repo.BranchByName.TryGetValue(name, out var branch) && !IsPushConfirmed(branch))
                return Result.Ok;

            using (status.Progress($"Pushing '{NiceName(name)}'"))
            {
                if (await server.PushBranchAsync(name, repo.Path) is Error e)
                    return new Error($"Failed to push branch:\n{name}", e);
            }

            Refresh();
            status.Info(Pushed($"'{NiceName(name)}'"));
            return Result.Ok;
        });

    public void PushAllBranches() =>
        Do(async () =>
        {
            if (repo.Repo.Status.IsMerging)
                return new Notice(Why.InProgress);
            if (!CanPush())
                return new Notice("Nothing to push");

            // A branch whose push would put back what a force push dropped is left to be pushed on its
            // own, which asks first
            var branches = BranchesToPush(repo.Repo).ToList();
            var pushedBack = branches.Where(IsPushedBack).ToList();
            branches = branches.Except(pushedBack).ToList();
            var leftNames = pushedBack.Select(b => ForcePushes.RewriteOf(repo.Repo, b)?.RemoteName ?? b.Name).ToList();
            if (branches.Count == 0)
                return new Notice(Rewritten(leftNames, "push"));

            foreach (var b in branches)
            {
                using (status.Progress($"Pushing '{b.NiceNameUnique}'"))
                {
                    if (await server.PushBranchAsync(b.Name, repo.Path) is Error e)
                    {
                        Refresh();
                        return new Error($"Failed to push branch {b.Name}", e);
                    }
                }
            }

            Refresh();
            var left = leftNames.Any() ? $"; {Rewritten(leftNames, "push")}" : "";
            status.Info(Pushed(Names(branches.Select(b => b.NiceNameUnique))) + left);
            return Result.Ok;
        });

    public void PullCurrentBranch() =>
        Do(async () =>
        {
            var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
            if (!repo.Repo.Status.IsOk)
                return new Notice("Commit the changes first, then pull");
            if (branch == null)
                return new Notice("No branch is checked out to pull");
            if (branch.RemoteName == "")
                return new Notice($"'{branch.NiceNameUnique}' is not on origin, so there is nothing to pull");

            // A rewritten remote branch is not merged, whatever pull.rebase says, and one a force push
            // only dropped commits from is pulled with nothing new on it
            if (ForcePushes.RewriteOf(repo.Repo, branch) is RemoteRewrite rewrite)
                return await PullRewrittenAsync(rewrite);

            var remoteBranch = repo.Repo.BranchByName[branch.RemoteName];
            if (remoteBranch == null || !remoteBranch.HasRemoteOnly)
                return new Notice($"Nothing to pull on '{branch.NiceNameUnique}'");

            var way = await EnsurePullWayAsync(remoteBranch);
            if (way is not bool isToPull)
                return way.Error;
            if (!isToPull)
                return Result.Ok;

            using (status.Progress($"Pulling '{branch.NiceNameUnique}'"))
            {
                if (await server.PullCurrentBranchAsync(repo.Path) is Error e)
                    return new Error($"Failed to pull current branch", e);
            }

            Refresh();
            status.Info($"Pulled '{branch.NiceNameUnique}'");
            return Result.Ok;
        });

    public void PullBranch(string name) =>
        Do(async () =>
        {
            if (
                repo.Repo.BranchByName.TryGetValue(name, out var branch)
                && ForcePushes.RewriteOf(repo.Repo, branch) is RemoteRewrite rewrite
            )
                return await PullRewrittenAsync(rewrite);

            using (status.Progress($"Updating '{NiceName(name)}'"))
            {
                if (await server.PullBranchAsync(name, repo.Path) is Error e)
                    return new Error($"Failed to pull branch {name}", e);
            }

            Refresh();
            status.Info($"Updated '{NiceName(name)}'");
            return Result.Ok;
        });

    // A branch whose remote branch a force push rewrote, after saying what that means and asking:
    // its own commits are moved onto the new version, rather than the two versions merged
    async Task<Result> PullRewrittenAsync(RemoteRewrite rewrite)
    {
        if (
            UI.InfoMessage("Pull Rewritten Branch", ForcePushes.PullQuestion(repo.Repo, rewrite), 1, ["Pull", "Cancel"])
            != 0
        )
            return Result.Ok;

        using (status.Progress($"Pulling '{rewrite.BranchName}'"))
        {
            if (await server.PullRewrittenAsync(repo.Repo, rewrite) is Error e)
                return new Error($"Failed to pull '{rewrite.BranchName}'", e);
        }

        Refresh();
        status.Info(ForcePushes.Pulled(rewrite));
        return Result.Ok;
    }

    // Puts a remote branch a force push rewrote back as it was, after asking, since that is a force
    // push too: a mistaken one taken back, or gmd's Rebase undone on origin as well as here. Only if
    // nothing was pushed on top of the rewrite, and only while origin is where it was last fetched.
    public void RestoreOrigin(string name) =>
        Do(async () =>
        {
            if (
                !repo.Repo.BranchByName.TryGetValue(name, out var branch)
                || ForcePushes.RewriteOf(repo.Repo, branch) is not RemoteRewrite rewrite
            )
                return new Notice($"'{NiceName(name)}' was not rewritten by a force push");
            if (!rewrite.IsRestorable)
                return new Notice(ForcePushes.WhyNoRestore(rewrite));
            if (!Confirm.RestoreOrigin(rewrite.RemoteName, ForcePushes.RestoreQuestion(repo.Repo, rewrite)))
                return Result.Ok;

            using (status.Progress($"Restoring '{rewrite.RemoteName}'"))
            {
                if (await server.RestoreOriginAsync(rewrite, repo.Path) is Error e)
                    return new Error($"Failed to restore '{rewrite.RemoteName}'", e);
            }

            Refresh();
            status.Info(ForcePushes.Restored(rewrite));
            return Result.Ok;
        });

    public void PullAllBranches() =>
        Do(async () =>
        {
            var currentRemoteName = "";
            List<string> updated = [];

            // The current branch is pulled with 'git pull', which needs the changes out of the way,
            // while the others are fetched, which leaves the working tree alone. So they are pulled
            // all the same, and the current one is said to have been left, as 'Nothing to pull'
            // would be said with its ▼ on screen.
            var leftCurrent =
                !repo.Repo.Status.IsOk && IsCurrentBranchBehind(repo.Repo)
                    ? repo.Repo.CurrentBranch()?.NiceNameUnique
                    : null;

            // A branch whose remote branch a force push rewrote is not merged, so what pulling it
            // asks is left to pulling it on its own, unless all it has to do is take the new version
            List<string> leftRewritten = [];
            var currentRewrite = ForcePushes.RewriteOf(repo.Repo, repo.Repo.CurrentBranch());
            if (currentRewrite != null)
                leftRewritten.Add(currentRewrite.RemoteName);

            if (CanPullCurrentBranch() && currentRewrite == null)
            {
                if (repo.Repo.BranchByName.TryGetValue(repo.Repo.CurrentBranch()?.RemoteName ?? "", out var current))
                {
                    var way = await EnsurePullWayAsync(current);
                    if (way is not bool isToPull)
                        return way.Error;
                    if (!isToPull)
                        return Result.Ok;
                }

                Log.Info("Pull current");
                // Need to treat current branch separately
                using (status.Progress($"Pulling '{repo.Repo.CurrentBranch().NiceNameUnique}'"))
                {
                    if (await server.PullCurrentBranchAsync(repo.Path) is Error e)
                        return new Error($"Failed to pull current branch", e);
                }
                currentRemoteName = repo.Repo.CurrentBranch()?.RemoteName ?? "";
                updated.Add(repo.Repo.CurrentBranch()?.NiceNameUnique ?? "");
            }

            var branches = BranchesToPull(repo.Repo, currentRemoteName).ToList();
            // Read before the refresh below, so both lists come from the same shown repo
            var diverged = DivergedBranchesToPull(repo.Repo, currentRemoteName).ToList();
            var rewritten = RewritesToPull(repo.Repo, currentRemoteName).ToList();
            diverged = diverged.Where(b => ForcePushes.RewriteOf(repo.Repo, b) == null).ToList();
            leftRewritten.AddRange(rewritten.Where(r => r.OwnCount > 0).Select(r => r.RemoteName));

            Log.Info($"Pull {string.Join(", ", branches)}");

            // Every branch is tried and what failed is reported once at the end. Stopping at the
            // first failure left every branch after it unpulled, with nothing to say they had
            // been skipped.
            var failed = new List<string>();
            foreach (var b in branches)
            {
                using (status.Progress($"Updating '{b.NiceNameUnique}'"))
                {
                    if (await server.PullBranchAsync(b.Name, repo.Path) is Error e)
                        failed.Add($"{b.NiceNameUnique}: {Git.LoginError.Text(e)}");
                    else
                        updated.Add(b.NiceNameUnique);
                }
            }
            foreach (var r in rewritten.Where(r => r.OwnCount == 0))
            {
                using (status.Progress($"Updating '{r.BranchName}'"))
                {
                    if (await server.PullRewrittenAsync(repo.Repo, r) is Error e)
                        failed.Add($"{r.BranchName}: {Git.LoginError.Text(e)}");
                    else
                        updated.Add(r.BranchName);
                }
            }

            Refresh();

            if (failed.Any())
                return new Error($"Failed to pull:\n{string.Join("\n", failed)}");
            if (diverged.Any())
                ShowDivergedMessage(diverged);
            else if (!updated.Any())
                return new Notice(
                    leftRewritten.Any() ? Rewritten(leftRewritten)
                    : leftCurrent != null ? $"{Why.Changes}, then pull '{leftCurrent}'"
                    : "Nothing to pull"
                );

            if (updated.Any())
            {
                var left = leftCurrent != null ? $", but not '{leftCurrent}': commit or stash the changes first" : "";
                var rewrittenLeft = leftRewritten.Any() ? $"; {Rewritten(leftRewritten)}" : "";
                status.Info($"Updated {Names(updated)}{left}{rewrittenLeft}");
            }
            return Result.Ok;
        });

    // A branch that has diverged from its remote is joined to it by a merge or a rebase. Git leaves
    // that to its config, and refuses to guess when none is set: 'fatal: Need to specify how to
    // reconcile divergent branches', under a dozen lines of hints, which used to be gmd's error. So
    // gmd asks, once, and saves the answer where git reads it, pull.rebase, so that a pull on the
    // command line does the same from then on. A branch that is only behind is a fast-forward, which
    // needs neither. False when the user cancelled.
    async Task<Result<bool>> EnsurePullWayAsync(Branch remoteBranch)
    {
        if (!(remoteBranch.HasRemoteOnly && remoteBranch.HasLocalOnly))
            return true;

        var localName = remoteBranch.LocalName != "" ? remoteBranch.LocalName : remoteBranch.Name;
        var configured = await server.IsPullWayConfiguredAsync(localName, repo.Path);
        if (configured is not bool isConfigured)
            return configured.Error;
        if (isConfigured)
            return true;

        var commits = repo.Repo.ViewCommits.Where(c => c.BranchPrimaryName == remoteBranch.PrimaryName).ToList();
        var ahead = Commits(commits.Count(c => c.IsAhead));
        var behind = Commits(commits.Count(c => c.IsBehind));
        var choice = UI.InfoMessage(
            "Pull Diverged Branch",
            $"'{NiceName(remoteBranch.Name)}' has {ahead} not pushed, and origin has {behind}\n"
                + "not pulled. Merge joins the two with a merge commit, and Rebase\n"
                + $"moves your {ahead} on top of origin's.\n\n"
                + "The answer is saved as pull.rebase in the repository's git config,\n"
                + "so it is not asked again, and git on the command line does the same.",
            0,
            ["Merge", "Rebase", "Cancel"]
        );
        if (choice is not (0 or 1))
            return false;

        if (await server.SetPullRebaseAsync(choice == 1, repo.Path) is Error e)
            return new Error("Failed to save how to pull", e);
        return true;
    }

    static string Commits(int count) => count == 1 ? "1 commit" : $"{count} commits";

    // The rewritten branches Pull All and Push All leave to be pulled or pushed one by one
    static string Rewritten(IReadOnlyList<string> names, string verb = "pull") =>
        names.Count == 1
            ? $"{Names(names)} was rewritten by a force push: {verb} it on its own"
            : $"{Names(names)} were rewritten by a force push: {verb} each on its own";

    // A push that would put back what a force push dropped is asked first, see ForcePushes.IsPushedBack.
    // A diverged one is not: git refuses a plain push of it, and the force push is asked of already.
    // False when the user cancelled.
    bool IsPushConfirmed(Branch branch) =>
        !IsPushedBack(branch)
        || UI.ErrorMessage("Push Warning", ForcePushes.PushBackWarning(RewriteOf(branch)!), 1, "Push", "Cancel") == 0;

    bool IsPushedBack(Branch branch) => RewriteOf(branch) is RemoteRewrite r && ForcePushes.IsPushedBack(r);

    RemoteRewrite? RewriteOf(Branch branch) => ForcePushes.RewriteOf(repo.Repo, branch);

    // The name a branch is shown with, its local one for a local and remote pair: a branch menu and
    // a highlighted branch give the pair by its primary name, which is the remote's, 'origin/main'
    string NiceName(string name)
    {
        if (!repo.Repo.BranchByName.TryGetValue(name, out var branch))
            return name;
        if (branch.LocalName != "" && repo.Repo.BranchByName.TryGetValue(branch.LocalName, out var local))
            branch = local;
        return branch.NiceNameUnique;
    }

    // What was pushed, and that the changes were not, since that is easy to assume
    string Pushed(string what) =>
        repo.Repo.Status.IsOk ? $"Pushed {what}" : $"Pushed {what}; the uncommitted changes stay local";

    // Up to three names, which is what fits a status line, and otherwise how many
    internal static string Names(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count <= 3 ? string.Join(", ", list.Select(n => $"'{n}'")) : $"{list.Count} branches";
    }

    public bool CanPush() => CanPush(repo.Repo);

    public bool CanPushCurrentBranch() => CanPushCurrentBranch(repo.Repo);

    public bool CanPull() => CanPull(repo.Repo);

    public bool CanPullCurrentBranch() => CanPullCurrentBranch(repo.Repo);

    // The rules for what can be pushed and pulled are plain functions of the shown repo, so they
    // are testable without a view. Note that a diverged branch (both local and remote only
    // commits) can neither be pushed nor be part of 'push all branches', since git would reject
    // it as non fast-forward.
    // Push and Pull of one branch, the one a branch menu is for or the one highlighted when 'p' or
    // 'u' is pressed: one rule for both, so the key does what the menu item says. A branch not yet
    // on origin can be pushed, which publishes it. The current branch is pulled with 'git pull',
    // which merges, so it can be pulled even when diverged; any other branch is updated with a
    // fetch, which only fast-forwards, and not at all while it is checked out in another worktree.
    internal static bool CanPushBranch(Repo repo, Branch b) =>
        (b.HasLocalOnly || (!b.IsRemote && b.PullMergeParentBranchName == "")) && !repo.Status.IsMerging;

    internal static string WhyNoPushBranch(Repo repo, Branch b) =>
        repo.Status.IsMerging ? Why.InProgress : $"Nothing to push on '{b.NiceNameUnique}'";

    // A branch whose remote branch a force push rewrote is pulled by taking the new version, so one
    // that is not checked out can be pulled when it has no commits of its own to move onto it. One a
    // force push only dropped commits from has that to pull, with nothing new on origin.
    internal static bool CanPullBranch(Repo repo, Branch b) =>
        (b.HasRemoteOnly || ForcePushes.RewriteOf(repo, b) != null)
        && repo.Status.IsOk
        && (IsCurrent(b) || !b.HasLocalOnly || ForcePushes.RewriteOf(repo, b) is { OwnCount: 0 })
        && repo.WorktreePathOf(b) == "";

    internal static string WhyNoPullBranch(Repo repo, Branch b) =>
        !b.HasRemoteOnly && ForcePushes.RewriteOf(repo, b) == null ? $"Nothing to pull on '{b.NiceNameUnique}'"
        : !repo.Status.IsOk ? Why.Changes
        : repo.WorktreePathOf(b) != "" ? Why.InWorktree(b)
        : ForcePushes.RewriteOf(repo, b) is RemoteRewrite rewrite ? ForcePushes.WhyNoPull(rewrite)
        : "It has commits of its own too: switch to it, and pull (u) merges the two";

    static bool IsCurrent(Branch b) => b.IsCurrent || b.IsLocalCurrent;

    internal static bool CanPush(Repo repo) =>
        !repo.Status.IsMerging && repo.ViewBranches.Any(b => b.HasLocalOnly && !b.HasRemoteOnly);

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

        return !repo.Status.IsMerging && branch != null && branch.HasLocalOnly;
    }

    internal static bool CanPull(Repo repo) =>
        repo.Status.IsOk && repo.ViewBranches.Any(b => b.HasRemoteOnly || ForcePushes.RewriteOf(repo, b) != null);

    internal static bool CanPullCurrentBranch(Repo repo)
    {
        var branch = repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null)
            return false;

        if (branch.RemoteName == "")
            return false; // No remote branch to pull

        var remoteBranch = repo.BranchByName[branch.RemoteName];
        return repo.Status.IsOk
            && remoteBranch != null
            && (remoteBranch.HasRemoteOnly || ForcePushes.RewriteOf(repo, branch) != null);
    }

    // Whether origin has commits for the current branch, whether or not the changes let it be pulled
    internal static bool IsCurrentBranchBehind(Repo repo) =>
        repo.ViewBranches.FirstOrDefault(b => b.IsCurrent) is Branch branch
        && branch.RemoteName != ""
        && repo.BranchByName.TryGetValue(branch.RemoteName, out var remote)
        && remote.HasRemoteOnly;

    // The branches 'push all branches' pushes, i.e. one row per branch (a branch and its remote
    // share their primary name)
    internal static IEnumerable<Branch> BranchesToPush(Repo repo) =>
        repo.ViewBranches.Where(b => b.HasLocalOnly && !b.HasRemoteOnly).DistinctBy(b => b.PrimaryName);

    // The branches 'pull all branches' pulls, i.e. the remote branches that are behind only and
    // are not the current branch, which has already been pulled by then. A diverged branch is left
    // out for the same reason BranchesToPush leaves one out: a branch that is not current is
    // pulled with 'git fetch origin <b>:<b>', which git rejects as non fast-forward. Merging the
    // two sides means switching to the branch, so DivergedBranchesToPull reports those instead.
    // A branch checked out in another worktree is left out as well: git refuses to move it from
    // here, that worktree has to pull it.
    internal static IEnumerable<Branch> BranchesToPull(Repo repo, string currentRemoteName) =>
        repo
            .ViewBranches.Where(b =>
                b.Name != currentRemoteName
                && b.IsRemote
                && !b.IsLocalCurrent
                && !b.IsCurrent
                && b.HasRemoteOnly
                && !b.HasLocalOnly
                && repo.WorktreePathOf(b) == ""
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
                && repo.WorktreePathOf(b) == ""
            )
            .DistinctBy(b => b.PrimaryName);

    // The rewritten remote branches 'pull all branches' pulls by taking the new version, or leaves to
    // be pulled on their own: the same branches as DivergedBranchesToPull, and the ones only ahead,
    // which a force push that only dropped commits leaves
    internal static IEnumerable<RemoteRewrite> RewritesToPull(Repo repo, string currentRemoteName) =>
        repo
            .ViewBranches.Where(b =>
                b.Name != currentRemoteName
                && b.IsRemote
                && !b.IsLocalCurrent
                && !b.IsCurrent
                && b.HasLocalOnly
                && repo.WorktreePathOf(b) == ""
            )
            .Select(b => ForcePushes.RewriteOf(repo, b))
            .OfType<RemoteRewrite>()
            .DistinctBy(r => r.BranchName);

    // Said out loud rather than passed over in silence: a diverged branch keeps its '▼' marker
    // after an update of all branches, which without this looks like the update having failed.
    static void ShowDivergedMessage(IReadOnlyList<Branch> diverged)
    {
        var names = string.Join("\n", diverged.Select(b => $"  {b.NiceNameUnique}"));
        UI.InfoMessage(
            "Pull All Branches",
            "These branches have both local and remote commits, which an update of all\n"
                + "branches cannot merge, since it only fast-forwards a branch it is not on.\n"
                + $"Switch to the branch and pull it to merge:\n\n{names}"
        );
    }

    void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    void RefreshAndFetch(string addName = "", string commitId = "") => repoView.RefreshAndFetch(addName, commitId);

    void Do(Func<Task<Result>> action) => CommandRunner.Do(progress, status, repo, action);
}
