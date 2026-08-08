using gmd.Common;
using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IBranchCommands
{
    void ShowBranch(string name, bool includeAmbiguous, ShowBranches show = ShowBranches.Specified, int count = 1);
    void ShowBranch(string name, string showCommitId);
    void HideBranch(string name, bool hideAllBranches = false);

    void SwitchTo(string branchName);
    void SwitchToCommit();

    void DiffWithOtherBranch(string name, bool isFromCurrentCommit, bool isSwitchOrder);
    void DiffBranchesBranch(string branchName1, string branchName2);

    void CreateBranch();
    void CreateBranchFromBranch(string name);
    void CreateBranchFromCommit();
    void RenameBranch(string name);
    void DeleteBranch(string name);
    void MergeBranch(string name);
    void MergeToBranch(string targetName);
    void RebaseBranchOnto(string onto);

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

    void SetBranchManuallyAsync();
    void MoveBranch(string commonName, string otherCommonName, int delta);
    void ChangeBranchColor(string brandName);
}

// The branch commands the menus and keys call. Creating/deleting and pushing/pulling are their
// own classes below this one, since they are the two large groups; this class holds the rest,
// i.e. showing, switching, diffing, merging and how a branch is shown.
class BranchCommands : IBranchCommands
{
    readonly IViewRepo repo;
    readonly IProgress progress;
    readonly IRepoView repoView;
    readonly IServer server;
    readonly IDiffView diffView;
    readonly IBranchColorService branchColorService;
    readonly ISetBranchDlg setBranchDlg;
    readonly IRepoConfig repoConfig;
    readonly IBranchCreateCommands createCmds;
    readonly IBranchPushPullCommands pushPullCmds;

    public BranchCommands(
        IViewRepo repo,
        IProgress progress,
        IRepoView repoView,
        IServer server,
        IDiffView diffView,
        IBranchColorService branchColorService,
        ISetBranchDlg setBranchDlg,
        IRepoConfig repoConfig,
        Func<IViewRepo, IRepoView, IBranchCreateCommands> newCreateCommands,
        Func<IViewRepo, IRepoView, IBranchPushPullCommands> newPushPullCommands
    )
    {
        this.repo = repo;
        this.progress = progress;
        this.repoView = repoView;
        this.server = server;
        this.diffView = diffView;
        this.branchColorService = branchColorService;
        this.setBranchDlg = setBranchDlg;
        this.repoConfig = repoConfig;
        this.createCmds = newCreateCommands(repo, repoView);
        this.pushPullCmds = newPushPullCommands(repo, repoView);
    }

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    public void RefreshAndCommit(
        string addName = "",
        string commitId = "",
        IReadOnlyList<Server.Commit>? commits = null
    ) => repoView.RefreshAndCommit(addName, commitId, commits);

    public void RefreshAndFetch(string addName = "", string commitId = "") =>
        repoView.RefreshAndFetch(addName, commitId);

    // Creating and deleting branches
    public void CreateBranch() => createCmds.CreateBranch();

    public void CreateBranchFromBranch(string name) => createCmds.CreateBranchFromBranch(name);

    public void CreateBranchFromCommit() => createCmds.CreateBranchFromCommit();

    public void RenameBranch(string name) => createCmds.RenameBranch(name);

    public void DeleteBranch(string name) => createCmds.DeleteBranch(name);

    // Pushing and pulling branches
    public void PushCurrentBranch() => pushPullCmds.PushCurrentBranch();

    public void PushBranch(string name) => pushPullCmds.PushBranch(name);

    public void PushAllBranches() => pushPullCmds.PushAllBranches();

    public void PublishCurrentBranch() => pushPullCmds.PublishCurrentBranch();

    public void PullCurrentBranch() => pushPullCmds.PullCurrentBranch();

    public void PullBranch(string name) => pushPullCmds.PullBranch(name);

    public void PullAllBranches() => pushPullCmds.PullAllBranches();

    public bool CanPushCurrentBranch() => pushPullCmds.CanPushCurrentBranch();

    public bool CanPush() => pushPullCmds.CanPush();

    public bool CanPull() => pushPullCmds.CanPull();

