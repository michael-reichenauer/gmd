using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using gmd.Common;

namespace gmd.Server.Private;


interface IViewRepoCreater
{
    Repo GetViewRepoAsync(Repo augRepo, IReadOnlyList<string> showBranches, ShowBranches show = ShowBranches.Specified, int count = 1);

    Repo GetFilteredViewRepoAsync(Repo repo, string filter, int maxCount);
}

class ViewRepoCreater : IViewRepoCreater
{
    readonly IConverter converter;
    readonly IRepoConfig repoConfig;

    internal ViewRepoCreater(IConverter converter, IRepoConfig repoConfig)
    {
        this.converter = converter;
        this.repoConfig = repoConfig;
    }


    public Repo GetViewRepoAsync(Repo repo, IReadOnlyList<string> showBranches, ShowBranches show = ShowBranches.Specified, int count = 1)
    {
        var t = Timing.Start();
        var viewBranches = FilterOutViewBranches(repo, showBranches, show, count);
        var viewCommits = FilterOutViewCommits(repo, viewBranches);

        if (TryGetUncommittedCommit(repo, viewBranches, out var uncommitted))
        {
            AdjustCurrentBranch(viewBranches, viewCommits, uncommitted);
        }

        SetAheadBehind(viewBranches, viewCommits);

        var viewRepo = converter.ToViewRepo(DateTime.UtcNow, viewCommits, viewBranches, "", repo);

        Log.Info($"ViewRepo {t} {viewRepo}");
        return viewRepo;
    }

    public Repo GetFilteredViewRepoAsync(Repo repo, string filter, int maxCount)
    {
        using (Timing.Start($"Filtered repo on '{filter}'"))
        {
            IReadOnlyList<Commit> filteredCommits;

            if (filter == "$")
            {   // Get all commits, where branch was set manually by user
                filteredCommits = repo.CommitById.Values
                    .Where(c => c.IsBranchSetByUser).Take(maxCount).ToList();
            }
            else if (filter == "*")
            {   // Get all commits, with ambiguous tip
                filteredCommits = repo.AllBranches.Where(b => b.AmbiguousTipId != "")
                    .Select(b => repo.CommitById[b.AmbiguousTipId])
                    .Where(c => c.IsAmbiguousTip)
                    .Take(maxCount)
                    .ToList();
            }
            else
            {   // Get all commits matching filter
                filteredCommits = GetFilteredCommits(repo, filter, maxCount);
            }

            if (!filteredCommits.Any()) EmptyFilteredRepo(repo, filter);


            // Get all branch names for the filtered commits
            var filteredBranchNames = filteredCommits.Select(c => c.BranchName).Distinct().ToList();

            // First create view repo with all filtered branches and all those branches commits
            // i.e. more commits than the filtered commits, since a branch can contains more commits than the filtered commits
            var filteredRepo = GetViewRepoAsync(repo, filteredBranchNames);

            // From the repo with all filtered branches and to many commits, extract only those
            // commits that match the filtered commits
            var filteredCommitsById = filteredCommits.ToDictionary(c => c.Id, c => c);
            var viewCommits = filteredRepo.ViewCommits
                .Where(c => filteredCommitsById.ContainsKey(c.Id))
                .ToList();
            var viewBranches = filteredRepo.ViewBranches;

            return converter.ToViewRepo(filteredRepo.TimeStamp, viewCommits, viewBranches, filter, repo);
        }
    }

