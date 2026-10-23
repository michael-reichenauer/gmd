using gmd.Common;
using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// See IMergedBranchService. A branch is a candidate when origin deleted it (Branch.GoneRemoteName),
// as GitHub does once a pull request is merged, and its commits are merged nowhere as git sees it,
// i.e. its tip has no child: a pull request merged with a merge commit is merged as git sees it, and
// is drawn so. It is merged on origin when merging it into the branch it went into would change
// nothing (IGit.IsMergedByContentAsync). Which branch that was is not recorded either: the branch it
// was made from, as gmd inferred it, or else main, each on origin, where the merge was made.
//
// What is known is kept by commit ids, since the answer for two commits never changes: a branch tip
// merged into a commit of main is so for good, and one not merged is asked again once main moves.
[SingleInstance]
class MergedBranchService : IMergedBranchService
{
    readonly IGit git;
    readonly IRepoConfigService repoConfig;
    readonly object syncRoot = new();

    // The branch tips known to be merged, and the name of the branch they are merged into
    readonly Dictionary<string, string> mergedByTip = [];

    // The pairs of a branch tip and the tip of a branch it may be merged into, asked of git already
    readonly HashSet<(string Tip, string IntoTip)> asked = [];

    internal MergedBranchService(IGit git, IRepoConfigService repoConfig)
    {
        this.git = git;
        this.repoConfig = repoConfig;
    }

    record Candidate(string BranchName, string TipId, string IntoName, string IntoTipId);

    public Repo WithKnown(Repo repo)
    {
        Dictionary<string, string> merged = [];
        lock (syncRoot)
        {
            foreach (var c in Candidates(repo))
            {
                if (mergedByTip.TryGetValue(c.TipId, out var into))
                    merged.TryAdd(c.BranchName, into);
            }
        }
        return repo with { MergedOnOrigin = merged };
    }

    public bool HasUnknown(Repo repo)
    {
        lock (syncRoot)
        {
            return Unknown(repo).Any();
        }
    }

    public async Task<Result<Repo>> GetUpdatedRepoAsync(Repo repo)
    {
        List<Candidate> unknown;
        lock (syncRoot)
        {
            unknown = Unknown(repo).ToList();
        }

        foreach (var c in unknown)
        {
            var mergedResult = await git.IsMergedByContentAsync(c.IntoTipId, c.TipId, repo.Path);
            if (mergedResult is not bool isMerged)
            {
                // E.g. a git older than 2.38, which has no 'merge-tree --write-tree': not asked again
                Log.Warn($"Failed to tell if '{c.BranchName}' is merged into '{c.IntoName}', {mergedResult.Error}");
                isMerged = false;
            }

            lock (syncRoot)
            {
                asked.Add((c.TipId, c.IntoTipId));
                if (isMerged)
                    mergedByTip.TryAdd(c.TipId, c.IntoName);
            }
        }

        return WithKnown(repo);
    }

    // The branches a merged branch can be listed for: a local branch with its branch on origin, if
    // any, or a branch only origin has, with commits of their own, i.e. not a branch just made, whose
    // tip is another branch's commit and so merged into it, nor main or an integration branch, which
    // branches are merged into. One deleted on origin, or never pushed, is listed when it is merged as
    // git counts it into main or an integration branch, as origin has them, or by content on origin.
    // One origin has is listed when origin's is merged as git counts it, and so is its local branch,
    // if any: deleting it deletes both, and its local branch is not to lose work origin has not.
    public async Task<Result<IReadOnlyList<MergedBranch>>> GetMergedBranchesAsync(Repo repo)
    {
        var updatedResult = await GetUpdatedRepoAsync(repo);
        if (updatedResult is not Repo updated)
            return updatedResult.Error;

        var integrationNames = repoConfig.Get(repo.Path).IntegrationBranches;
        bool IsInto(Branch b) => b.IsMainBranch || WellKnownBranches.IsIntegrationName(b.NiceName, integrationNames);
        var intos = updated
            .AllBranches.Where(b => b.IsGitBranch && !b.IsDetached && IsInto(b))
            .Select(b => OnOrigin(updated, b.Name))
            .OfType<Branch>()
            .DistinctBy(b => b.Name)
            .ToList();

        Dictionary<string, string> mergedInto = [];
        foreach (var into in intos)
        {
            var namesResult = await git.GetMergedBranchNamesAsync(into.TipId, repo.Path);
            if (namesResult is not IReadOnlyList<string> names)
                return namesResult.Error;
            foreach (var name in names)
            {
                mergedInto.TryAdd(name, into.NiceName);
            }
        }

        List<MergedBranch> merged = [];
        foreach (var b in updated.AllBranches.Where(b => b.IsGitBranch && !b.IsDetached && !IsInto(b)))
        {
            // Only origin's remote branches, which deleting on origin deletes, not another remote's
            if (b.IsRemote && !b.Name.StartsWith(OriginPrefix))
                continue;
            // Each pair once, from its local branch, or from origin's when there is no local one
            if (b.IsRemote && IsPair(updated, b.LocalName, b))
                continue;
            var local = b.IsRemote ? null : b;
            var remote =
                b.IsRemote ? b
                : b.RemoteName != ""
                && updated.BranchByName.TryGetValue(b.RemoteName, out var r)
                && IsPair(updated, b.Name, r)
                    ? r
                : null;
            if (!HasCommitsOfItsOwn(updated, local, remote))
                continue;

            if (remote != null)
            {
                if (
                    mergedInto.TryGetValue(remote.Name, out var into)
                    && (local == null || mergedInto.ContainsKey(local.Name))
                )
                    merged.Add(ToMerged(updated, local, remote, into, false));
            }
            else if (mergedInto.TryGetValue(b.Name, out var into))
                merged.Add(ToMerged(updated, b, null, into, false));
            else if (updated.MergedOnOrigin.TryGetValue(b.Name, out var onOrigin))
                merged.Add(ToMerged(updated, b, null, onOrigin, true));
        }

        return merged.OrderByDescending(m => m.Time).ToList();
    }

