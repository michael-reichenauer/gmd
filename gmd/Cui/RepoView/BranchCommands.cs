using gmd.Common;
using gmd.Cui.Branches;
using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IBranchCommands
{
    void ShowBranch(string name, bool includeAmbiguous, ShowBranches show = ShowBranches.Specified, int count = 1);
    void ShowBranch(string name, string showCommitId);
    void FindBranch(string text);
    void HideBranch(string name, bool hideAllBranches = false);
    void UndoShowOrHide();
    void ShowSearchMatch(int direction);
    void MarkHiddenNewsSeen(IReadOnlyList<HiddenBranchNews> looked);

    void SwitchTo(string branchName);
    void SwitchToCommit();

    void DiffBranchesBranch(string branchName1, string branchName2);

    void MergeBranch(string name);
    void MergeToBranch(string targetName);
    void RebaseBranchOnto(string onto);

    void SetBranchManuallyAsync();
    void MoveBranch(string commonName, string otherCommonName, int delta);
    void ChangeBranchColor(string brandName);
}

// The branch commands that show, switch to, diff and merge branches, and set how a branch is shown.
// Creating, renaming and deleting branches, pushing and pulling, the worktrees and undoing are
// command classes of their own beside this one (BranchCreateCommands, BranchPushPullCommands,
// WorktreeCommands, UndoCommands), all in the CommandContext.
class BranchCommands : IBranchCommands
{
    readonly CommandContext context;
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly IRepoView repoView;
    readonly IServer server;
    readonly IDiffView diffView;
    readonly IBranchColorService branchColorService;
    readonly ISetBranchDlg setBranchDlg;
    readonly IRepoConfigService repoConfig;
    readonly IFindBranchDlg findBranchDlg;

    public BranchCommands(
        CommandContext context,
        IDiffView diffView,
        IBranchColorService branchColorService,
        ISetBranchDlg setBranchDlg,
        IFindBranchDlg findBranchDlg,
        IRepoConfigService repoConfig
    )
    {
        this.context = context;
        this.progress = context.Progress;
        this.status = context.Status;
        this.repoView = context.View;
        this.server = context.Server;
        this.diffView = diffView;
        this.branchColorService = branchColorService;
        this.setBranchDlg = setBranchDlg;
        this.findBranchDlg = findBranchDlg;
        this.repoConfig = repoConfig;
    }

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    public void RefreshAndCommit(
        string addName = "",
        string commitId = "",
        IReadOnlyList<Server.Commit>? commits = null
    ) => repoView.RefreshAndCommit(addName, commitId, commits);

    public void ShowBranch(
        string name,
        bool includeAmbiguous,
        ShowBranches show = ShowBranches.Specified,
        int count = 1
    )
    {
        var repo = context.ViewRepo;
        var totalCount = 0;
        if (show == ShowBranches.AllActive)
            totalCount = repo.Repo.AllBranches.Count(b => b.IsGitBranch);
        if (show == ShowBranches.AllActiveAndDeleted)
            totalCount = repo.Repo.AllBranches.Count;

        if (totalCount > 20)
        {
            if (UI.InfoMessage("Show Branches", $"Do you want to show {totalCount} branches?", 1, ["Yes", "No"]) != 0)
            {
                return;
            }
        }

        Repo newRepo = server.ShowBranch(repo.Repo, name, includeAmbiguous, show, count);
        var what = show switch
        {
            ShowBranches.AllRecent => $"{count} More Recent",
            ShowBranches.AllActive => "All Active",
            ShowBranches.AllActiveAndDeleted => "All Active and Deleted",
            _ => Quoted(name),
        };
        RecordShown(newRepo, "Show", what, show == ShowBranches.Specified ? name : "");
        SetRepo(newRepo, name);
    }

    public void ShowBranch(string name, string showCommitId)
    {
        var repo = context.ViewRepo;
        Repo newRepo = server.ShowBranch(repo.Repo, name, false);
        RecordShown(newRepo, "Show", Quoted(name), name);
        SetRepoAttCommit(newRepo, showCommitId);
    }

    // Finds a branch by name, starting from what was typed in the Show Branch menu, and shows it, as
    // picking it in that menu does. A branch already shown is scrolled to.
    public void FindBranch(string text)
    {
        var repo = context.ViewRepo;
        if (findBranchDlg.Show(repo.Repo, text) is not Server.Branch branch)
            return;

        // Shown in the repo as it is now, which ShowBranch reads: the log goes on refreshing under the
        // dialog, e.g. after a fetch, and showing the branch in the repo from before it would put that
        // back
        ShowBranch(branch.Name, false);
    }

