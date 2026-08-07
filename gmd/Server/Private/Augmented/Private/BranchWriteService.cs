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
    Task<R> RenameBranchAsync(string oldName, string newName, string wd);
    Task<R> SwitchToAsync(Repo repo, string branchName);
    Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name);
    Task<R<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName);
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

    // Renames the local branch. The meta data remembers which branch a commit belongs to by name,
    // so those names are renamed as well, otherwise the old name would reappear as a branch of its
    // own. The remote branch is not renamed here, git has no such command, the caller pushes the
    // new name and deletes the old remote branch instead.
    public async Task<R> RenameBranchAsync(string oldName, string newName, string wd)
    {
        Log.Info($"Rename branch {oldName} to {newName} ...");

        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.RenameBranchAsync(oldName, newName, wd)))
                return e;

            // Get the latest meta data
            if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd)))
                return e;

            metaData.RenameBranch(NiceName(oldName), NiceName(newName));
            return await metaDataService.SetMetaDataAsync(wd, metaData);
        }
    }

    // The meta data stores nice names, i.e. names without the remote prefix
    static string NiceName(string branchName) => branchName.TrimPrefix("origin/");

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

    // Merges the current branch into some other branch, i.e. the opposite direction of
    // MergeBranchAsync. Git can only merge into the branch that is checked out, and there is no
    // way to merge into a branch without a working folder, so the target is checked out first and
    // the merge is left staged there for the caller to commit and then switch back.
    //
    // Anything that fails leaves HEAD where the failure left it, which for a conflict is on the
    // target branch, since that is where the conflict has to be resolved.
    public async Task<R<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName)
    {
        using (fileMonitor.Pause())
        {
            var mergeName = YoungestTipName(repo, repo.BranchByName[repo.CurrentBranch().Name]);

            // The two failures are told apart, since they leave the user in very different places:
            // a failed checkout has not moved HEAD at all, while a conflicting merge has, and the
            // conflict then has to be resolved on the target branch.
            if (!Try(out var e, await SwitchToAsync(repo, targetName)))
                return R.Error($"Failed to switch to '{targetName}'", e);
            if (!Try(out e, await git.MergeBranchAsync(mergeName, repo.Path)))
                return R.Error($"Failed to merge '{mergeName}' while on '{targetName}'", e);

            // Now on the target branch, so this is the commits the merge brings in. An empty list
            // means the target was already up to date, i.e. the merge did nothing.
            if (!Try(out var commits, out e, await git.GetMergeLogAsync(mergeName, repo.Path)))
                return e;

            return ToMergeCommits(repo, commits).ToList();
        }
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

    // The merge log reaches as far back as the merge does, while the shown repo is a log truncated
    // to a max count, so a commit of the merge can be missing from it. Those are skipped rather
    // than looked up blindly, which would throw a KeyNotFoundException past all the R handling.
    IEnumerable<Commit> ToMergeCommits(Repo repo, IReadOnlyList<Git.Commit> commits)
    {
        var mergeCommits = commits
            .Select(c => repo.CommitById.TryGetValue(c.Id, out var commit) ? commit : null)
            .OfType<Commit>()
            .ToList();
        if (mergeCommits.Count == 0)
            return [];

        var branchName = mergeCommits[0].BranchPrimaryName;
        return mergeCommits.Where(c => c.BranchPrimaryName == branchName);
    }
}
