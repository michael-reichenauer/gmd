using gmd.Common;
using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// See ICommitRewriteService
class CommitRewriteService : ICommitRewriteService
{
    readonly IGit git;
    readonly IRepoConfigService repoConfig;

    internal CommitRewriteService(IGit git, IRepoConfigService repoConfig)
    {
        this.git = git;
        this.repoConfig = repoConfig;
    }

    public async Task<Result> SquashCommitsAsync(Repo repo, string id1, string id2, string message)
    {
        var c1 = repo.CommitById[id1];
        var c2 = repo.CommitById[id2];
        if (!c2.ParentIds.Any())
            return new Error("Last commit does not have a parent");
        if (c1.BranchPrimaryName != c2.BranchPrimaryName)
            return new Error("Commits are not on the same branch");
        var branch = repo.BranchByName[c1.BranchName];

        // Both flags: IsLocalCurrent is only ever set on a *remote* branch whose local branch
        // is current, so asking for it alone refused every commit that had not been pushed yet.
        // The same check is made before the dialog is shown, in CommitCommands.SquashCommits.
        if (!branch.IsCurrent && !branch.IsLocalCurrent)
            return new Error("Commits not on current branch");

        // The local branch, which the squash moves, and where it was before, for Undo, which the
        // squash is recorded for below. A squash waits for a clean tree, so the local branch's tip
        // is git's own, not the uncommitted row.
        var local = repo.AllBranches.FirstOrDefault(b => b.IsCurrent && !b.IsRemote);

        // The commits are on the local branch's line, walked down from its tip, and the ones above
        // them are the ones put back on the squash. Not walked from the tip of the branch the commits
        // are on: commits already pushed are on the remote branch, whose tip is below the local one's
        // when there are commits on top not pushed yet, and those were squashed in with the others. A
        // commit that is not on the local branch's line, e.g. one only origin has when the two have
        // diverged, is refused before anything is changed, rather than the branch moved onto it.
        var c = repo.CommitById[local?.TipId ?? branch.TipId];
        while (c.Id != c1.Id)
        {
            if (c.ParentIds.Count == 0 || !repo.CommitById.TryGetValue(c.ParentIds[0], out var parent))
                return new Error(
                    $"Only commits on the current branch can be squashed, and {c1.Sid} is not on "
                        + $"'{local?.NiceNameUnique ?? branch.NiceNameUnique}'"
                );
            c = parent;
        }

        // The squash is a commit with the files of the newest of the commits on the parent of the
        // oldest, made without moving anything, onto which the commits on top of them are then moved,
        // the one move of the branch. They were made on those very files, so they cannot conflict,
        // and nothing has changed until the branch moves. The branch used to be reset first and the
        // commits on top cherry-picked back one by one, so a failure among them left it half done,
        // with a backup branch, and a merge among them stopped it, cherry-pick refusing a merge.
        var squashResult = await git.CommitTreeAsync(id1, c2.ParentIds[0], message, repo.Path);
        if (squashResult is not string squashId)
            return new Error("Failed to squash commits", squashResult.Error);
        if (await git.RebaseOntoKeepingMergesAsync(squashId, id1, repo.Path) is Error rebaseError)
            return new Error("Failed to put the commits on top back on the squash", rebaseError);

        // One move of the branch, the rebase, recorded as the squash, so that Undo names it
        if (local != null)
        {
            UndoService.Record(
                repoConfig,
                repo,
                local.Name,
                new RecordedStep
                {
                    Kind = nameof(StepKind.Squash),
                    Name = message.Split('\n')[0].Trim(),
                    BeforeId = local.TipId,
                    Moves = 1,
                }
            );
        }

        return Result.Ok;
    }

    // Gives an older commit of the current branch, one not pushed yet, a new message and the changes
    // in these files, none for a new message alone. It is git's own way of doing it: an 'amend!'
    // commit carrying both is made on top, and a rebase with --autosquash folds it into the commit
    // and replays the newer ones after it. The changes left out stay as they are, put aside and back
    // by the rebase. A later commit that conflicts with the changes stops the rebase there, to be
    // continued or aborted as any rebase; an abort leaves the 'amend!' commit on top, which Undo
    // takes back to uncommitted changes.
    //
    // What the menu checks (CommitRewrite) is checked here against git too, since the commits are
    // rewritten: a commit on another branch of origin, or one another branch or a tag is on, is left
    // as it is. One on origin's copy of the branch is rewritten, and a force push replaces it there.
    public async Task<Result> AmendOlderCommitAsync(Repo repo, string id, string message, IReadOnlyList<string> paths)
    {
        var commit = repo.CommitById[id];
        var checkResult = await CheckRewritableAsync(repo, commit);
        if (checkResult is not Branch local)
            return new Error($"Cannot amend {commit.Sid}", checkResult.Error);

        // git matches the 'amend!' subject with the first commit of the rebase of that subject,
        // which is this one, since the rebase starts at it
        var amendMessage = $"amend! {commit.Subject}\n\n{message}";
        if (await git.CommitFilesAsync(amendMessage, false, paths, repo.Path) is Error commitError)
            return new Error($"Failed to commit the changes to amend {commit.Sid} with", commitError);

        var baseId = commit.ParentIds.Count == 0 ? "" : $"{id}^";
        if (await git.AutosquashAsync(baseId, repo.Path) is Error rebaseError)
            return new Error($"Failed to amend {commit.Sid}", rebaseError);

        // Two moves of the branch, the 'amend!' commit and the rebase, recorded as one change
        UndoService.Record(
            repoConfig,
            repo,
            local.Name,
            new RecordedStep
            {
                Kind = nameof(StepKind.Amend),
                Name = message.Split('\n')[0].Trim(),
                BeforeId = repo.CurrentCommit().Id,
                Moves = 2,
            }
        );
        return Result.Ok;
    }