    public bool CanPullCurrentBranch() => pushPullCmds.CanPullCurrentBranch();

    public void ShowBranch(
        string name,
        bool includeAmbiguous,
        ShowBranches show = ShowBranches.Specified,
        int count = 1
    )
    {
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
        SetRepo(newRepo, name);
    }

    public void ShowBranch(string name, string showCommitId)
    {
        Repo newRepo = server.ShowBranch(repo.Repo, name, false);
        SetRepoAttCommit(newRepo, showCommitId);
    }

    public void HideBranch(string name, bool hideAllBranches = false)
    {
        Repo newRepo = server.HideBranch(repo.Repo, name, hideAllBranches);
        SetRepo(newRepo);
    }

    public void SwitchTo(string branchName) =>
        Do(async () =>
        {
            if (!Try(out var e, await server.SwitchToAsync(repo.Repo, branchName)))
            {
                return R.Error($"Failed to switch to {branchName}", e);
            }

            Refresh(branchName);
            return R.Ok;
        });

    public void MergeBranch(string branchName) =>
        Do(async () =>
        {
            if (!Try(out var commits, out var e, await server.MergeBranchAsync(repo.Repo, branchName)))
                return R.Error($"Failed to merge branch {branchName}", e);

            RefreshAndCommit("", "", commits);
            return R.Ok;
        });

    // Merges the current branch into some other branch, the opposite direction of MergeBranch.
    // Git can only merge into the checked out branch, so this switches to the target, merges,
    // commits there and switches back. Anything that leaves the working folder dirty (a conflict,
    // or a cancelled commit) stops halfway and leaves the user on the target branch, which is
    // where the merge has to be finished, and is also the only place git would let them.
    public void MergeToBranch(string targetName) =>
        Do(async () =>
        {
            var serverRepo = repo.Repo;
            var sourceName = serverRepo.CurrentBranch().Name;
            var source = serverRepo.CurrentBranch().ShortNiceUniqueName();

            if (!Try(out var commits, out var e, await server.MergeToBranchAsync(serverRepo, targetName)))
            { // Left where it stopped, i.e. on the target if the merge conflicted, so show that
                await repoView.RefreshAsync(targetName);
                return R.Error($"Failed to merge '{source}' into '{targetName}'", e);
            }

            // Every refresh here names the target, since the branch HEAD just moved to is not
            // necessarily one the user had shown, and the commit dialog is drawn from the view
            await repoView.RefreshAsync(targetName);

            // The commit commands of the refreshed view, since the merge replaced the repo
            // snapshot, and the dialog is seeded from the merge message git just wrote
            if (!Try(out var result, out e, await repoView.ViewRepo.CommitCmds.CommitAsync(false, commits)))
                return R.Error($"Failed to commit the merge on '{targetName}'", e);

            if (result == CommitResult.Cancelled)
            { // The merge is still staged, and git cannot check out over it
                await repoView.RefreshAsync(targetName);
                UI.InfoMessage("Merge", $"The merge was not committed, so you are still on '{targetName}'.");
                return R.Ok;
            }

            if (!Try(out e, await server.SwitchToAsync(serverRepo, sourceName)))
                return R.Error($"Merged '{source}' into '{targetName}', but failed to switch back", e);

            await repoView.RefreshAsync(sourceName);

            // Nothing was staged, so the target already had everything the source has
            if (result == CommitResult.NothingToCommit)
                UI.InfoMessage("Merge", $"'{targetName}' is already up to date with '{source}'.");

            return R.Ok;
        });

    public void RebaseBranchOnto(string onto) =>
        Do(async () =>
        {
            var cb = repo.Repo.CurrentBranch();
            if (cb.RemoteName != "")
            {
                var rcb = repo.Repo.BranchByName[cb.RemoteName];

                if (cb.TipId == rcb.TipId && rcb.BottomId == rcb.TipId) { }
            }

            if (!Try(out var e, await server.RebaseBranchAsync(repo.Repo, onto)))
                return R.Error($"Failed to rebase branch {onto}", e);

            Refresh();
            return R.Ok;
        });

