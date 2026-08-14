namespace gmd.Git;

interface IGit
{
    R<string> RootPath(string path);
    Task<R<string>> Version();
    string CurrentAuthor { get; }

    Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd);
    Task<R<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd);
    Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd);
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd);
    Task<R<Status>> GetStatusAsync(string wd);
    Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd);
    Task<R<CommitDiff>> GetUncommittedDiff(int contextLines, string wd);
    Task<R<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd);
    Task<R<Blame>> GetBlameAsync(string path, string reference, string wd);
    Task<R<CommitDiff>> GetPreviewMergeDiffAsync(string sha1, string sha2, string message, int contextLines, string wd);
    Task<R<CommitDiff>> GetDiffRangeAsync(string sha1, string sha2, string message, int contextLines, string wd);
    Task<R> RunDiffToolAsync(string path, string wd);
    Task<R> RunMergeToolAsync(string path, string wd);
    Task<R> FetchAsync(string wd);
    Task<R> PushBranchAsync(string name, string wd);
    Task<R> PushCurrentBranchAsync(bool isForce, string wd);
    Task<R> PullCurrentBranchAsync(string wd);
    Task<R> PullBranchAsync(string name, string wd);
    Task<R> PushRefForceAsync(string name, string wd);
    Task<R> PullRefAsync(string name, string wd);
    Task<R> CloneAsync(string uri, string path, string wd);
    Task<R> InitRepoAsync(string path, string wd);
    Task<R> CheckoutAsync(string name, string wd);
    Task<R> MergeBranchAsync(string name, string wd);
    Task<R> RebaseBranchAsync(string name, string wd);
    Task<R> RebaseOntoAsync(string newBase, string oldBase, string wd);
    Task<R> CherryPickAsync(string sha, string wd);
    Task<R> AbortOperationAsync(string wd);
    Task<R> ContinueOperationAsync(string wd);
    Task<R> SkipOperationAsync(string wd);
    Task<R<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd);
    Task<R<ConflictFile>> GetConflictFileAsync(string path, ConflictKind kind, string wd);
    Task<R<ConflictFile>> WithBaseAsync(ConflictFile file, string wd);
    Task<R> WriteConflictFileAsync(ConflictFile file, string wd);
    Task<R> ResolveConflictFileAsync(string path, ConflictKind kind, IReadOnlyList<HunkResolution> choices, string wd);
    Task<R> MarkResolvedAsync(string path, string wd);
    Task<R> UnresolveAsync(string path, string wd);
    Task<R> UseWholeFileAsync(string path, bool isOurs, string wd);
    Task<R> DeleteConflictedAsync(string path, string wd);
    Task<R> CreateBranchAsync(string name, bool isCheckout, string wd);
    Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd);
    Task<R> RenameBranchAsync(string oldName, string newName, string wd);
    Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd);
    Task<R> DeleteRemoteBranchAsync(string name, string wd);
    Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd);
    Task<R> UndoAllUncommittedChangesAsync(string wd);
    Task<R> UndoUncommittedFileAsync(string path, string wd);
    Task<R> CleanWorkingFolderAsync(string wd);
    Task<R> UndoCommitAsync(string id, int parentIndex, string wd);
    Task<R> UncommitLastCommitAsync(string wd);
    Task<R> UncommitUntilCommitAsync(string id, string wd);
    Task<R<string>> GetValueAsync(string key, string wd);
    Task<R> SetValueAsync(string key, string value, string wd);
    Task<R> PushValueAsync(string key, string wd);
    Task<R> PullValueAsync(string key, string wd);
    Task<R> StashAsync(string message, string wd);
    Task<R<IReadOnlyList<Stash>>> GetStashesAsync(string wd);
    Task<R> StashPopAsync(string name, string wd);
    Task<R> StashDropAsync(string name, string wd);
    Task<R<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd);
    Task<R> AddTagAsync(string name, string commitId, string wd);
    Task<R> AddAnnotatedTagAsync(string name, string message, string commitID, string wd);
    Task<R> RemoveTagAsync(string name, string wd);
    Task<R> PushTagAsync(string name, string wd);
    Task<R> DeleteRemoteTagAsync(string name, string wd);
    Task<R> ResetHardUntilCommitAsync(string id, string wd);
}

public record Commit(
    string Id,
    string Sid,
    string[] ParentIds,
    string Subject,
    string Message,
    string Author,
    DateTime AuthorTime,
    DateTime CommitTime
);

public record Branch(
    string Name,
    string TipID,
    bool IsCurrent,
    bool IsRemote,
    string RemoteName, // The remote name for a local branch
    bool IsDetached,
    int AheadCount, // Number of commits on local branch not yet synced up to remote branch
    int BehindCount // Number of commits on remote branch not yet synced down to local branch
);