    static IReadOnlyList<Commit> GetFilteredCommits(
        Repo repo, string filter, int maxCount)
    {
        var sc = StringComparison.OrdinalIgnoreCase;

        // Need extract all text enclosed by double quotes in filter (for exact matches of them)
        var matches = Regex.Matches(filter, "\"([^\"]*)\"");
        var quoted = matches.Select(m => m.Groups[1].Value).ToList();

        // Replace all quoted text, where space is replaced by newlines to make it easier to split on space below. 
        var modifiedFilter = filter;
        quoted.ForEach(q => modifiedFilter = modifiedFilter.Replace($"\"{q}\"", q.Replace(" ", "\n")));

        // Split on space to get all AND parts of the text (and fix newlines to spaces again)
        var andParts = modifiedFilter.Split(' ').Where(p => p != "")
            .Select(p => p.Replace("\n", " "))      // Replace newlines back to spaces again 
            .ToList();

        // Find all branches matching all AND parts.
        return repo.CommitById.Values
            .Where(c => andParts.All(p =>
                c.Id.Contains(p, sc) ||
                c.Subject.Contains(p, sc) ||
                c.BranchName.Contains(p, sc) ||
                c.Author.Contains(p, sc) ||
                c.AuthorTime.IsoDate().Contains(p, sc) ||
                c.BranchNiceUniqueName.Contains(p, sc) ||
                c.BranchName.Contains(p, sc) ||
                c.Tags.Any(t => t.Name.Contains(p, sc))))
            .Take(maxCount)
            .ToList();
    }


    Repo EmptyFilteredRepo(Repo repo, string filter)
    {
        // A repo with just 1 virtual commit and 1 branch
        var id = Repo.TruncatedLogCommitID;
        var msg = $"<... No commits matching filter ...>";
        var branchName = "<none>";

        var commit = new Commit(id, id.Sid(),
            msg, msg, "", DateTime.UtcNow, true, 0, 0, branchName, branchName, branchName,
            new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<Tag>(),
            new List<string>(), false, false, false, false, false, false, false, false, false, false, false, More.None);
        var branch = new Branch(branchName, branchName, id, branchName, branchName,
            id, id, false, false, false, "", "", true, false, false, true, true, "", "", false, false, "",
            new List<string>(), new List<string>(), new List<string>(), new List<string>(), false, 0, false, false);

        // Create a virtual augmented repo with just the 1 commit and 1 branch
        var allCommits = new List<Commit>() { commit };
        var allBranches = new List<Branch>() { branch };
        var viewCommits = new List<Commit>();
        var viewBranches = new List<Branch>();

        var augRepo = new Repo(repo.Path, DateTime.UtcNow, repo.TimeStamp,
             viewCommits, viewBranches, allCommits, allBranches, new List<Stash>(), Status.Empty, filter);

        // Convert to a view repo
        viewCommits = new List<Commit>() { commit };
        viewBranches = new List<Branch>() { branch };
        return converter.ToViewRepo(DateTime.UtcNow, viewCommits, viewBranches, filter, augRepo);
    }

    static void SetAheadBehind(
        List<Branch> filteredBranches,
        List<Commit> filteredCommits)
    {
        foreach (var b in filteredBranches.ToList())  // ToList() since SetBehind/SetAhead modifies branches
        {
            if (b.IsRemote && b.LocalName != "")
            {
                // Remote branch with ahead commits (remote only commits)
                SetBehindCommits(b, filteredBranches, filteredCommits);
            }
            else if (!b.IsRemote && b.RemoteName != "")
            {
                // Local branch with behind commits (local only commits)
                SetAheadCommits(b, filteredBranches, filteredCommits);
            }
        }
    }

