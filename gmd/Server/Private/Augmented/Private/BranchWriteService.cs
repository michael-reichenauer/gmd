using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// See IBranchWriteService
class BranchWriteService : IBranchWriteService
{
    readonly IGit git;
    readonly IMetaDataService metaDataService;

    internal BranchWriteService(IGit git, IMetaDataService metaDataService)
    {
        this.git = git;
        this.metaDataService = metaDataService;
    }

    public async Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout)
    {
        var wd = repo.Path;
        Log.Info($"Create branch {newBranchName} ...");
        Commit? currentCommit = null;
        var currentBranch = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {
            currentCommit = repo.CommitById[currentBranch.TipId];
        }

        if (await git.CreateBranchAsync(newBranchName, isCheckout, wd) is Error e)
            return e;

        if (currentCommit == null || currentBranch == null)
            return Result.Ok;

        return await metaDataService.UpdateMetaDataAsync(
            wd,
            m => m.SetBranched(currentCommit.Id, currentBranch.NiceName)
        );
    }

    public async Task<Result> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout
    )
    {
        var wd = repo.Path;
        Log.Info($"Create branch {newBranchName} ...");

        var source = repo.BranchByName[sourceBranch];

        if (await git.CreateBranchFromCommitAsync(newBranchName, source.TipId, isCheckout, wd) is Error e)
            return e;

        return await metaDataService.UpdateMetaDataAsync(wd, m => m.SetBranched(source.TipId, source.NiceName));
    }

    public async Task<Result> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout)
    {
        var wd = repo.Path;
        Log.Info($"Create branch {newBranchName} from {sha} ...");
        if (await git.CreateBranchFromCommitAsync(newBranchName, sha, isCheckout, wd) is Error e)
            return e;

        // A commit the repo does not have, i.e. one no branch reached until now (a lost commit
        // brought back), has no branch to record the new one as branching out of
        if (!repo.CommitById.TryGetValue(sha, out var commit))
            return Result.Ok;
        var branch = repo.BranchByName[commit.BranchName];

        return await metaDataService.UpdateMetaDataAsync(wd, m => m.SetBranched(commit.Id, branch.NiceName));
    }

    // Renames the local branch. The meta data remembers which branch a commit belongs to by name,
    // so those names are renamed as well, otherwise the old name would reappear as a branch of its
    // own. The remote branch is not renamed here, git has no such command, the caller pushes the
    // new name and deletes the old remote branch instead.
    public async Task<Result> RenameBranchAsync(string oldName, string newName, string wd)
    {
        Log.Info($"Rename branch {oldName} to {newName} ...");

        if (await git.RenameBranchAsync(oldName, newName, wd) is Error e)
            return e;

        return await metaDataService.UpdateMetaDataAsync(wd, m => m.RenameBranch(NiceName(oldName), NiceName(newName)));
    }

    // The meta data stores nice names, i.e. names without the remote prefix
    static string NiceName(string branchName) => branchName.TrimPrefix("origin/");

    public async Task<Result> SwitchToAsync(Repo repo, string branchName)
    {
        var branch = repo.BranchByName[branchName];
        if (branch.IsGitBranch)
        {
            return await git.CheckoutAsync(branchName, repo.Path);
        }

        // Not a git branch so the branch was deleted, lets recreate it
        var tip = repo.CommitById[branch.TipId];

        return await CreateBranchFromCommitAsync(repo, branch.NiceName, tip.Id, true);
    }

    public async Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name)
    {
        if (repo.CommitById.TryGetValue(name, out var commit))
        { // Merging from a commit
            if (await git.MergeBranchAsync(commit.Id, repo.Path) is Error e2)
                return e2;
            var merged = await git.GetMergeLogAsync(commit.Id, repo.Path);
            if (merged is not IReadOnlyList<Git.Commit> commits2)
                return merged.Error;
            return ToMergeCommits(repo, commits2).ToList();
        }

        var mergeName = MergeTipName(repo, repo.BranchByName[name]);

        if (await git.MergeBranchAsync(mergeName, repo.Path) is Error e)
            return e;
        var mergeLog = await git.GetMergeLogAsync(mergeName, repo.Path);
        if (mergeLog is not IReadOnlyList<Git.Commit> commits)
            return mergeLog.Error;
        return ToMergeCommits(repo, commits).ToList();
    }

    // Merges the current branch into some other branch, i.e. the opposite direction of
    // MergeBranchAsync. Git can only merge into the branch that is checked out, and there is no
    // way to merge into a branch without a working folder, so the target is checked out first and
    // the merge is left staged there for the caller to commit and then switch back.
    //
    // Anything that fails leaves HEAD where the failure left it, which for a conflict is on the
    // target branch, since that is where the conflict has to be resolved.
    public async Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName)
    {
        var mergeName = MergeTipName(repo, repo.BranchByName[repo.CurrentBranch().Name]);

        // The two failures are told apart, since they leave the user in very different places:
        // a failed checkout has not moved HEAD at all, while a conflicting merge has, and the
        // conflict then has to be resolved on the target branch.
        if (await SwitchToAsync(repo, targetName) is Error switchError)
            return new Error($"Failed to switch to '{targetName}'", switchError);
        if (await git.MergeBranchAsync(mergeName, repo.Path) is Error mergeError)
            return new Error($"Failed to merge '{mergeName}' while on '{targetName}'", mergeError);

        // Now on the target branch, so this is the commits the merge brings in. An empty list
        // means the target was already up to date, i.e. the merge did nothing.
        var mergeLog = await git.GetMergeLogAsync(mergeName, repo.Path);
        if (mergeLog is not IReadOnlyList<Git.Commit> commits)
            return mergeLog.Error;

        return ToMergeCommits(repo, commits).ToList();
    }

    public async Task<Result> RebaseBranchAsync(Repo repo, string name, bool isPush)
    {
        var cb = repo.CurrentBranch();
        var primaryCurrent = repo.BranchByName[cb.PrimaryName];
        var oldBase = primaryCurrent.BottomId;

        var newBase = MergeTipName(repo, repo.BranchByName[name]);

        if (await git.RebaseOntoAsync(newBase, $"{oldBase}~", repo.Path) is Error e)
            return e;

        if (isPush && cb.RemoteName != "")
        { // Current Branch is local branch with a remote branch, push it with force, over the
            // remote branch as it was before the rebase, i.e. as the user saw it
            var remoteTipId = repo.BranchByName.TryGetValue(cb.RemoteName, out var remote) ? remote.TipId : "";
            if (await git.PushForceAsync(cb.Name, cb.RemoteName, remoteTipId, repo.Path) is Error pushError)
                return new Error(
                    $"Rebased '{cb.NiceName}' onto '{repo.BranchByName[name].NiceName}', but did not push it",
                    pushError
                );
        }

        return Result.Ok;
    }

    // A local branch and its remote branch are one branch to the user, but they can point at
    // different commits, so merging or rebasing onto the branch has to use the one that is ahead,
    // i.e. that has the other's commits as well, as git counts them: the local branch unless it is
    // behind, and the remote branch when only it has commits of its own. When both have, the one
    // with the youngest tip is taken; merging it is what brings the other side in.
    //
    // Author times alone decided before, and a commit keeps its author time when it is cherry picked
    // or rebased, as it does a clock running ahead on another machine: a local branch ahead by such
    // a commit was taken as behind, and the remote branch merged without the local commits.
    internal static string MergeTipName(Repo repo, Branch branch)
    {
        var local =
            !branch.IsRemote ? branch
            : branch.LocalName != "" ? repo.BranchByName[branch.LocalName]
            : null;
        if (local == null || !repo.BranchByName.TryGetValue(local.RemoteName, out var remote))
            return branch.Name;

        if (local.BehindCount == 0)
            return local.Name;
        if (local.AheadCount == 0)
            return remote.Name;

        var localTime = repo.CommitById[local.TipId].AuthorTime;
        var remoteTime = repo.CommitById[remote.TipId].AuthorTime;
        return localTime >= remoteTime ? local.Name : remote.Name;
    }

    // The merge log reaches as far back as the merge does, while the shown repo is a log truncated
    // to a max count, so a commit of the merge can be missing from it. Those are skipped rather
    // than looked up blindly, which would throw a KeyNotFoundException past all the Result handling.
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

    // Which branch a commit is on, as the user chose it, kept in the metadata, which the inference
    // takes before anything else (CommitBranchRules.TryIsBranchSetByUser)
    public async Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setNiceName)
    {
        Log.Info($"Set {commitId.Sid()} to {setNiceName} ...");

        return await metaDataService.UpdateMetaDataAsync(repo.Path, m => m.SetCommitBranch(commitId, setNiceName));
    }

    public async Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName)
    {
        var branch = repo.BranchByName[branchName];
        var ambiguousTip = branch.AmbiguousTipId;
        Log.Info($"Resolve {ambiguousTip.Sid()} of {branchName} to {setHumanName} ...");

        return await metaDataService.UpdateMetaDataAsync(repo.Path, m => m.SetCommitBranch(ambiguousTip, setHumanName));
    }

    public async Task<Result> UnresolveAmbiguityAsync(Repo repo, string commitId)
    {
        return await metaDataService.UpdateMetaDataAsync(repo.Path, m => m.RemoveCommitBranch(commitId));
    }
}