    // Hides a branch, and those made from it, but never the main branch, which is always shown: Enter
    // on a commit of main alone and the menus reach here too
    public void HideBranch(string name, bool hideAllBranches = false)
    {
        var repo = context.ViewRepo;
        if (!hideAllBranches && repo.Repo.BranchByName[name] is { IsMainBranch: true } main)
        {
            status.Notice(Why.AlwaysShown(main));
            return;
        }

        Repo newRepo = server.HideBranch(repo.Repo, name, hideAllBranches);
        RecordShown(newRepo, "Hide", hideAllBranches ? "All Branches" : Quoted(name), hideAllBranches ? "" : name);
        SetRepo(newRepo);
    }

    // Goes back to the branches shown before the last show or hide. Undoing a hide of one branch
    // scrolls to it, as showing it does, since it is what the user wanted back.
    public void UndoShowOrHide()
    {
        var repo = context.ViewRepo;
        if (repo.ShownHistory.Undo() is not ShownChange change)
        {
            status.Notice("No branch has been shown or hidden to undo");
            return;
        }

        SetRepo(server.SetShownBranches(repo.Repo, change.Before), change.IsHide ? change.BranchName : "");
        status.Info($"Undid {change}");
    }

    // Steps to the next match of the last search, 1 down and -1 up, and shows it in the log as
    // picking it in the search does, so its branch is shown too, and Backspace hides it again
    public void ShowSearchMatch(int direction)
    {
        var repo = context.ViewRepo;
        var search = repo.SearchMatches;
        if (!search.IsActive)
        {
            status.Notice("No search to go on with: search with f and pick a commit first");
            return;
        }

        // A match a refresh has taken away, e.g. by a rebase, is stepped past
        while (search.Step(direction) is string id)
        {
            if (!repo.Repo.CommitById.TryGetValue(id, out var commit))
                continue;

            ShowBranch(commit.BranchName, commit.Id);
            status.Info($"Match {search.Number} of {search.Count} for '{search.Filter}'");
            return;
        }

        status.Notice(
            direction > 0
                ? $"No more matches for '{search.Filter}' below: ⇧n goes back up"
                : $"No more matches for '{search.Filter}' above: n goes down"
        );
    }

    public void MarkHiddenNewsSeen(IReadOnlyList<HiddenBranchNews> looked) => repoView.MarkHiddenNewsSeen(looked);

    // A show or hide the user asked for, so that Backspace can undo it
    void RecordShown(Repo newRepo, string verb, string what, string name) =>
        context.ViewRepo.ShownHistory.Add(BranchNames(context.ViewRepo.Repo), BranchNames(newRepo), verb, what, name);

    static IReadOnlyList<string> BranchNames(Repo repo) => repo.ViewBranches.Select(b => b.Name).ToList();

    string Quoted(string name) =>
        context.ViewRepo.Repo.BranchByName.TryGetValue(name, out var branch)
            ? $"'{branch.NiceNameUnique}'"
            : $"'{name}'";

    // A branch checked out in another worktree cannot be checked out here, git refuses, so the
    // folder it is checked out in is opened instead. Done here rather than in the menu, since the
    // S key and a double click switch without going through the menu.
    public void SwitchTo(string branchName) =>
        Do(async repo =>
        {
            if (repo.Repo.BranchByName.TryGetValue(branchName, out var branch))
            {
                var worktreePath = repo.Repo.WorktreePathOf(branch);
                if (worktreePath != "")
                    return await OpenWorktreeAsync(worktreePath);
            }

            if (await server.SwitchToAsync(repo.Repo, branchName) is Error e)
            {
                if (IsBlockedByChanges(e))
                    return await SwitchWithStashAsync(branchName);
                return new Error($"Failed to switch to {branchName}", e);
            }

            Refresh(branchName);
            return Result.Ok;
        });

    // Git carries uncommitted changes over to the branch switched to, unless they would be
    // overwritten there, when it refuses. That refusal is where the switch offers to take the
    // changes along by way of the stash: stash them, switch, and put them back.
    static bool IsBlockedByChanges(Error e) =>
        e.AllMessages() is var text
        && (text.Contains("would be overwritten by checkout") || text.Contains("stash them before you switch"));

