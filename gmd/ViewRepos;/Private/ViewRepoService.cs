using gmd.ViewRepos.Private.Augmented;

namespace gmd.ViewRepos.Private;


class ViewRepoService : IViewRepoService
{
    private readonly IAugmentedRepoService augmentedRepoService;
    private readonly IConverter converter;

    public ViewRepoService(
        IAugmentedRepoService augmentedRepoService,
        IConverter converter)
    {
        this.augmentedRepoService = augmentedRepoService;
        this.converter = converter;
    }

    public event EventHandler? RepoChange;


    public Task<R<Repo>> GetRepoAsync(string path) =>
        GetRepoAsync(path, new string[0]);


    public async Task<R<Repo>> GetRepoAsync(string path, string[] showBranches)
    {
        var augmentedRepo = await augmentedRepoService.GetRepoAsync(path);
        if (augmentedRepo.IsError)
        {
            return augmentedRepo.Error;
        }

        return await GetViewRepoAsync(augmentedRepo.Value, showBranches);
    }


    protected virtual void OnRepoChange(EventArgs e)
    {
        EventHandler? handler = RepoChange;
        handler?.Invoke(this, e);
    }

    async Task<R<Repo>> GetViewRepoAsync(Augmented.Repo augRepo, string[] showBranches)
    {
        Log.Info($"Show branches {GetBranchNamesToShow(augRepo, showBranches).AsString()}");
        return converter.ToRepo(augRepo);
    }


    IReadOnlyList<string> GetBranchNamesToShow(Augmented.Repo repo, string[] showBranches)
    {
        List<Augmented.Branch> branches = new List<Augmented.Branch>();
        if (showBranches.Length == 0)
        {   // No branches where specified, assume current branch
            var current = repo.Branches.FirstOrDefault(b => b.IsCurrent);
            if (current != null)
            {
                branches.Add(current);
                Ancestors(repo, current).ForEach(b => branches.Add(b));
            }
        }

        foreach (var name in showBranches)
        {
            var ab = repo.Branches.FirstOrDefault(b => b.Name == name);
            if (ab != null && !branches.Contains(ab))
            {
                branches.Add(ab);
                Ancestors(repo, ab).ForEach(b => branches.Add(b));
            }
        }

        if (branches.Count == 0)
        {
            var main = repo.Branches.First(b => b.IsMainBranch);
            branches.Add(main);
        }

        var b = branches.DistinctBy(b => b.Name).ToList();  // Remove duplicates
        b.Sort((b1, b2) =>                      // Sort on branch hierarchy
            IsFirstAncestorOfSecond(repo, b1, b2) ? -1 :
            IsFirstAncestorOfSecond(repo, b2, b1) ? 1 : 0);

        return b.Select(b => b.Name).ToList();
    }

    IReadOnlyList<Augmented.Branch> Ancestors(Augmented.Repo repo, Augmented.Branch branch)
    {
        List<Augmented.Branch> ancestors = new List<Augmented.Branch>();

        while (branch.ParentBranchName != "")
        {
            var parent = repo.BranchByName[branch.ParentBranchName];
            ancestors.Add(parent);
            branch = parent;
        }

        return ancestors;
    }

    bool IsFirstAncestorOfSecond(Augmented.Repo repo, Augmented.Branch ancestor, Augmented.Branch branch)
    {
        var current = branch;

        while (current.ParentBranchName != "")
        {
            var parent = repo.BranchByName[current.ParentBranchName];
            if (parent == ancestor)
            {
                return true;
            }

            current = parent;
        }

        return false;
    }
}

