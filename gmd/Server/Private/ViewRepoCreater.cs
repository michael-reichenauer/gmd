using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using gmd.Common;

namespace gmd.Server.Private;


interface IViewRepoCreater
{
    Repo GetViewRepoAsync(Repo augRepo, IReadOnlyList<string> showBranches, ShowBranches show = ShowBranches.Specified, int count = 1);

    Repo GetFilteredViewRepoAsync(Repo augRepo, string filter, int maxCount);
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


    public Repo GetViewRepoAsync(Repo augRepo, IReadOnlyList<string> showBranches, ShowBranches show = ShowBranches.Specified, int count = 1)
    {
        var t = Timing.Start();
        var filteredBranches = FilterOutViewBranches(augRepo, showBranches, show, count);
        var filteredCommits = FilterOutViewCommits(augRepo, filteredBranches);

        if (TryGetUncommittedCommit(augRepo, filteredBranches, out var uncommitted))
        {
            AdjustCurrentBranch(filteredBranches, filteredCommits, uncommitted);
        }

        SetAheadBehind(filteredBranches, filteredCommits);

        var repo = new Repo(
            augRepo.Path,
            DateTime.UtcNow,
            converter.ToCommits(filteredCommits),
            converter.ToBranches(filteredBranches),
            augRepo.Stashes,
            augRepo.Status,
            "",
            augRepo);

        Log.Info($"ViewRepo {t} {repo}");
        return repo;
    }

    public Repo GetFilteredViewRepoAsync(Repo augRepo, string filter, int maxCount)
    {
        using (Timing.Start($"Filtered repo on '{filter}'"))
        {
            IReadOnlyDictionary<string, Commit> filteredCommits = null!;

            if (filter == "$")
            {   // Get all commits, where branch was set manually by user
                filteredCommits = augRepo.Commits.Where(c => c.IsBranchSetByUser).Take(maxCount).ToDictionary(c => c.Id, c => c);
            }
            else if (filter == "*")
            {   // Get all commits, with ambiguous tip
                filteredCommits = augRepo.BranchByName.Values.Where(b => b.AmbiguousTipId != "")
                    .Select(b => augRepo.CommitById[b.AmbiguousTipId])
                    .Where(c => c.IsAmbiguousTip)
                    .Take(maxCount)
                    .ToDictionary(c => c.Id, c => c);
            }
            else
            {   // Get all commits matching filter
                filteredCommits = GetFilteredCommits(augRepo, filter, maxCount);
            }

            // Get all branch names for the filtered commits
            var branchNames = filteredCommits.Values.Select(c => c.BranchName).Distinct().ToList();
            if (!branchNames.Any())
            {   // No commits matching filter, return empty repo
                Log.Info($"No commits matching filter'");
                return EmptyFilteredRepo(augRepo, filter);
            }

            // First create view repo with all branches and their commits
            var r = GetViewRepoAsync(augRepo, branchNames);

            // Return repo with filtered commits and their branches
            var adjustedCommits = r.Commits
                .Where(c => filteredCommits.ContainsKey(c.Id))
                .Select((c, i) => c with { ViewIndex = i })
                .ToList();
            return new Repo(r.AugmentedRepo.Path, r.TimeStamp, adjustedCommits, r.Branches, r.Stashes, r.Status, filter, r.AugmentedRepo);
        }
    }

