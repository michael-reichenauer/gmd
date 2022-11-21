using gmd.Git;
using gmd.Server.Private.Augmented;

namespace gmd.Server.Private;

[SingleInstance]
class Server : IServer
{
    private readonly IGit git;
    private readonly IAugmentedService augmentedRepoService;
    private readonly IConverter converter;
    private readonly IViewRepoCreater viewRepoCreater;

    public Server(
        IGit git,
        IAugmentedService augmentedRepoService,
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


    public IReadOnlyList<Commit> GetFilterCommits(Repo repo, string filter)
    {
        var sc = StringComparison.OrdinalIgnoreCase;
        var commits = repo.AugmentedRepo.Commits
         .Where(c =>
             c.Id.Contains(filter, sc) ||
             c.Subject.Contains(filter, sc) ||
             c.BranchName.Contains(filter, sc) ||
             c.Author.Contains(filter, sc));
        return converter.ToCommits(commits.ToList());

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

    public async Task<R> FetchAsync(string wd) =>
        await git.FetchAsync(wd);

    public async Task<R> CommitAllChangesAsync(string message, string wd) =>
         await git.CommitAllChangesAsync(message, wd);


    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd)
    {
        var diffTask = commitId == Repo.UncommittedId
            ? git.GetUncommittedDiff(wd)
            : git.GetCommitDiffAsync(commitId, wd);

        if (!Try(out var gitCommitDiff, out var e, await diffTask)) return e;

        var diff = converter.ToCommitDiff(gitCommitDiff);
        return diff;
    }


    public Task<R> PushBranchAsync(string name, string wd) =>
        git.PushBranchAsync(name, wd);

    public Task<R> PullCurrentBranchAsync(string wd) =>
        git.PullCurrentBranchAsync(wd);

    public Task<R> PullBranchAsync(string name, string wd) =>
        git.PullBranchAsync(name, wd);

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

