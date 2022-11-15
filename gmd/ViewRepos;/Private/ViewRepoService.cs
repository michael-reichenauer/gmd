using gmd.Git;
using gmd.ViewRepos.Private.Augmented;

namespace gmd.ViewRepos.Private;

[SingleInstance]
class ViewRepoService : IViewRepoService
{
    private readonly IGit git;
    private readonly IAugmentedRepoService augmentedRepoService;
    private readonly IConverter converter;
    private readonly IViewRepoCreater viewRepoCreater;

    public ViewRepoService(
        IGit git,
        IAugmentedRepoService augmentedRepoService,
        IConverter converter,
        IViewRepoCreater viewRepoCreater)
    {
        this.git = git;
        this.augmentedRepoService = augmentedRepoService;
        this.converter = converter;
        this.viewRepoCreater = viewRepoCreater;

        augmentedRepoService.RepoChange += e => RepoChange?.Invoke(e);
        augmentedRepoService.StatusChange += e => StatusChange?.Invoke(e);
    }

    public event Action<ChangeEvent>? RepoChange;
    public event Action<ChangeEvent>? StatusChange;


    public async Task<R<Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        if (!Try(out var augmentedRepo, out var e,
            await augmentedRepoService.GetRepoAsync(path))) return e;

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, showBranches);
    }

    public async Task<R<Repo>> GetUpdateStatusRepoAsync(Repo repo)
    {
        var branches = repo.Branches.Select(b => b.Name).ToArray();

        if (!Try(out var augmentedRepo, out var e,
            await augmentedRepoService.UpdateStatusRepoAsync(repo.AugmentedRepo)))
        {
            return e;
        }

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, branches);
    }


    public IReadOnlyList<Branch> GetAllBranches(Repo repo) =>
        converter.ToBranches(repo.AugmentedRepo.Branches);


    public IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId)
    {
        if (commitId == Repo.UncommittedId)
        {
            return new List<Branch>();
        }

        var c = repo.AugmentedRepo.CommitById[commitId];
        var ids = c.ChildIds.Concat(c.ParentIds);
        var branches = ids.Select(id =>
        {
            var cc = repo.AugmentedRepo.CommitById[id];

            // Get not shown branches of either child or parent commits.
            if (!repo.BranchByName.TryGetValue(cc.BranchName, out var branch))
            {
                return repo.AugmentedRepo.BranchByName[cc.BranchName];
            }

            return null;
        })
        .Where(b => b != null)
        .Cast<Augmented.Branch>();

        return converter.ToBranches(branches.ToList());
    }


    public Repo ShowBranch(Repo repo, string branchName)
    {
        var branchNames = repo.Branches.Select(b => b.Name).Append(branchName).ToArray();
        return viewRepoCreater.GetViewRepoAsync(repo.AugmentedRepo, branchNames);
    }


    public Repo HideBranch(Repo repo, string name)
    {
        Log.Info($"Hide {name}");
        var branch = repo.AugmentedRepo.BranchByName[name];
        if (branch.RemoteName != "")
        {
            branch = repo.AugmentedRepo.BranchByName[branch.RemoteName];
        }

        var branchNames = repo.Branches
            .Where(b => b.Name != branch.Name &&
                !viewRepoCreater.IsFirstAncestorOfSecond(repo.AugmentedRepo, branch, repo.AugmentedRepo.BranchByName[b.Name]))
            .Select(b => b.Name)
            .ToArray();

        Log.Info($"Show names {branchNames}");
        return viewRepoCreater.GetViewRepoAsync(repo.AugmentedRepo, branchNames);
    }


    public async Task<R> CommitAllChangesAsync(string message, string wd) =>
         await git.CommitAllChangesAsync(message, wd);


    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd)
    {
        var t = Timing.Start;
        if (!Try(out var gitCommitDiff, out var e, await git.GetCommitDiffAsync(commitId, wd))) return e;

        var diff = converter.ToCommitDiff(gitCommitDiff);
        Log.Info($"{t} {diff}");
        return diff;
    }


    public async Task<R<CommitDiff>> GetUncommittedDiff(string wd)
    {
        var t = Timing.Start;
        if (!Try(out var gitCommitDiff, out var e, await git.GetUncommittedDiff(wd))) return e;

        var diff = converter.ToCommitDiff(gitCommitDiff);
        Log.Info($"{t} {diff}");
        return diff;
    }


    public Task<R> PushBranchAsync(string name, string wd) =>
        git.PushBranchAsync(name, wd);

    public Task<R> SwitchToAsync(string branchName, string wd) =>
        git.CheckoutAsync(branchName, wd);

    public Task<R> MergeBranch(string name, string wd) =>
        git.MergeBranch(name, wd);

    public Task<R> CreateBranchAsync(string name, bool isCheckout, string wd) =>
        git.CreateBranchAsync(name, isCheckout, wd);

    public Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd) =>
        git.CreateBranchFromCommitAsync(name, sha, isCheckout, wd);

    public Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        git.DeleteLocalBranchAsync(name, isForced, wd);

    public Task<R> DeleteRemoteBranchAsync(string name, string wd) =>
        git.DeleteRemoteBranchAsync(name, wd);
}