    static IReadOnlyDictionary<string, Commit> GetFilteredCommits(
        Repo augRepo, string filter, int maxCount)
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
        return augRepo.Commits
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
            .ToDictionary(c => c.Id, c => c);
    }


    static Repo EmptyFilteredRepo(Repo augRepo, string filter)
    {
        var id = Repo.TruncatedLogCommitID;
        var msg = $"<... No commits matching filter ...>";
        var branchName = "<none>";
        var commits = new List<Commit>(){ new Commit( id, id.Sid(),
            msg, msg, "", DateTime.UtcNow, true, 0, 0, branchName, branchName, branchName,
            new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<Tag>(),
            new List<string>(), false,false,false,false,false,false,false,false,false,false,false, More.None)};
        var branches = new List<Branch>() { new Branch(branchName, branchName, id, branchName, branchName,
            id, id, false, false, false, "", "", true, false, true, true, "", "", false, false, "",
            new List<string>(), new List<string>(), new List<string>(),new List<string>(),false, 0, false, false) };

        return new Repo(augRepo.Path, DateTime.UtcNow, commits, branches, new List<Stash>(), augRepo.Status, filter, augRepo);
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
        return repo.Commits
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
                    .Select(name => repo.BranchByName.Values
                        .FirstOrDefault(b => b.PrimaryBaseName == name || b.Name == name || b.PrimaryName == name))
                    .Where(b => b != null)
                    .ForEach(b => AddBranchAndAncestorsAndRelatives(repo, b!, branches));
                break;
            case ShowBranches.AllRecent:
                repo.BranchByName.Values.Where(b => !b.IsCircularAncestors)
                    .OrderBy(b => repo.CommitById[b.TipId].GitIndex)
                    .Where(b => b.IsPrimary && !showBranches.Contains(b.Name))
                    .Take(count)
                    .Concat(showBranches.Select(n => repo.BranchByName.TryGetValue(n, out var bbb) ? bbb : null).Where(b => b != null))
                    .ForEach(b => AddBranchAndAncestorsAndRelatives(repo, b, branches));
                break;
            case ShowBranches.AllActive:
                repo.BranchByName.Values
                    .Where(b => !b.IsCircularAncestors && b.IsGitBranch && b.IsPrimary)
                    .ForEach(b => AddBranchAndAncestorsAndRelatives(repo, b, branches));

                break;
            case ShowBranches.AllActiveAndDeleted:
                repo.BranchByName.Values
                     .Where(b => !b.IsCircularAncestors && b.IsPrimary)
                     .ForEach(b => AddBranchAndAncestorsAndRelatives(repo, b, branches));
                break;
        }


        if (showBranches.Count == 0)
        {   // No branches where specified, assume current branch
            var current = repo.BranchByName.Values.FirstOrDefault(b => b.IsCurrent);
            AddBranchAndAncestorsAndRelatives(repo, current, branches);
        }

        // Ensure that main branch is always included 
        var main = repo.BranchByName.Values.First(b => b.IsMainBranch);
        AddBranchAndAncestorsAndRelatives(repo, main, branches);

        // If current branch is detached, include it as well (commit is checked out directly)
        var detached = repo.BranchByName.Values.FirstOrDefault(b => b.IsDetached);
        if (detached != null) AddBranchAndAncestorsAndRelatives(repo, detached, branches);

        // Is this still needed?????
        // Ensure all branch tip branches are included (in case of tip on parent with no own commits)
        // foreach (var b in branches.Values.ToList())
        // {
        //     var tipBranch = repo.Branches[repo.CommitById[b.TipId].BranchName];
        //     AddBranchAndAncestorsAndRelatives(repo, tipBranch, branches);
        // }

        var sorted = SortBranches(repo, branches.Values);

        // Log.Info($"Filtered {sorted.Count} branches:\n  {sorted.Select(b => b.Name).Join("\n  ")}");
        // var mainBranches = sorted.Where(b => b.IsPrimary || b.RemoteName != "").ToList();
        // Log.Debug($"Filtered {mainBranches.Count} main branches:\n  {mainBranches.Select(b => b.Name).Join("\n  ")}");
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
                    if (repo.CommitById.TryGetValue(repo.Status.MergeHeadId, out var mergeHead))
                    {
                        parentIds.Add(repo.Status.MergeHeadId);
                    }
                }

                // Create a new virtual uncommitted commit
                uncommitted = new Commit(
                    Id: Repo.UncommittedId, Sid: Repo.UncommittedId.Sid(),
                    Subject: subject, Message: subject, Author: "", AuthorTime: DateTime.Now,
                    IsView: true, ViewIndex: 0, GitIndex: 0, currentBranch.Name, currentBranch.PrimaryName, currentBranch.NiceNameUnique,
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
        filteredCommits.Insert(0, uncommitted);

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
