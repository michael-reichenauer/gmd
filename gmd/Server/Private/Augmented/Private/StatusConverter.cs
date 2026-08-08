using GitConflictedFile = gmd.Git.ConflictedFile;
using GitConflictKind = gmd.Git.ConflictKind;
using GitOperationKind = gmd.Git.GitOperation;
using GitStatus = gmd.Git.Status;

namespace gmd.Server.Private.Augmented.Private;

// The git status as the layers above see it. The two models have the same shape and are converted
// member for member, as ViewRepoConverter does for DiffMode, so that nothing above the Git layer
// names a gmd.Git type.
//
// One class rather than a copy in each caller: Augmenter and WorkRepoConverter both need this and
// used to hold the same conversion twice.
static class StatusConverter
{
    public static Status ToStatus(GitStatus s) =>
        new Status(
            s.Modified,
            s.Added,
            s.Deleted,
            s.Conflicted,
            s.Renamed,
            ToOperation(s.Operation),
            s.MergeMessage,
            s.MergeHeadId,
            s.OperationBranchName,
            s.OperationStep,
            s.OperationTotal,
            s.ModifiedFiles,
            s.AddedFiles,
            s.DeletedFiles,
            s.Conflicts.Select(ToConflictedFile).ToArray(),
            s.RenamedSourceFiles,
            s.RenamedTargetFiles
        );

    static ConflictedFile ToConflictedFile(GitConflictedFile f) => new ConflictedFile(f.Path, ToConflictKind(f.Kind));

    // Both switches are exhaustive and fail fast on an unmapped member, as ViewRepoConverter does,
    // so adding one to the Git enum and forgetting it here is loud rather than silent.
    static GitOperation ToOperation(GitOperationKind operation) =>
        operation switch
        {
            GitOperationKind.None => GitOperation.None,
            GitOperationKind.Merge => GitOperation.Merge,
            GitOperationKind.CherryPick => GitOperation.CherryPick,
            GitOperationKind.Revert => GitOperation.Revert,
            GitOperationKind.Rebase => GitOperation.Rebase,
            GitOperationKind.Am => GitOperation.Am,
            _ => throw Asserter.FailFast($"Unknown operation {operation}"),
        };

    static ConflictKind ToConflictKind(GitConflictKind kind) =>
        kind switch
        {
            GitConflictKind.BothModified => ConflictKind.BothModified,
            GitConflictKind.BothAdded => ConflictKind.BothAdded,
            GitConflictKind.BothDeleted => ConflictKind.BothDeleted,
            GitConflictKind.AddedByUs => ConflictKind.AddedByUs,
            GitConflictKind.AddedByThem => ConflictKind.AddedByThem,
            GitConflictKind.DeletedByThem => ConflictKind.DeletedByThem,
            GitConflictKind.DeletedByUs => ConflictKind.DeletedByUs,
            _ => throw Asserter.FailFast($"Unknown conflict kind {kind}"),
        };
}
