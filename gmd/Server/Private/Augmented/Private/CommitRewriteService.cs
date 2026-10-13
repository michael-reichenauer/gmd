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
        if (c1.BranchName != c2.BranchName)
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

        // The commits on top of the squashed ones, to be picked back onto the squash, walked from
        // the local branch's tip. Not from the tip of the branch the commits are on: commits
        // already pushed are on the remote branch, whose tip is below the local one's when there
        // are commits on top not pushed yet, and those were squashed in with the others. A commit
        // that is not on the local branch's line, e.g. one only origin has when the two have
        // diverged, is refused before anything is changed, rather than the branch reset onto it.
        var preCommits = new List<Commit>();
        var c = repo.CommitById[local?.TipId ?? branch.TipId];
        while (c.Id != c1.Id)
        {
            preCommits.Add(c);
            if (c.ParentIds.Count == 0 || !repo.CommitById.TryGetValue(c.ParentIds[0], out var parent))
                return new Error(
                    $"Only commits on the current branch can be squashed, and {c1.Sid} is not on "
                        + $"'{local?.NiceNameUnique ?? branch.NiceNameUnique}'"
                );
            c = parent;
        }

        // Create a backup branch (in case of errors)
        var tmpName = $"squash-backup-{Guid.NewGuid().ToString()[..6]}";
        if (await git.CreateBranchAsync(tmpName, false, repo.Path) is Error backupError)
            return new Error("Failed to create backup branch", backupError);

        // Remove all prefix commits on current branch until the first commit to squash
        if (preCommits.Any())
        {
            if (await git.ResetHardUntilCommitAsync(id1, repo.Path) is Error resetError)
                return new Error("Failed to prepare for squash", resetError);
        }

        // Squash commits and commit
        if (await git.UncommitUntilCommitAsync(c2.ParentIds[0], repo.Path) is Error uncommitError)
            return new Error("Failed to squash commits", uncommitError);
        if (await git.CommitAllChangesAsync(message, false, repo.Path) is Error commitError)
            return new Error("Failed to commit squashed commits", commitError);

        // Cherry pick prefix commits back to current branch
        foreach (var commit in preCommits.AsEnumerable().Reverse())
        {
            if (await git.CherryPickAsync(commit.Id, repo.Path) is Error pickError)
                return new Error($"Failed to cherry pick {commit.Sid}", pickError);
            if (await git.CommitAllChangesAsync(commit.Message, false, repo.Path) is Error pickCommitError)
                return new Error($"Failed to commit cherry pick {commit.Sid}", pickCommitError);
        }

        // The squash is several moves of the branch, one entry each in its reflog: the reset of
        // the newer commits, if any, the reset of the squashed ones, their commit and the commit of
        // each newer one picked back. Recorded as one change, so that Undo takes it back whole.
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
                    Moves = (preCommits.Any() ? 1 : 0) + 2 + preCommits.Count,
                }
            );
        }

        // Remove temp backup branch
        if (await git.DeleteLocalBranchAsync(tmpName, true, repo.Path) is Error deleteError)
            return new Error("Failed to delete backup branch", deleteError);

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
    // rewritten: a commit on origin already, or one another branch or a tag is on, is left as it is.
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

    // Takes an older commit of the current branch, one not pushed yet, out of the branch: the newer
    // commits are replayed onto its parent. One that conflicts without it stops the rebase there.
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

    // The current local branch, when only it has the commit: no remote branch, i.e. it is not pushed,
    // and no other branch or tag, which would be left on the old commits. Asked of git rather than
    // taken from the repo read, since the commits are rewritten.
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
        if (refs.FirstOrDefault(r => r.StartsWith("refs/remotes/")) is string remote)
            return new Error($"{commit.Sid} is on '{remote["refs/remotes/".Length..]}' already, i.e. pushed");
        if (refs.FirstOrDefault(r => r.StartsWith("refs/tags/")) is string tag)
            return new Error($"The tag '{tag["refs/tags/".Length..]}' is on {commit.Sid} or a commit after it");
        if (refs.FirstOrDefault(r => r != $"refs/heads/{local.Name}") is string other)
            return new Error($"'{other["refs/heads/".Length..]}' is on {commit.Sid} or a commit after it");
        return local;
    }
}