// What git is in the middle of, i.e. something it has stopped part way through and has to be told
// to finish or abort. No porcelain command reports this, so it is probed from the files git leaves
// in the git dir, which is what git's own wt_status_get_state() does.
public enum GitOperation
{
    None,
    Merge,
    CherryPick,
    Revert,
    Rebase,
    Am,
}

// How git left a path in the index, i.e. the porcelain XY code. This is the only place the kind of
// a conflict exists, and it decides what can be offered for it: a modify/delete has no text to
// merge, only a keep-or-delete choice, and neither side of an add/add has a common ancestor.
public enum ConflictKind
{
    BothModified, // UU
    BothAdded, // AA
    BothDeleted, // DD
    AddedByUs, // AU
    AddedByThem, // UA
    DeletedByThem, // UD
    DeletedByUs, // DU
}

public record ConflictedFile(string Path, ConflictKind Kind)
{
    public override string ToString() => $"{Kind} {Path}";
}

public record Status(
    int Modified,
    int Added,
    int Deleted,
    int Conflicted,
    int Renamed,
    GitOperation Operation,
    string MergeMessage,
    string MergeHeadId,
    // Which branch is being rebased and how far it has got, for naming the operation to the user.
    // Empty and 0 for everything except a rebase, which is the only operation with several steps.
    string OperationBranchName,
    int OperationStep,
    int OperationTotal,
    // Whether making a commit is the whole of what is left of the operation, rather than telling
    // git to '--continue'. True for a merge, which has no --continue at all, and for gmd's own
    // Cherry Pick and Undo/Revert Commit, which run '--no-commit' and leave a single change staged
    // for the commit dialog. False for a rebase or an 'am', which replay a series and are not
    // finished by the commit git stopped on, and for a cherry pick or revert of several commits
    // started outside gmd, where a commit would make one of them and leave the rest unapplied.
    bool IsFinishedByCommit,
    string[] ModifiedFiles,
    string[] AddedFiles,
    string[] DeletedFiles,
    ConflictedFile[] Conflicts,
    string[] RenamedSourceFiles,
    string[] RenamedTargetFiles
)
{
    // True while any operation is in progress, not merely a merge — the name predates rebase and
    // cherry-pick being detected. This is what 'git add .' and 'git commit -a' have to be guarded
    // on: either of them stages an unmerged path with whatever text the working tree holds, which
    // resolves the conflict with the markers still in it and drops the stages for good.
    public bool IsMerging => Operation != GitOperation.None;

    public string[] ConflictsFiles => Conflicts.Select(c => c.Path).ToArray();

    public override string ToString() => $"M:{Modified},A:{Added},D:{Deleted},C:{Conflicted},R:{Renamed}";
}

public record Tag(string Name, string CommitId);

public record Stash(string Id, string Name, string Branch, string ParentId, string IndexId, string Message);

record CommitDiff(string Id, string Author, DateTime Time, string Message, IReadOnlyList<FileDiff> FileDiffs);

record FileDiff(
    string PathBefore,
    string PathAfter,
    bool IsRenamed,
    bool IsBinary,
    DiffMode DiffMode,
    IReadOnlyList<SectionDiff> SectionDiffs
);

record SectionDiff(
    string ChangedIndexes,
    int LeftLine,
    int LeftCount,
    int RightLine,
    int RightCount,
    IReadOnlyList<LineDiff> LineDiffs
);

record LineDiff(DiffMode DiffMode, string Line);

// The blame of one file, i.e. which commit last changed each line. The commits are kept in a
// dictionary the lines reference by id, since a commit typically covers many consecutive lines.
record Blame(
    string Path,
    string Reference,
    IReadOnlyList<BlameLine> Lines,
    IReadOnlyDictionary<string, BlameCommit> CommitById
);

record BlameLine(string CommitId, int LineNbr, int OriginalLineNbr, string Text);

record BlameCommit(
    string Id,
    string Sid,
    string Author,
    string AuthorMail,
    DateTime AuthorTime,
    string Subject,
    bool IsUncommitted,
    bool IsBoundary, // The first commit of the history, so it has no previous version to blame
    string PreviousId, // The commit and path to blame to see the version before this commit
    string PreviousPath,
    string Path // The path the file had in this commit, which differs from Blame.Path if renamed
);

enum DiffMode
{
    DiffModified,
    DiffAdded,
    DiffRemoved,
    DiffSame,
    DiffConflicts,
    DiffConflictStart,
    DiffConflictBase,
    DiffConflictSplit,
    DiffConflictEnd,
}