    static void SetBehindCommits(
        Branch remoteBranch,
        List<Branch> filteredBranches,
        List<Commit> filteredCommits)
    {
        int remoteBranchIndex = filteredBranches.FindIndex(b => b.Name == remoteBranch.Name);
        int localBranchIndex = filteredBranches.FindIndex(b => b.Name == remoteBranch.LocalName);

        var localBranch = filteredBranches[localBranchIndex];
        if (localBranch.TipId == remoteBranch.TipId)
        {
            // Local branch tip on same commit as remote branch tip (i.e. synced)
            return;
        }

        var localTip = filteredCommits.First(c => c.Id == localBranch.TipId);
        var localBottom = filteredCommits.First(c => c.Id == localBranch.BottomId);
        var localBase = filteredCommits.First(c => c.Id == localBottom.ParentIds[0]);

        bool hasBehindCommits = false;
        int count = 0;
        int commitIndex = filteredCommits.FindIndex(c => c.Id == remoteBranch.TipId);
        while (commitIndex != -1)
        {
            var commit = filteredCommits[commitIndex];
            count++;
            if (commit.BranchName != remoteBranch.Name || count > 50)
            {   // Other branch or to many ahead commits
                break;
            }
            if (commit.Id == localTip.Id)
            {
                // Local branch tip on same commit as remote branch tip (i.e. synced)
                break;
            }
            if (commit.Id == localBase.Id)
            {
                // Reached same commit as local branch branched from (i.e. synced from this point)
                break;
            }

            if (commit.ParentIds.Count > 1)
            {
                var mergeParent = filteredCommits.FirstOrDefault(c => c.Id == commit.ParentIds[1]);
                if (mergeParent != null && mergeParent.BranchName == localBranch.Name)
                {   // Merge from local branch (into this remote branch)
                    break;
                }
            }

            // Commit is(behind)
            filteredCommits[commitIndex] = commit with { IsBehind = true };
            hasBehindCommits = true;

            if (commit.ParentIds.Count == 0)
            {   // Reach last commit
                break;
            }
            commitIndex = filteredCommits.FindIndex(c => c.Id == commit.ParentIds[0]);
        }

        if (hasBehindCommits)
        {
            filteredBranches[remoteBranchIndex] = remoteBranch with { HasRemoteOnly = true };
            filteredBranches[localBranchIndex] = localBranch with { HasRemoteOnly = true };
        }
    }

    static void SetAheadCommits(
        Branch localBranch,
        List<Branch> filteredBranches,
        List<Commit> filteredCommits)
    {
        int localBranchIndex = filteredBranches.FindIndex(b => b.Name == localBranch.Name);
        int remoteBranchIndex = filteredBranches.FindIndex(b => b.Name == localBranch.RemoteName);
        var remoteBranch = filteredBranches[remoteBranchIndex];
        bool hasAheadCommits = false;
        int count = 0;
        int commitIndex = filteredCommits.FindIndex(c => c.Id == localBranch.TipId);
        while (commitIndex != -1)
        {
            count++;
            var commit = filteredCommits[commitIndex];

            if (commit.BranchName != localBranch.Name || count > 50)
            {
                break;
            }

            if (commit.Id != Repo.UncommittedId)
            {
                // Commit is ahead
                filteredCommits[commitIndex] = commit with { IsAhead = true };
                hasAheadCommits = true;
            }

            if (commit.ParentIds.Count == 0)
            {   // Reach last commit
                break;
            }
            commitIndex = filteredCommits.FindIndex(c => c.Id == commit.ParentIds[0]);
        }

        if (hasAheadCommits)
        {
            filteredBranches[localBranchIndex] = localBranch with { HasLocalOnly = true };
            filteredBranches[remoteBranchIndex] = remoteBranch with { HasLocalOnly = true };
        }
    }

    static List<Commit> FilterOutViewCommits(Repo repo, IReadOnlyList<Branch> filteredBranches)
    {
        // Return filtered commits, where commit branch does is in filtered branches to be viewed.
        return repo.AllCommits
            .Where(c => filteredBranches.FirstOrDefault(b => b.Name == c.BranchName) != null)
            .ToList();
    }

