namespace gmd.Server.Private;

// Converts into the view repo the UI renders, i.e. the augmented repo narrowed to the commits
// and branches the user chose to show. See WorkRepoConverter for the step before this one, and
// GitConverter for the records read from git.
interface IViewRepoConverter
{
    IReadOnlyList<Commit> ToViewCommits(IEnumerable<Commit> commits);

    Repo ToViewRepo(
        DateTime timeStamp,
        IReadOnlyList<Commit> viewCommits,
        IReadOnlyList<Branch> viewBranches,
        string filter,
        Repo repo
    );
}

class ViewRepoConverter : IViewRepoConverter
{
    public IReadOnlyList<Commit> ToViewCommits(IEnumerable<Commit> commits) =>
        commits.Select((c, i) => c with { IsInView = true, ViewIndex = i }).ToList();

    public Repo ToViewRepo(
        DateTime timeStamp,
        IReadOnlyList<Commit> viewCommits,
        IReadOnlyList<Branch> viewBranches,
        string filter,
        Repo repo
    )
    {
        // Copy and ensure commits and repo are by default not in view
        var allCommits = repo.AllCommits.Select(c => c with { IsInView = false, ViewIndex = -1 }).ToList();
        var allBranches = repo.AllBranches.Select(b => b with { IsInView = false }).ToList();

        // Crate index lookup for commits and branches
        var commitIndexById = new Dictionary<string, int>();
        var branchIndexByName = new Dictionary<string, int>();
        allCommits.ForEach((c, i) => commitIndexById[c.Id] = i);
        allBranches.ForEach((b, i) => branchIndexByName[b.Name] = i);

        // Set IsInView and ViewIndex for commits and branches in view and update commitsById and branchByName
        viewCommits = viewCommits.Select((c, i) => c with { IsInView = true, ViewIndex = i }).ToList();
        viewCommits.ForEach(c => allCommits[commitIndexById[c.Id]] = c);

        viewBranches = viewBranches.Select(b => b with { IsInView = true }).ToList();
        viewBranches.ForEach(b => allBranches[branchIndexByName[b.Name]] = b);

        // The repo time stamp is when the read the repo came from started, which says what changes it
        // has seen (ChangeEvent.IsSeenBy). Showing other branches of it reads nothing, so it keeps it:
        // the time the view repo was made would take a change made during the read as seen.
        return new Repo(
            repo.Path,
            timeStamp,
            repo.RepoTimeStamp,
            viewCommits,
            viewBranches,
            allCommits,
            allBranches,
            repo.Stashes,
            repo.Status,
            filter
        )
        {
            Worktrees = repo.Worktrees,
            UndoSteps = repo.UndoSteps,
            RemoteRewrites = repo.RemoteRewrites,
            CurrentAuthor = repo.CurrentAuthor,
        };
    }
}