    public void DiffBranchesBranch(string branchName1, string branchName2) =>
        Do(async () =>
        {
            string message = "";
            var branch1 = repo.Repo.BranchByName[branchName1];
            var branch2 = repo.Repo.BranchByName[branchName2];

            var sha1 = branch1.TipId;
            var sha2 = branch2.TipId;
            if (sha1 == Repo.UncommittedId || sha2 == Repo.UncommittedId)
                return R.Error("Cannot diff while uncommitted changes");

            message = $"Diff '{branch1.NiceNameUnique}' to '{branch2.NiceNameUnique}'";

            var reload = DiffReloads.Single(n => server.GetPreviewMergeDiffAsync(sha2, sha1, message, n, repo.Path));
            if (!Try(out var diffs, out var e, await reload(DiffContext.Default)))
            {
                return R.Error($"Failed to get diff", e);
            }

            diffView.Show(diffs[0], sha1, repo.Path, reload);
            return R.Ok;
        });

    public void DiffWithOtherBranch(string branchName, bool isFromCurrentCommit, bool isSwitchOrder) =>
        Do(async () =>
        {
            string message = "";
            var branch = repo.Repo.BranchByName[branchName];
            var sha1 = branch.TipId;
            var sha2 = isFromCurrentCommit ? repo.RowCommit.Sid : repo.Repo.CurrentBranch().TipId;
            if (sha2 == Repo.UncommittedId)
                return R.Error("Cannot diff while uncommitted changes");

            if (isSwitchOrder)
            {
                (sha2, sha1) = (sha1, sha2);
                message = $"Diff '{branch.NiceName}' with '{repo.Repo.CurrentBranch().NiceName}'";
            }
            else
            {
                message = $"Diff '{repo.Repo.CurrentBranch().NiceName}' with '{branch.NiceName}'";
            }

            var reload = DiffReloads.Single(n => server.GetPreviewMergeDiffAsync(sha1, sha2, message, n, repo.Path));
            if (!Try(out var diffs, out var e, await reload(DiffContext.Default)))
            {
                return R.Error($"Failed to get diff", e);
            }

            diffView.Show(diffs[0], sha1, repo.Path, reload);
            return R.Ok;
        });

    public void ChangeBranchColor(string brandName)
    {
        var b = repo.Repo.BranchByName[brandName];
        if (b.IsMainBranch)
            return;

        branchColorService.ChangeColor(repo.Repo, b);
        Refresh();
    }

    public void SetBranchManuallyAsync() =>
        Do(async () =>
        {
            var commit = repo.RowCommit;
            if (commit.IsUncommitted)
                return R.Error($"Not a valid commit");

            var branch = repo.Repo.BranchByName[commit.BranchName];

            var possibleBranches = server.GetPossibleBranchNames(repo.Repo, commit.Id, 20);

            if (
                !Try(
                    out var name,
                    setBranchDlg.Show(commit.Sid, commit.IsBranchSetByUser, branch.NiceName, possibleBranches)
                )
            )
                return R.Ok;

            if (name != "")
            {
                if (!Try(out var e, await server.SetBranchManuallyAsync(repo.Repo, commit.Id, name ?? "")))
                {
                    return R.Error($"Failed to set branch name manually", e);
                }
            }
            else if (commit.IsBranchSetByUser)
            { // name is empty, lets unset name (if set)
                if (!Try(out var ee, await server.UnresolveAmbiguityAsync(repo.Repo, commit.Id)))
                {
                    return R.Error($"Failed to unresolve ambiguity", ee);
                }
            }

            Refresh();
            return R.Ok;
        });

    public void MoveBranch(string commonName, string otherCommonName, int delta) =>
        Do(async () =>
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
            return R.Ok;
        });

    public void SwitchToCommit() =>
        Do(async () =>
        {
            var commit = repo.RowCommit;
            if (!Try(out var e, await server.SwitchToCommitAsync(commit.Id, repo.Path)))
            {
                return R.Error($"Failed to switch to commit {commit.Id}", e);
            }

            Refresh();
            return R.Ok;
        });

    void SetRepo(Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);

    void SetRepoAttCommit(Server.Repo newRepo, string commitId) => repoView.UpdateRepoToAtCommit(newRepo, commitId);

    void Do(Func<Task<R>> action) => CommandRunner.Do(progress, action);
}