    List<Branch> FilterOutViewBranches(Repo repo,
    IReadOnlyList<string> showBranches, ShowBranches show = ShowBranches.Specified, int count = 1)
    {
        var branches = new Dictionary<string, Branch>();

        switch (show)
        {
            case ShowBranches.Specified:
                showBranches
                    .Select(name => repo.AllBranches
                        .FirstOrDefault(b => b.PrimaryBaseName == name || b.Name == name || b.PrimaryName == name))
                    .Where(b => b != null)
                    .ForEach(b => AddBranchAndAncestorsAndRelatives(repo, b!, branches));
                break;
            case ShowBranches.AllRecent:
                repo.AllBranches.Where(b => !b.IsCircularAncestors)
                    .OrderBy(b => repo.CommitById[b.TipId].GitIndex)
                    .Where(b => b.IsPrimary && !showBranches.Contains(b.Name))
                    .Take(count)
                    .Concat(showBranches.Select(n => repo.BranchByName.TryGetValue(n, out var bbb) && bbb.IsInView ? bbb : null).Where(b => b != null))
                    .ForEach(b => AddBranchAndAncestorsAndRelatives(repo, b, branches));
                break;
            case ShowBranches.AllActive:
                repo.AllBranches
                    .Where(b => !b.IsCircularAncestors && b.IsGitBranch && b.IsPrimary)
                    .ForEach(b => AddBranchAndAncestorsAndRelatives(repo, b, branches));

                break;
            case ShowBranches.AllActiveAndDeleted:
                repo.AllBranches
                     .Where(b => !b.IsCircularAncestors && b.IsPrimary)
                     .ForEach(b => AddBranchAndAncestorsAndRelatives(repo, b, branches));
                break;
        }


        if (showBranches.Count == 0)
        {   // No branches where specified, assume current branch
            var current = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
            AddBranchAndAncestorsAndRelatives(repo, current, branches);
        }

        // Ensure that main branch is always included 
        var main = repo.AllBranches.First(b => b.IsMainBranch);
        AddBranchAndAncestorsAndRelatives(repo, main, branches);

        // If current branch is detached, include it as well (commit is checked out directly)
        var detached = repo.AllBranches.FirstOrDefault(b => b.IsDetached);
        if (detached != null) AddBranchAndAncestorsAndRelatives(repo, detached, branches);

        var sorted = SortBranches(repo, branches.Values);
        return sorted;
    }

    void AddBranchAndAncestorsAndRelatives(Repo repo, Branch? branch, IDictionary<string, Branch> branches)
    {
        if (branch == null || branches.ContainsKey(branch.Name)) return;
        if (branch.IsCircularAncestors) return;

        branches[branch.Name] = branch;
        branch.AncestorNames.ForEach(n => AddBranchAndAncestorsAndRelatives(repo, repo.BranchByName[n], branches));

        var primary = repo.BranchByName[branch.PrimaryName];
        primary.RelatedBranchNames.ForEach(n => AddBranchAndAncestorsAndRelatives(repo, repo.BranchByName[n], branches));
    }

    List<Branch> SortBranches(Repo repo, IEnumerable<Branch> branches)
    {
        var sorted = branches.Where(b => b.IsPrimary).ToList();

        var branchOrders = repoConfig.Get(repo.Path).BranchOrders;
        // Sort on branch hierarchy, For some strange reason, List.Sort does not work, why ????
        Sorter.Sort(sorted, (b1, b2) => CompareBranches(b1, b2, branchOrders));

        // Reinsert the pullMerge branches just after its parent branch
        var toInsert = new Queue<Branch>(branches.Where(b => b.PullMergeParentBranchName != ""));
        while (toInsert.Any())
        {
            var b = toInsert.Dequeue();
            var index = sorted.FindIndex(bb => bb.Name == b.PullMergeParentBranchName);
            if (index == -1)
            {   // Parent branch not yet inserted, skip now and try again later
                toInsert.Enqueue(b);
                continue;
            }
            sorted.Insert(index + 1, b);
        }

        // Reinsert the local branches just after its remote branch
        branches.Where(b => b.RemoteName != "").ForEach(b =>
        {
            var index = sorted.FindIndex(bb => bb.Name == b.RemoteName);
            sorted.Insert(index + 1, b);
        });

        return sorted;
    }