    // Takes an older commit of the current branch out of the branch: the newer commits are replayed
    // onto its parent. One that conflicts without it stops the rebase there.
    public async Task<Result> DropCommitAsync(Repo repo, string id)
    {
        var commit = repo.CommitById[id];
        var checkResult = await CheckRewritableAsync(repo, commit);
        if (checkResult is not Branch local)
            return new Error($"Cannot drop {commit.Sid}", checkResult.Error);
        if (commit.ParentIds.Count == 0)
            return new Error("The first commit cannot be dropped");

        if (await git.DropCommitAsync(id, repo.Path) is Error dropError)
            return new Error($"Failed to drop {commit.Sid}", dropError);

        UndoService.Record(
            repoConfig,
            repo,
            local.Name,
            new RecordedStep
            {
                Kind = nameof(StepKind.Drop),
                Name = commit.Subject.Trim(),
                BeforeId = repo.CurrentCommit().Id,
                Moves = 1,
            }
        );
        return Result.Ok;
    }

    // Swaps a commit of the current branch with the one after it on the branch, or before it: an
    // interactive rebase from the older of the two, with the two the other way round in its list and
    // the newer ones as they were. The older is the one git checks, the rewrite starting there. One
    // that conflicts with the other, which then comes first, stops the rebase there.
    public async Task<Result> MoveCommitAsync(Repo repo, string id, bool isUp)
    {
        var commit = repo.CommitById[id];
        var local = repo.AllBranches.FirstOrDefault(b => b.IsCurrent && !b.IsRemote && !b.IsDetached);
        if (local == null)
            return new Error("Not on a branch");

        // The commits from the tip down to the older of the two, newest first, down git's first parents
        List<Commit> commits = [];
        var c = repo.CurrentCommit();
        var olderId = isUp ? id : commit.GitParentIds.FirstOrDefault() ?? "";
        while (true)
        {
            commits.Add(c);
            if (c.Id == olderId)
                break;
            if (c.GitParentIds.Count == 0 || !repo.CommitById.TryGetValue(c.GitParentIds[0], out var parent))
                return new Error(
                    $"{commit.Sid} has no commit {(isUp ? "after" : "before")} it on '{local.NiceNameUnique}'"
                );
            c = parent;
        }
        var older = commits[^1];
        if (commits.Count < 2 || older.GitParentIds.Count == 0)
            return new Error($"{commit.Sid} has no commit {(isUp ? "after" : "before")} it to move past");

        var checkResult = await CheckRewritableAsync(repo, older);
        if (checkResult is not Branch)
            return new Error($"Cannot move {commit.Sid}", checkResult.Error);

        var order = commits.Select(x => x.Id).Reverse().ToList();
        (order[0], order[1]) = (order[1], order[0]);
        if (await git.ReorderCommitsAsync(older.GitParentIds[0], order, repo.Path) is Error moveError)
            return new Error($"Failed to move {commit.Sid}", moveError);

        UndoService.Record(
            repoConfig,
            repo,
            local.Name,
            new RecordedStep
            {
                Kind = nameof(StepKind.Move),
                Name = commit.Subject.Trim(),
                BeforeId = repo.CurrentCommit().Id,
                Moves = 1,
            }
        );
        return Result.Ok;
    }

    // The current local branch, when only it and its copy on origin have the commit: no other remote
    // branch, e.g. origin's main once it is merged there, nor another branch or a tag, which would be
    // left on the old commits. Asked of git rather than taken from the repo read, since the commits are
    // rewritten.
    async Task<Result<Branch>> CheckRewritableAsync(Repo repo, Commit commit)
    {
        var local = repo.AllBranches.FirstOrDefault(b => b.IsCurrent && !b.IsRemote && !b.IsDetached);
        if (local == null)
            return new Error("Not on a branch");

        var refsResult = await git.GetRefsContainingAsync(commit.Id, repo.Path);
        if (refsResult is not IReadOnlyList<string> refs)
            return refsResult.Error;
        if (!refs.Contains($"refs/heads/{local.Name}"))
            return new Error($"{commit.Sid} is not on '{local.NiceNameUnique}'");
        // origin/HEAD names another remote branch, e.g. origin/main, which is listed on its own
        var ownRemote = local.RemoteName == "" ? "" : $"refs/remotes/{local.RemoteName}";
        refs = refs.Where(r => !(r.StartsWith("refs/remotes/") && r.EndsWith("/HEAD"))).ToList();
        if (refs.FirstOrDefault(r => r.StartsWith("refs/remotes/") && r != ownRemote) is string remote)
            return new Error($"{commit.Sid} is on '{remote["refs/remotes/".Length..]}' already");
        if (refs.FirstOrDefault(r => r.StartsWith("refs/tags/")) is string tag)
            return new Error($"The tag '{tag["refs/tags/".Length..]}' is on {commit.Sid} or a commit after it");
        if (refs.FirstOrDefault(r => r != $"refs/heads/{local.Name}" && r != ownRemote) is string other)
            return new Error($"'{other["refs/heads/".Length..]}' is on {commit.Sid} or a commit after it");
        return local;
    }
}
