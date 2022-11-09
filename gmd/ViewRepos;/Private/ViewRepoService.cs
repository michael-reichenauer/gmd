using gmd.Utils.Git;
using gmd.ViewRepos.Private.Augmented;

namespace gmd.ViewRepos.Private;

[SingleInstance]
class ViewRepoService : IViewRepoService
{
    private readonly IGitService gitService;
    private readonly IAugmentedRepoService augmentedRepoService;
    private readonly IConverter converter;
    private readonly IViewRepoCreater viewRepoCreater;

    public ViewRepoService(
        IGitService gitService,
        IAugmentedRepoService augmentedRepoService,
        IConverter converter,
        IViewRepoCreater viewRepoCreater)
    {
        this.gitService = gitService;
        this.augmentedRepoService = augmentedRepoService;
        this.converter = converter;
        this.viewRepoCreater = viewRepoCreater;

        augmentedRepoService.RepoChange += (s, e) => OnRepoChange(e);
        augmentedRepoService.StatusChange += (s, e) => OnStatusChange(e);
    }

    public event EventHandler<ChangeEventArgs>? RepoChange;
    public event EventHandler<ChangeEventArgs>? StatusChange;


    public Task<R<Repo>> GetRepoAsync(string path) =>
        GetRepoAsync(path, new string[0]);


    public async Task<R<Repo>> GetFreshRepoAsync(Repo repo)
    {
        var branches = repo.Branches.Select(b => b.Name).ToArray();
        return await GetRepoAsync(repo.Path, branches);
    }


    public async Task<R<Repo>> GetUpdateStatusRepoAsync(Repo repo)
    {
        var branches = repo.Branches.Select(b => b.Name).ToArray();

        var augmentedRepo = await augmentedRepoService.UpdateStatusRepoAsync(repo.AugmentedRepo);
        if (augmentedRepo.IsError)
        {
            return augmentedRepo.Error;
        }

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo.Value, branches);
    }

    public async Task<R<Repo>> GetRepoAsync(string path, string[] showBranches)
    {
        var augmentedRepo = await augmentedRepoService.GetRepoAsync(path);
        if (augmentedRepo.IsError)
        {
            return augmentedRepo.Error;
        }

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo.Value, showBranches);
    }

    public IReadOnlyList<Branch> GetAllBranches(Repo repo)
    {
        return converter.ToBranches(repo.AugmentedRepo.Branches);
    }

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


    public async Task<R> CommitAllChangesAsync(Repo repo, string message)
    {
        return await gitService.Git(repo.Path).CommitAllChangesAsync(message);
    }

    public async Task<R<CommitDiff>> GetCommitDiffAsync(Repo repo, string commitId)
    {
        var gitCommitDiff = await gitService.Git(repo.Path).GetCommitDiffAsync(commitId);
        if (gitCommitDiff.IsError)
        {
            return gitCommitDiff.Error;
        }

        return converter.ToCommitDiff(gitCommitDiff.Value);
    }

    public async Task<R<CommitDiff>> GetUncommittedDiff(Repo repo)
    {
        var gitCommitDiff = await gitService.Git(repo.Path).GetUncommittedDiff();
        if (gitCommitDiff.IsError)
        {
            return gitCommitDiff.Error;
        }

        return converter.ToCommitDiff(gitCommitDiff.Value);
    }


    protected virtual void OnRepoChange(ChangeEventArgs e)
    {
        var handler = RepoChange;
        handler?.Invoke(this, e);
    }

    protected virtual void OnStatusChange(ChangeEventArgs e)
    {
        var handler = StatusChange;
        handler?.Invoke(this, e);
    }
}

