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

    public IReadOnlyList<Branch> GetAllBranches(Repo repo)
    {
        return converter.ToBranches(repo.AugmentedRepo.Branches);
    }

    public async Task<Repo> ShowBranch(Repo repo, string branchName)
    {
        var names = repo.Branches.Select(b => b.Name).Append(branchName).ToArray();

        return await GetViewRepoAsync(repo.AugmentedRepo, names);
    }

    protected virtual void OnRepoChange(EventArgs e)
    {
        EventHandler? handler = RepoChange;
        handler?.Invoke(this, e);
    }

    async Task<Repo> GetViewRepoAsync(Augmented.Repo augRepo, string[] showBranches)
    {
        var branches = FilterOutViewBranches(augRepo, showBranches);
        var commits = FilterOutViewCommits(augRepo, branches);

        Log.Info($"Branches: {augRepo.Branches.Count} => {branches.Count}");
        Log.Info($"Commits: {augRepo.Commits.Count} => {commits.Count}");

        return new Repo(
            augRepo,
            converter.ToCommits(commits),
            converter.ToBranches(branches));
    }

    private IReadOnlyList<Augmented.Commit> FilterOutViewCommits(
        Augmented.Repo repo, IReadOnlyList<Augmented.Branch> viewBranches)
    {
        // Return commits, which branch does exist in branches to be viewed.
        return repo.Commits
            .Where(c => viewBranches.FirstOrDefault(b => b.Name == c.BranchName) != null)
            .ToList();
    }

    IReadOnlyList<Augmented.Branch> FilterOutViewBranches(Augmented.Repo repo, string[] showBranches)
    {
        var branches = showBranches
            .Select(name => repo.Branches.FirstOrDefault(b => b.Name == name))
            .Where(b => b != null)
            .Select(b => b!) // Workaround since compiler does not recognize the previous Where().
            .ToList();       // To be able to add more

        if (showBranches.Length == 0)
        {   // No branches where specified, assume current branch
            var current = repo.Branches.FirstOrDefault(b => b.IsCurrent);
            if (current != null)
            {
                branches.Add(current);
            }
        }

        if (branches.Count == 0)
        {   // Ensure that at least main branch is included 
            var main = repo.Branches.First(b => b.IsMainBranch);
            branches.Add(main);
        }

        // Ensure all ancestors are included
        foreach (var b in branches.ToList())
        {
            Ancestors(repo, b).ForEach(bb => branches.Add(bb));
        }

        // Ensure all local branches of remote branches are included 
        // (remote branches of local branches are ancestors and already included)
        foreach (var b in branches.ToList())
        {
            if (b.IsRemote && b.LocalName != "")
            {
                branches.Add(repo.BranchByName[b.LocalName]);
            }
        }

        // Remove duplicates (ToList(), since Sort works inline)
        branches = branches.DistinctBy(b => b.Name).ToList();

        // Sort on branch hierarchy
        branches.Sort((b1, b2) => CompareBranches(repo, b1, b2));
        return branches;
    }

    int CompareBranches(Augmented.Repo repo, Augmented.Branch b1, Augmented.Branch b2)
    {
        return IsFirstAncestorOfSecond(repo, b1, b2) ? -1 :
            IsFirstAncestorOfSecond(repo, b2, b1) ? 1 : 0;
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
        if (branch == ancestor)
        {
            // Same initial branches (not ancestor)
            return false;
        }
        if (branch.LocalName == ancestor.Name)
        {
            // Branch is remote branch of the ancestor
            return false;
        }
        if (branch.RemoteName == ancestor.Name)
        {
            // branch is the local name of the is the remote branch of the branch
            return true;
        }

        var current = branch;
        while (true)
        {
            if (current == ancestor)
            {
                // Current must now be one of its parents and thus is an ancestor
                return true;
            }
            if (current.Name == ancestor.LocalName)
            {
                // Current is the local branch of the ancestor, which is an ancestor as well
                return true;
            }
            if (current.Name == ancestor.RemoteName)
            {
                // Current is the no the remote branch of the ancestor, which is an ancestor of the
                //  original branch
                return true;
            }

            if (current.ParentBranchName == "")
            {
                // Reached root (current usually is origin/main or origin/master)
                return false;
            }

            // Try with parent of current
            current = repo.BranchByName[current.ParentBranchName];
        }
    }
}

