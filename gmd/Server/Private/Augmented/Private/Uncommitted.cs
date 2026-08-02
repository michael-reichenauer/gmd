namespace gmd.Server.Private.Augmented.Private;

// The uncommitted commit is the virtual commit gmd shows for the changes in the working folder.
// Git has no such commit, so it is added to, updated in and removed from the augmented repo here,
// which also means adjusting the parent commit and the branch it is the tip of.
// Pure repo model manipulation, no git and no dependencies, so it is testable on its own.
static class Uncommitted
{
    // Adjust adds, updates or removes the uncommitted commit to match the repo status
    public static Repo Adjust(Repo repo)
    {
        if (repo.Status.IsOk && !repo.CommitById.ContainsKey(Repo.UncommittedId))
            return repo;

        if (repo.Status.IsOk)
        { // Need to remove uncommitted commit
            var uncommitted = repo.CommitById[Repo.UncommittedId];
            Commit? parent = uncommitted.ParentIds.Any() ? repo.CommitById[uncommitted.ParentIds[0]] : null;
            if (parent != null)
            { // Uncommitted has a parent, need to adjust parents children
                parent = parent with
                {
                    AllChildIds = parent.AllChildIds.Where(c => c != uncommitted.Id).ToList(),
                    FirstChildIds = parent.FirstChildIds.Where(c => c != uncommitted.Id).ToList(),
                    MergeChildIds = parent.MergeChildIds.Where(c => c != uncommitted.Id).ToList(),
                };
            }

            var commits = repo
                .AllCommits.Where(c => c.Id != Repo.UncommittedId)
                .Select(c => c.Id == parent?.Id ? parent : c)
                .ToList();
            var commitsById = commits.ToDictionary(c => c.Id);

            var branches = repo
                .AllBranches.Select(b =>
                {
                    if (b.Name != uncommitted.BranchName)
                        return b;
                    if (b.TipId == Repo.UncommittedId && b.BottomId == Repo.UncommittedId)
                        return b with { TipId = parent?.Id ?? "", BottomId = parent?.Id ?? "" };
                    return b with { TipId = parent?.Id ?? "" };
                })
                .ToList();
            var branchByName = branches.ToDictionary(b => b.Name);

            return repo with
            {
                AllCommits = commits,
                CommitById = commitsById,
                AllBranches = branches,
                BranchByName = branchByName,
            };
        }

        var newUncommitted = NewCommit(repo);

        // Status is not ok, need to add or update commit
        if (repo.CommitById.TryGetValue(Repo.UncommittedId, out var uncommitted2))
        { // Uncommitted commit exists, need to update it
            var commits = repo.AllCommits.Select(c => c.Id == Repo.UncommittedId ? newUncommitted : c).ToList();
            var commitsById = commits.ToDictionary(c => c.Id);

            return repo with
            {
                AllCommits = commits,
                CommitById = commitsById,
            };
        }

        // Uncommitted commit does not exist, need to add it
        var commits2 = repo.AllCommits.Prepend(newUncommitted).ToList();
        var commitsById2 = commits2.ToDictionary(c => c.Id);
        var branch = repo.BranchByName[newUncommitted.BranchName];
        var parentBranch = string.IsNullOrEmpty(branch.ParentBranchName)
            ? null
            : repo.BranchByName[branch.ParentBranchName];

        var branches2 = repo
            .AllBranches.Select(b =>
            {
                if (b.Name != newUncommitted.BranchName)
                    return b;
                if (b.TipId == b.BottomId && b.TipId == parentBranch?.TipId)
                    return b with { TipId = newUncommitted.Id, BottomId = newUncommitted.Id };
                return b with { TipId = Repo.UncommittedId };
            })
            .ToList();
        var branchByName2 = branches2.ToDictionary(b => b.Name);

        return repo with
        {
            AllCommits = commits2,
            CommitById = commitsById2,
            AllBranches = branches2,
            BranchByName = branchByName2,
        };
    }

    static Commit NewCommit(Repo repo)
    {
        var currentBranch =
            repo.AllBranches.FirstOrDefault(b => b.IsCurrent)
            ?? repo.AllBranches.FirstOrDefault(b => b.IsMainBranch)
            ?? repo.AllBranches.First();

        var current = repo.CommitById[currentBranch.TipId];
        if (current.IsUncommitted)
        { // First commit is uncommitted, so use its parent as local tip
            current = repo.CommitById[current.ParentIds[0]];
        }

        var parentIds = new List<string>() { current.Id };

        if (repo.Status.MergeHeadId != "")
        { // Merge in progress, add the source merge id as a merge parent to the uncommitted commit
            var mergeHead = repo.CommitById[repo.Status.MergeHeadId];
            parentIds.Add(repo.Status.MergeHeadId);
        }

        string subject = $"{repo.Status.ChangesCount} uncommitted changes";
        if (repo.Status.IsMerging && repo.Status.MergeMessage != "")
        { // Merge in progress
            subject = $"{repo.Status.MergeMessage}, {subject}";
        }
        if (repo.Status.Conflicted > 0)
        { // Conflicts exists
            subject = $"CONFLICTS: {repo.Status.Conflicted}, {subject}";
        }

        // Create a new virtual uncommitted commit
        return new Commit(
            Id: Repo.UncommittedId,
            Sid: Repo.UncommittedId.Sid(),
            Subject: subject,
            Message: subject,
            Author: "",
            AuthorTime: DateTime.Now,
            IsInView: true,
            ViewIndex: 0,
            GitIndex: 0,
            currentBranch.Name,
            currentBranch.PrimaryName,
            currentBranch.NiceNameUnique,
            ParentIds: parentIds,
            AllChildIds: new List<string>(),
            FirstChildIds: new List<string>(),
            MergeChildIds: new List<string>(),
            Tags: new List<Tag>(),
            BranchTips: new List<string>(),
            IsCurrent: false,
            IsDetached: false,
            IsUncommitted: true,
            IsConflicted: repo.Status.Conflicted > 0,
            IsAhead: false,
            IsBehind: false,
            IsTruncatedLogCommit: false,
            IsAmbiguous: false,
            IsAmbiguousTip: false,
            IsBranchSetByUser: false,
            HasStash: false,
            More.None
        );
    }
}
