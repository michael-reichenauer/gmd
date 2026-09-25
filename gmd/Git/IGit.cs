namespace gmd.Git;

interface IGit
{
    Result<string> RootPath(string path);
    Task<Result<string>> Version();
    string CurrentAuthor { get; }

    Task<Result<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd);

    // The ids of the commits that changed a file whose path contains the text, for a search
    Task<Result<IReadOnlyList<string>>> GetIdsChangingFilesAsync(string pathText, int maxCount, string wd);
    Task<Result<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd);
    Task<Result<IReadOnlyList<string>>> GetFileAsync(string reference, string wd);
    Task<Result<IReadOnlyList<Branch>>> GetBranchesAsync(string wd);
    Task<Result<Status>> GetStatusAsync(string wd);

    // The status of a worktree someone else is working in, read without taking its index lock
    Task<Result<Status>> GetStatusWithoutLocksAsync(string wd);
    Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<Result<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd);
    Task<Result<CommitDiff>> GetUncommittedDiff(int contextLines, string wd);
    Task<Result<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd);
    Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd);
    Task<Result<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    );
    Task<Result<CommitDiff>> GetDiffRangeAsync(string sha1, string sha2, string message, int contextLines, string wd);
    Task<Result> RunDiffToolAsync(string path, string wd);
    Task<Result> RunMergeToolAsync(string path, string wd);
    Task<Result> FetchAsync(string wd);

    // The URL of the remote 'origin', as git uses it (insteadOf rules applied), empty when there is
    // no such remote
    Task<Result<string>> GetRemoteUrlAsync(string wd);
    Task<Result> PushBranchAsync(string name, string wd);
    Task<Result> PushCurrentBranchAsync(bool isForce, string wd);
    Task<Result> PullCurrentBranchAsync(string wd);
    Task<Result> PullBranchAsync(string name, string wd);
    Task<Result> PushRefForceAsync(string name, string wd);
    Task<Result> PullRefAsync(string name, string wd);
    Task<Result> CloneAsync(string uri, string path, string wd);
    Task<Result> InitRepoAsync(string path, string wd);
    Task<Result> CheckoutAsync(string name, string wd);
    Task<Result> MergeBranchAsync(string name, string wd);
    Task<Result> RebaseBranchAsync(string name, string wd);
    Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd);
    Task<Result> CherryPickAsync(string sha, string wd);
    Task<Result> AbortOperationAsync(string wd);
    Task<Result> ContinueOperationAsync(string wd);
    Task<Result> SkipOperationAsync(string wd);
    Task<Result<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd);
    Task<Result<ConflictFile>> GetConflictFileAsync(string path, ConflictKind kind, string wd);
    Task<Result<ConflictFile>> WithBaseAsync(ConflictFile file, string wd);
    Task<Result> WriteConflictFileAsync(ConflictFile file, string wd);
    Task<Result> ResolveConflictFileAsync(
        string path,
        ConflictKind kind,
        IReadOnlyList<HunkResolution> choices,
        string wd
    );
    Task<Result> MarkResolvedAsync(string path, string wd);
    Task<Result> UnresolveAsync(string path, string wd);
    Task<Result> UseWholeFileAsync(string path, bool isOurs, string wd);
    Task<Result> DeleteConflictedAsync(string path, string wd);
    Task<Result> CreateBranchAsync(string name, bool isCheckout, string wd);
    Task<Result> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd);
    Task<Result> RenameBranchAsync(string oldName, string newName, string wd);
    Task<Result> DeleteLocalBranchAsync(string name, bool isForced, string wd);
    Task<Result> DeleteRemoteBranchAsync(string name, string wd);
    Task<Result<IReadOnlyList<Tag>>> GetTagsAsync(string wd);
    Task<Result> UndoAllUncommittedChangesAsync(string wd);
    Task<Result> UndoUncommittedFileAsync(string path, string wd);
    Task<Result> CleanWorkingFolderAsync(string wd);
    Task<Result> UndoCommitAsync(string id, int parentIndex, string wd);
    Task<Result> UncommitLastCommitAsync(string wd);
    Task<Result> UncommitUntilCommitAsync(string id, string wd);
    Task<Result<string>> GetValueAsync(string key, string wd);
    Task<Result> SetValueAsync(string key, string value, string wd);
    Task<Result> PushValueAsync(string key, string wd);
    Task<Result> PullValueAsync(string key, string wd);
    Task<Result> StashAsync(string message, string wd);
    Task<Result<IReadOnlyList<Stash>>> GetStashesAsync(string wd);
    Task<Result> StashPopAsync(string name, string wd);
    Task<Result> StashDropAsync(string name, string wd);
    Task<Result<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd);
    Task<Result> AddTagAsync(string name, string commitId, string wd);
    Task<Result> AddAnnotatedTagAsync(string name, string message, string commitID, string wd);
    Task<Result> RemoveTagAsync(string name, string wd);
    Task<Result> PushTagAsync(string name, string wd);
    Task<Result> DeleteRemoteTagAsync(string name, string wd);
    Task<Result> ResetHardUntilCommitAsync(string id, string wd);
    Task<Result<IReadOnlyList<Worktree>>> GetWorktreesAsync(string wd);
    Task<Result> AddWorktreeAsync(string path, string branchName, bool isNewBranch, string startPoint, string wd);
    Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd);
    Task<Result> PruneWorktreesAsync(string wd);
    Task<Result<IReadOnlyList<string>>> GetIgnoredPathsAsync(IReadOnlyList<string> paths, string wd);
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
    int BehindCount, // Number of commits on remote branch not yet synced down to local branch
    // Checked out in another worktree of this repository (the '+' marker of 'git branch'), so it
    // can be neither checked out here nor deleted. Which worktree is answered by the worktree list.
    bool IsCheckedOutElsewhere = false
);

// A worktree of the repository, i.e. a folder with a checkout, as 'git worktree list' reports it.
// The main worktree is the repository's own folder; the others are linked to it and share its
// refs and objects, while each has its own HEAD, index and uncommitted changes.
public record Worktree(
    string Path,
    string HeadId,
    string Branch, // Empty when detached or bare
    bool IsMain,
    bool IsBare,
    bool IsDetached,
    bool IsLocked, // Locked against removal and pruning, e.g. by a tool while it is using it
    string LockReason,
    bool IsPrunable, // The folder is gone, so 'git worktree prune' would forget it
    string PruneReason
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