    async Task<Result> SwitchWithStashAsync(string branchName)
    {
        var repo = context.ViewRepo;
        var name = repo.Repo.BranchByName.TryGetValue(branchName, out var b) ? b.NiceNameUnique : branchName;
        var question =
            $"Your uncommitted changes would be overwritten by switching to '{name}'.\n\n"
            + "Stash them, switch, and put them back on it?";
        if (UI.InfoMessage("Switch Branch", question, 0, ["Stash and Switch", "Cancel"]) != 0)
            return Result.Ok;

        var stashMessage = $"Switching to {name}";
        if (await server.StashAsync(stashMessage, repo.Path) is Error stashError)
            return new Error("Failed to stash the changes", stashError);

        if (await server.SwitchToAsync(repo.Repo, branchName) is Error switchError)
        { // Back where it was, with the changes put back, or said to be in the stash if they could not be
            var popped = await server.StashPopAsync("stash@{0}", repo.Path);
            Refresh();
            return new Error(
                popped is Error
                    ? $"Failed to switch to {name}, and to put the changes back: they are in the stash, '{stashMessage}'"
                    : $"Failed to switch to {name}",
                switchError
            );
        }

        if (await server.StashPopAsync("stash@{0}", repo.Path) is Error)
        { // Git keeps the stash when putting it back conflicts, so nothing is lost
            Refresh(branchName);
            UI.InfoMessage(
                "Switch Branch",
                $"Switched to '{name}', but the changes conflict with it where they were put back.\n\n"
                    + $"They are still in the stash '{stashMessage}' as well. Resolve the conflicts\n"
                    + "in the diff of the uncommitted changes, then drop the stash."
            );
            return Result.Ok;
        }

        Refresh(branchName);
        status.Info($"Switched to '{name}', with the changes");
        return Result.Ok;
    }

    // A branch checked out in another worktree is switched to by showing that folder
    Task<Result> OpenWorktreeAsync(string path) => context.WorktreeCmds.OpenWorktreeAsync(path);

    public void MergeBranch(string branchName) =>
        Do(async repo =>
        {
            var commitsResult = await server.MergeBranchAsync(repo.Repo, branchName);
            if (commitsResult is not IReadOnlyList<Commit> commits)
                return new Error($"Failed to merge branch {branchName}", commitsResult.Error);

            // Git merged nothing, so there is nothing to commit either: it did nothing and said nothing
            if (commits.Count == 0)
            {
                var name = repo.Repo.BranchByName.TryGetValue(branchName, out var b)
                    ? b.NiceNameUnique
                    : branchName.Sid();
                return NothingToMerge(repo.Repo.CurrentBranch().ShortNiceUniqueName(), name);
            }

            RefreshAndCommit("", "", commits);
            return Result.Ok;
        });

    static Notice NothingToMerge(string target, string source) =>
        new($"Nothing to merge: '{target}' is already up to date with '{source}'");

    // Merges the current branch into some other branch, the opposite direction of MergeBranch.
    // Git can only merge into the checked out branch, so this switches to the target, merges,
    // commits there and switches back. Anything that leaves the working folder dirty (a conflict,
    // or a cancelled commit) stops halfway and leaves the user on the target branch, which is
    // where the merge has to be finished, and is also the only place git would let them.
    public void MergeToBranch(string targetName) =>
        Do(async repo =>
        {
            var serverRepo = repo.Repo;
            var sourceName = serverRepo.CurrentBranch().Name;
            var source = serverRepo.CurrentBranch().ShortNiceUniqueName();

            var mergeResult = await server.MergeToBranchAsync(serverRepo, targetName);
            if (mergeResult is not IReadOnlyList<Commit> commits)
            { // Left where it stopped, i.e. on the target if the merge conflicted, so show that
                await repoView.RefreshAsync(targetName);
                return new Error($"Failed to merge '{source}' into '{targetName}'", mergeResult.Error);
            }

            // Every refresh here names the target, since the branch HEAD just moved to is not
            // necessarily one the user had shown, and the commit dialog is drawn from the view
            await repoView.RefreshAsync(targetName);

            // On the refreshed repo, which CommitAsync reads, with the dialog seeded from the merge
            // message git just wrote
            var commitResult = await context.CommitCmds.CommitAsync(false, commits);
            if (commitResult is not CommitResult result)
                return new Error($"Failed to commit the merge on '{targetName}'", commitResult.Error);

            if (result == CommitResult.Cancelled)
            { // The merge is still staged, and git cannot check out over it
                await repoView.RefreshAsync(targetName);
                UI.InfoMessage("Merge", $"The merge was not committed, so you are still on '{targetName}'.");
                return Result.Ok;
            }

            if (await server.SwitchToAsync(serverRepo, sourceName) is Error e)
                return new Error($"Merged '{source}' into '{targetName}', but failed to switch back", e);

            await repoView.RefreshAsync(sourceName);

            // Nothing was staged, so the target already had everything the source has
            return result == CommitResult.NothingToCommit ? NothingToMerge(targetName, source) : Result.Ok;
        });

    public void RebaseBranchOnto(string onto) =>
        Do(async repo =>
        {
            var cb = repo.Repo.CurrentBranch();
            if (WhyNotRebaseAndPush(repo.Repo, cb) is var whyNot && whyNot != "")
                return new Notice(whyNot);
            var ontoName = repo.Repo.BranchByName.TryGetValue(onto, out var b) ? b.NiceNameUnique : onto;
            if (cb.RemoteName != "" && !Confirm.RebaseAndPush(cb.NiceNameUnique, ontoName, cb.RemoteName))
                return Result.Ok;

            if (await server.RebaseBranchAsync(repo.Repo, onto) is Error e)
                return new Error($"Failed to rebase branch {onto}", e);

            Refresh();
            return Result.Ok;
        });

