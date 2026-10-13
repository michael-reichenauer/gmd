using gmd.Common;
using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// See IDeletedBranchService
class DeletedBranchService : IDeletedBranchService
{
    readonly IGit git;
    readonly IRepoConfigService repoConfig;

    internal DeletedBranchService(IGit git, IRepoConfigService repoConfig)
    {
        this.git = git;
        this.repoConfig = repoConfig;
    }

    // Deletes a branch on origin, here or both, origin's first, and records each side once it is gone,
    // so that Restore Deleted Branch can bring it back: git deletes a branch's reflog with the branch,
    // so nothing else would say where it was. A name is "" for a side not to delete. The branches are
    // looked up in the repo given, which can be newer than the one they were picked in, since a
    // refresh runs while a dialog is shown, so one can be gone.
    public async Task<Result> DeleteBranchAsync(Repo repo, string localName, string remoteName, bool isForce)
    {
        Branch? GitBranch(string name) => repo.BranchByName.TryGetValue(name, out var b) && b.IsGitBranch ? b : null;
        string SubjectOf(string id) => repo.CommitById.TryGetValue(id, out var c) ? c.Subject : "";

        var local = localName != "" ? GitBranch(localName) : null;
        var remote = remoteName != "" ? GitBranch(remoteName) : null;
        var missingName =
            localName != "" && local == null ? localName
            : remoteName != "" && remote == null ? remoteName
            : "";
        if (missingName != "")
            return new Error($"There is no branch '{missingName}' any more, so nothing was deleted");

        if (remote != null)
        {
            if (await git.DeleteRemoteBranchAsync(remote.Name, repo.Path) is Error e)
                return new Error($"Failed to delete remote branch {remote.Name}", e);
            RecordDelete(
                repo,
                new RecordedDelete
                {
                    Name = remote.LocalName,
                    RemoteName = remote.Name,
                    RemoteTipId = remote.TipId,
                    RemoteSubject = SubjectOf(remote.TipId),
                    Time = DateTime.Now,
                }
            );
        }

        // Both sides deleted are one record, which takes the place of origin's recorded above
        if (local != null)
        {
            if (await git.DeleteLocalBranchAsync(local.Name, isForce, repo.Path) is Error e)
                return new Error($"Failed to delete local branch {local.Name}", e);
            RecordDelete(
                repo,
                new RecordedDelete
                {
                    Name = local.Name,
                    TipId = local.TipId,
                    Subject = SubjectOf(local.TipId),
                    RemoteName = remote?.Name ?? local.RemoteName,
                    RemoteTipId = remote?.TipId ?? "",
                    RemoteSubject = remote != null ? SubjectOf(remote.TipId) : "",
                    Time = DateTime.Now,
                }
            );
        }

        return Result.Ok;
    }

    // The branches gmd deleted that can be restored, newest first, see DeletedBranchRecords.Restorable.
    // Read when asked, since whether git still has their commits is a question for git. The sides
    // that cannot be restored are forgotten.
    public async Task<Result<IReadOnlyList<DeletedBranch>>> GetDeletedBranchesAsync(Repo repo)
    {
        var records = repoConfig.Get(repo.Path).DeletedBranches.ToList();
        if (records.Count == 0)
            return new List<DeletedBranch>();

        var existingResult = await git.GetExistingCommitIdsAsync(DeletedBranchRecords.TipIds(records), repo.Path);
        if (existingResult is not IReadOnlySet<string> existing)
            return new Error("Failed to look up the commits of the deleted branches", existingResult.Error);

        var restorable = records
            .Select(r => (Record: r, Deleted: DeletedBranchRecords.Restorable(r, repo, existing)))
            .ToList();
        var unrestorable = restorable
            .Select(r => DeletedBranchRecords.Unrestorable(r.Record, r.Deleted))
            .OfType<RecordedDelete>()
            .ToList();
        if (unrestorable.Count > 0)
        {
            repoConfig.Set(
                repo.Path,
                c => unrestorable.ForEach(sides => DeletedBranchRecords.Forget(c.DeletedBranches, sides))
            );
        }

        return restorable.Select(r => r.Deleted).OfType<DeletedBranch>().ToList();
    }

    // Brings a deleted branch back, the sides asked for: origin's pushed back at its tip, but only
    // while origin has no branch of the name, so one someone pushed since is not overwritten, and the
    // local branch at its tip, tracking the remote branch it tracked. Origin's first, since that is
    // what can be refused, and a refusal then leaves nothing half done. Each side is forgotten in the
    // records once it is back, so a later delete of the name is not joined with it.
    public async Task<Result> RestoreBranchAsync(Repo repo, DeletedBranch deleted, bool isLocal, bool isRemote)
    {
        isLocal = isLocal && deleted.IsLocal;
        isRemote = isRemote && deleted.IsRemote;

        if (isRemote)
        {
            if (await git.PushRestoreAsync(deleted.RemoteName, deleted.RemoteTipId, "", repo.Path) is Error pushError)
            {
                return pushError is CmdError cmdError && cmdError.Output.Contains("stale info")
                    ? new Error(
                        $"Origin has a branch '{deleted.RemoteName}' again, which was left as it is, so nothing was restored",
                        pushError
                    )
                    : new Error($"Failed to push '{deleted.RemoteName}' back, so nothing was restored", pushError);
            }
            ForgetRestored(repo, deleted, false, true);
        }

        if (isLocal)
        {
            if (
                await git.CreateBranchFromCommitAsync(deleted.Name, deleted.TipId, false, repo.Path)
                is Error createError
            )
            {
                return new Error(
                    isRemote
                        ? $"Restored '{deleted.RemoteName}', but failed to create '{deleted.Name}' again"
                        : $"Failed to create '{deleted.Name}' again",
                    createError
                );
            }
            ForgetRestored(repo, deleted, true, false);

            // Git deleted what the branch tracked with it, which is written back whether the remote
            // branch is there or not: deleted too and not restored yet, the branch tracks it once it is
            if (
                deleted.RemoteName != ""
                && await git.SetUpstreamAsync(deleted.Name, deleted.RemoteName, repo.Path) is Error upstreamError
            )
            {
                var where = isRemote ? " here and on origin" : "";
                return new Error(
                    $"Restored '{deleted.Name}'{where}, but failed to make it track '{deleted.RemoteName}'",
                    upstreamError
                );
            }
        }

        return Result.Ok;
    }

    // Records a branch gmd deleted, see RepoConfig.DeletedBranches
    void RecordDelete(Repo repo, RecordedDelete deleted) =>
        repoConfig.Set(repo.Path, c => DeletedBranchRecords.Add(c.DeletedBranches, deleted));

    void ForgetRestored(Repo repo, DeletedBranch deleted, bool isLocal, bool isRemote) =>
        repoConfig.Set(
            repo.Path,
            c =>
                DeletedBranchRecords.Forget(
                    c.DeletedBranches,
                    DeletedBranchRecords.Restored(deleted, isLocal, isRemote)
                )
        );
}
