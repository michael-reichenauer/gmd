using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// BranchWriteService runs the git commands that change which branches exist and where they point.
// They are the write operations that need the augmented repo to work out what to actually run,
// i.e. which commit a created branch should remember it was branched from, whether a branch that
// git no longer has needs to be recreated first, and whether the local or the remote branch of a
// pair holds the tip to merge or rebase onto.
interface IBranchWriteService
{
    Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd);
    Task<R> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout,
        string wd
    );
    Task<R> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd);
    Task<R> SwitchToAsync(Repo repo, string branchName);
    Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name);
    Task<R> RebaseBranchAsync(Repo repo, string name);
}

class BranchWriteService : IBranchWriteService
{
    readonly IGit git;
    readonly IFileMonitor fileMonitor;
    readonly IMetaDataService metaDataService;

    internal BranchWriteService(IGit git, IFileMonitor fileMonitor, IMetaDataService metaDataService)
    {
        this.git = git;
        this.fileMonitor = fileMonitor;
        this.metaDataService = metaDataService;
    }

    public async Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd)
    {
        Log.Info($"Create branch {newBranchName} ...");
        Commit? currentCommit = null;
        var currentBranch = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {
            currentCommit = repo.CommitById[currentBranch.TipId];
        }

        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.CreateBranchAsync(newBranchName, isCheckout, wd)))
                return e;

            if (currentCommit == null || currentBranch == null)
                return R.Ok;

            // Get the latest meta data
            if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd)))
                return e;

            metaData.SetBranched(currentCommit.Sid, currentBranch.NiceName);
            return await metaDataService.SetMetaDataAsync(wd, metaData);
        }
    }

    public async Task<R> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout,
        string wd
    )
    {
        Log.Info($"Create branch {newBranchName} ...");

        var source = repo.BranchByName[sourceBranch];

        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.CreateBranchFromCommitAsync(newBranchName, source.TipId, isCheckout, wd)))
                return e;

            // Get the latest meta data
            if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd)))
                return e;

            metaData.SetBranched(source.TipId, source.NiceName);
            return await metaDataService.SetMetaDataAsync(wd, metaData);
        }
    }

    public async Task<R> CreateBranchFromCommitAsync(
        Repo repo,
        string newBranchName,
        string sha,
        bool isCheckout,
        string wd
    )
    {
        Log.Info($"Create branch {newBranchName} from {sha} ...");
        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.CreateBranchFromCommitAsync(newBranchName, sha, isCheckout, wd)))
                return e;

            Commit commit = repo.CommitById[sha];
            var branch = repo.BranchByName[commit.BranchName];

            // Get the latest meta data
            if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd)))
                return e;

            metaData.SetBranched(commit.Sid, branch.NiceName);
            return await metaDataService.SetMetaDataAsync(wd, metaData);
        }
    }

    public async Task<R> SwitchToAsync(Repo repo, string branchName)
    {
        var branch = repo.BranchByName[branchName];
        if (branch.IsGitBranch)
        {
            return await git.CheckoutAsync(branchName, repo.Path);
        }

        // Not a git branch so the branch was deleted, lets recreate it
        var tip = repo.CommitById[branch.TipId];

        return await CreateBranchFromCommitAsync(repo, branch.NiceName, tip.Id, true, repo.Path);
    }

    public async Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name)
    {
        if (repo.CommitById.TryGetValue(name, out var commit))
        { // Merging from a commit
            if (!Try(out var e2, await git.MergeBranchAsync(commit.Id, repo.Path)))
                return e2;
            if (!Try(out var commits2, out e2, await git.GetMergeLogAsync(commit.Id, repo.Path)))
                return e2;
            return ToMergeCommits(repo, commits2).ToList();
        }

        var mergeName = YoungestTipName(repo, repo.BranchByName[name]);

        if (!Try(out var e, await git.MergeBranchAsync(mergeName, repo.Path)))
            return e;
        if (!Try(out var commits, out e, await git.GetMergeLogAsync(mergeName, repo.Path)))
            return e;
        return ToMergeCommits(repo, commits).ToList();
    }

    public async Task<R> RebaseBranchAsync(Repo repo, string name)
    {
        using (fileMonitor.Pause())
        {
            var cb = repo.CurrentBranch();
            var primaryCurrent = repo.BranchByName[cb.PrimaryName];
            var oldBase = primaryCurrent.BottomId;

            var newBase = YoungestTipName(repo, repo.BranchByName[name]);

            if (!Try(out var e, await git.RebaseOntoAsync(newBase, $"{oldBase}~", repo.Path)))
                return e;

            if (cb.RemoteName != "")
            { // Current Branch is local branch with a remote branch, push it with force
                if (!Try(out e, await git.PushCurrentBranchAsync(true, repo.Path)))
                    return e;
            }

            return R.Ok;
        }
    }

    // A local branch and its remote branch are one branch to the user, but they can have
    // different tips, so merging or rebasing onto one of them has to use the youngest of the two.
    internal static string YoungestTipName(Repo repo, Branch branch)
    {
        var tip = repo.CommitById[branch.TipId];

        if (branch.LocalName != "")
        { // Branch is a remote branch with an existing local branch, which might have a younger tip
            var localBranch = repo.BranchByName[branch.LocalName];
            var localTip = repo.CommitById[localBranch.TipId];
            if (localTip.AuthorTime >= tip.AuthorTime)
            { // The local branch is younger or same, use that.
                return localBranch.Name;
            }
        }
        else if (branch.RemoteName != "")
        { // Branch is a local branch with an existing remote branch, which might have a younger tip
            var remoteBranch = repo.BranchByName[branch.RemoteName];
            var remoteTip = repo.CommitById[remoteBranch.TipId];
            if (remoteTip.AuthorTime >= tip.AuthorTime)
            { // The remote branch is younger or same, use that.
                return remoteBranch.Name;
            }
        }

        return branch.Name;
    }

    IEnumerable<Commit> ToMergeCommits(Repo repo, IReadOnlyList<Git.Commit> commits)
    {
        if (commits.Count == 0)
            return [];
        var branchName = repo.CommitById[commits[0].Id].BranchPrimaryName;

        return commits.Select(c => repo.CommitById[c.Id]).Where(c => c.BranchPrimaryName == branchName);
    }
}