    static bool TryGetUncommittedCommit(Repo repo,
      IReadOnlyList<Branch> filteredBranches,
      [MaybeNullWhen(false)] out Commit uncommitted)
    {
        if (!repo.Status.IsOk)
        {
            var currentBranch = filteredBranches.FirstOrDefault(b => b.IsCurrent);
            if (currentBranch != null)
            {
                var current = repo.CommitById[currentBranch.TipId];

                var parentIds = new List<string>() { current.Id };

                int changesCount = repo.Status.ChangesCount;
                string subject = $"{changesCount} uncommitted changes";
                if (repo.Status.IsMerging && repo.Status.MergeMessage != "")
                {
                    subject = $"{repo.Status.MergeMessage}, {subject}";
                }
                if (repo.Status.Conflicted > 0)
                {
                    subject = $"CONFLICTS: {repo.Status.Conflicted}, {subject}";
                }
                if (repo.Status.MergeHeadId != "")
                {   // Add the source merge id as a merge parent to the uncommitted commit
                    var mergeHead = repo.CommitById[repo.Status.MergeHeadId];
                    if (mergeHead.IsInView)
                    {
                        parentIds.Add(repo.Status.MergeHeadId);
                    }
                }

                // Create a new virtual uncommitted commit
                uncommitted = new Commit(
                    Id: Repo.UncommittedId, Sid: Repo.UncommittedId.Sid(),
                    Subject: subject, Message: subject, Author: "", AuthorTime: DateTime.Now,
                    IsInView: true, ViewIndex: 0, GitIndex: 0, currentBranch.Name, currentBranch.PrimaryName, currentBranch.NiceNameUnique,
                    ParentIds: parentIds, AllChildIds: new List<string>(), FirstChildIds: new List<string>(), MergeChildIds: new List<string>(),
                    Tags: new List<Tag>(), BranchTips: new List<string>(),
                    IsCurrent: false, IsDetached: false, IsUncommitted: true, IsConflicted: repo.Status.Conflicted > 0,
                    IsAhead: false, IsBehind: false,
                    IsTruncatedLogCommit: false, IsAmbiguous: false, IsAmbiguousTip: false,
                    IsBranchSetByUser: false, HasStash: false, More.None);

                return true;
            }
        }

        uncommitted = null;
        return false;
    }

    static void AdjustCurrentBranch(
        List<Branch> filteredBranches,
        List<Commit> filteredCommits,
        Commit uncommitted)
    {
        // Prepend the commits with the uncommitted commit
        if (filteredCommits.Count > 0 && filteredCommits[0].Id == uncommitted.Id)
        {   // Old uncommitted commit already in list, replace
            filteredCommits[0] = uncommitted;
        }
        else
        {   // Prepend the new uncommitted commit
            filteredCommits.Insert(0, uncommitted);
        }


        // We need to adjust the current branch and the tip of that branch to include the
        // uncommitted commit
        var currentBranchIndex = filteredBranches.FindIndex(b => b.Name == uncommitted.BranchName);
        var currentBranch = filteredBranches[currentBranchIndex];
        var tipCommitIndex = filteredCommits.FindIndex(c => c.Id == currentBranch.TipId);
        var tipCommit = filteredCommits[tipCommitIndex];

        // Adjust the current branch tip id and if the branch is empty, the bottom id
        var tipId = uncommitted.Id;
        var bottomId = currentBranch.BottomId;
        if (tipCommit.BranchName != currentBranch.Name)
        {
            // Current branch does not yet have any commits, so bottom id will be the uncommitted commit
            bottomId = uncommitted.Id;
        }

        var newCurrentBranch = currentBranch with { TipId = tipId, BottomId = bottomId };
        filteredBranches[currentBranchIndex] = newCurrentBranch;

        // Adjust the current tip commit to have the uncommitted commit as child
        var childIds = tipCommit.AllChildIds.Append(uncommitted.Id).ToList();
        var newTipCommit = tipCommit with { AllChildIds = childIds };
        filteredCommits[tipCommitIndex] = newTipCommit;
    }

    static int CompareBranches(Branch b1, Branch b2,
        List<BranchOrder> branchOrders)
    {
        if (b1 == b2) return 0;
        if (b1.Name == b2.ParentBranchName) return -1;   // b1 is parent of b2
        if (b2.Name == b1.ParentBranchName) return 1;   // b2 is parent of b1

        if (b2.AncestorNames.Contains(b1.Name)) return -1; // b1 is ancestor of b2
        if (b1.AncestorNames.Contains(b2.Name)) return 1; // b2 is ancestor of b1

        // Check if unrelated branches have been ordered
        var bo = branchOrders.FirstOrDefault(b => b.Branch == b1.PrimaryName && b.Other == b2.PrimaryName);
        if (bo != null)
        {
            return bo.Order;
        }
        bo = branchOrders.FirstOrDefault(b => b.Branch == b2.PrimaryName && b.Other == b1.PrimaryName);
        if (bo != null)
        {
            return -bo.Order;
        }

        // Not related nor ordered
        return 0;
    }
}