    const string OriginPrefix = "origin/";

    // Whether a local branch and the origin branch it tracks are deleted as one: only a branch of
    // origin of the same name, never one that branches are merged into, e.g. 'origin/dev' tracked by a
    // branch made from it, nor someone else's of another name, which deleting the pair would delete
    static bool IsPair(Repo repo, string localName, Branch remote) =>
        localName != ""
        && repo.BranchByName.TryGetValue(localName, out var local)
        && !local.IsRemote
        && local.RemoteName == remote.Name
        && remote.IsGitBranch
        && remote.Name == OriginPrefix + localName;

    // Whether the tip of the branch, or of either branch of the pair, is a commit of the pair's own, or
    // of a branch of the same name: a local branch that does not track origin's of its name is no pair
    // with it, though their commits are drawn as one branch's, origin's
    static bool HasCommitsOfItsOwn(Repo repo, Branch? local, Branch? remote)
    {
        var niceName = (local ?? remote)!.NiceName;
        bool IsOwn(Branch? b) =>
            b != null
            && repo.CommitById.TryGetValue(b.TipId, out var tip)
            && (
                tip.BranchName == local?.Name
                || tip.BranchName == remote?.Name
                || repo.BranchByName.TryGetValue(tip.BranchName, out var tipBranch) && tipBranch.NiceName == niceName
            );
        return IsOwn(local) || IsOwn(remote);
    }

    // A pair's last commit is the newer of its two tips
    static MergedBranch ToMerged(Repo repo, Branch? local, Branch? remote, string into, bool isByContent)
    {
        var tips = new[] { local, remote }.OfType<Branch>().Select(b => repo.CommitById[b.TipId]);
        var tip = tips.MaxBy(c => c.AuthorTime)!;
        return new MergedBranch(
            local?.Name ?? remote!.NiceName,
            local?.Name ?? "",
            remote?.Name ?? "",
            into,
            isByContent,
            tip.Id,
            tip.Subject,
            tip.Author,
            tip.AuthorTime,
            local?.IsCurrent ?? false,
            local?.WorktreePath ?? ""
        );
    }

    // The candidates not known to be merged, and not asked about with the tips they have now
    IEnumerable<Candidate> Unknown(Repo repo) =>
        Candidates(repo).Where(c => !mergedByTip.ContainsKey(c.TipId) && !asked.Contains((c.TipId, c.IntoTipId)));

    // Each local branch deleted on origin and merged nowhere as git sees it, with each branch it may
    // have gone into: the branch it was made from first, then main
    static IEnumerable<Candidate> Candidates(Repo repo)
    {
        foreach (var b in repo.AllBranches.Where(b => !b.IsRemote && b.IsGitBranch && b.GoneRemoteName != ""))
        {
            if (!repo.HasUnmergedCommits(b))
                continue;

            var main = repo.AllBranches.FirstOrDefault(m => m.IsMainBranch)?.Name ?? "";
            var intos = new[] { b.ParentBranchName, main }
                .Select(name => OnOrigin(repo, name))
                .OfType<Branch>()
                .Where(into => into.Name != b.Name && into.LocalName != b.Name && into.TipId != b.TipId)
                .DistinctBy(into => into.Name);
            foreach (var into in intos)
            {
                yield return new Candidate(b.Name, b.TipId, into.NiceName, into.TipId);
            }
        }
    }

    // A branch as origin has it, or as it is here when origin has it not, or null when git has neither
    static Branch? OnOrigin(Repo repo, string name)
    {
        if (name == "" || !repo.BranchByName.TryGetValue(name, out var branch) || !branch.IsGitBranch)
            return null;
        return !branch.IsRemote && repo.BranchByName.TryGetValue(branch.RemoteName, out var remote) ? remote : branch;
    }
}