    // A rebased branch that is on origin is pushed with force, leased on origin's tip as shown, so a
    // commit origin has and the branch has not would be dropped from origin: the fetch that brought
    // it in moved the lease along with it. Such commits are pulled first. "" when it can be rebased.
    // One rewritten here has only the old version to drop there, which is no reason to pull, and which
    // its own force push replaces first.
    internal static string WhyNotRebaseAndPush(Repo repo, Branch current) =>
        RewrittenHere.Of(repo, current) is LocalRewrite rewrite ? RewrittenHere.WhyNoRebase(rewrite)
        : current.RemoteName != "" && current.BehindCount > 0
            ? $"'{current.RemoteName}' has commits '{current.NiceNameUnique}' has not, which the push would drop: pull them first"
        : "";

    public void DiffBranchesBranch(string branchName1, string branchName2) =>
        Do(async repo =>
        {
            string message = "";
            var branch1 = repo.Repo.BranchByName[branchName1];
            var branch2 = repo.Repo.BranchByName[branchName2];

            var sha1 = branch1.TipId;
            var sha2 = branch2.TipId;
            if (sha1 == Repo.UncommittedId || sha2 == Repo.UncommittedId)
                return new Error("Cannot diff while uncommitted changes");

            message = $"Diff '{branch1.NiceNameUnique}' to '{branch2.NiceNameUnique}'";

            var source = DiffSource.Single(n => server.GetPreviewMergeDiffAsync(sha2, sha1, message, n, repo.Path));
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not CommitDiff[] diffs)
                return new Error("Failed to get diff", diffsResult.Error);

            UI.Post(() => diffView.Show(diffs, source, repo.Path)); // Outside Do, see CommitCommands.ShowDiff
            return Result.Ok;
        });

    public void ChangeBranchColor(string brandName)
    {
        var repo = context.ViewRepo;
        var b = repo.Repo.BranchByName[brandName];
        if (b.IsMainBranch)
            return;

        branchColorService.ChangeColor(repo.Repo, b);
        Refresh();
    }

    public void SetBranchManuallyAsync() =>
        Do(async repo =>
        {
            var commit = repo.RowCommit;
            if (commit.IsUncommitted)
                return new Error($"Not a valid commit");

            var branch = repo.Repo.BranchByName[commit.BranchName];

            var possibleBranches = server.GetPossibleBranchNames(repo.Repo, commit.Id, 20);

            var nameResult = setBranchDlg.Show(commit.Sid, commit.IsBranchSetByUser, branch.NiceName, possibleBranches);
            if (nameResult is not string name)
                return Result.Ok;

            if (name != "")
            {
                if (await server.SetBranchManuallyAsync(repo.Repo, commit.Id, name) is Error e)
                {
                    return new Error($"Failed to set branch name manually", e);
                }
            }
            else if (commit.IsBranchSetByUser)
            { // name is empty, lets unset name (if set)
                if (await server.UnresolveAmbiguityAsync(repo.Repo, commit.Id) is Error ee)
                {
                    return new Error($"Failed to unresolve ambiguity", ee);
                }
            }

            Refresh();
            return Result.Ok;
        });

    public void MoveBranch(string commonName, string otherCommonName, int delta) =>
        Do(async repo =>
        {
            await Task.Yield();

            repoConfig.Set(
                repo.Path,
                s =>
                {
                    // Filter existing branch orders for the two branches
                    var branchOrders = s.BranchOrders.Where(b =>
                        !(b.Branch == commonName && b.Other == otherCommonName)
                        && !(b.Branch == otherCommonName && b.Other == commonName)
                    );

                    // Add this branch order
                    s.BranchOrders = branchOrders
                        .Append(
                            new BranchOrder()
                            {
                                Branch = commonName,
                                Other = otherCommonName,
                                Order = delta,
                            }
                        )
                        .ToList();
                }
            );

            Refresh();
            return Result.Ok;
        });

    public void SwitchToCommit() =>
        Do(async repo =>
        {
            var commit = repo.RowCommit;
            if (await server.SwitchToCommitAsync(commit.Id, repo.Path) is Error e)
            {
                return new Error($"Failed to switch to commit {commit.Id}", e);
            }

            Refresh();
            return Result.Ok;
        });

    void SetRepo(Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);

    void SetRepoAttCommit(Server.Repo newRepo, string commitId) => repoView.UpdateRepoToAtCommit(newRepo, commitId);

    void Do(Func<IViewRepo, Task<Result>> action) => context.Do(action);
}
