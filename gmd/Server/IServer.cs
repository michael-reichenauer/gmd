namespace gmd.Server;

enum ShowBranches
{
    Specified,
    AllRecent,
    AllActive,
    AllActiveAndDeleted,
}

interface IServer
{
    event Action<ChangeEvent> RepoChange;
    event Action<ChangeEvent> StatusChange;

    Task<Result<Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches);
    Task<Result<Repo>> GetUpdateStatusRepoAsync(Repo repo);
    Task<Result<Repo>> GetFilteredRepoAsync(Repo repo, string filter, int maxCount);

    // The branches of the commits next to a commit, and those starting at it, but its own; with
    // isAll false only those not shown
    IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId, bool isAll);
    IReadOnlyList<string> GetPossibleBranchNames(Repo repo, string commitId, int maxCount);

    Repo ShowBranch(
        Repo repo,
        string branchName,
        bool includeAmbiguous,
        ShowBranches show = ShowBranches.Specified,
        int count = 1
    );
    Repo HideBranch(Repo repo, string name, bool hideAllBranches = false);

    // Shows exactly these branches, with their ancestors, as the names of a view repo's own
    // ViewBranches give it back, which is how a show or hide is undone
    Repo SetShownBranches(Repo repo, IReadOnlyList<string> branchNames);
    Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName);
    Task<Result> UnresolveAmbiguityAsync(Repo repo, string commitId);
    Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName);
    Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout);
    Task<Result> CreateBranchFromBranchAsync(Repo repo, string newBranchName, string sourceBranch, bool isCheckout);
    Task<Result> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout);
    Task<Result> RenameBranchAsync(string oldName, string newName, string wd);
    Task<Result> StashAsync(string message, string wd);
    Task<Result> StashPopAsync(string name, string wd);
    Task<Result> StashApplyAsync(string name, string wd);

    // Git commands
    Task<Result<IReadOnlyList<string>>> GetFilePathsAsync(string reference, string wd);
    Task<Result> FetchAsync(string wd);
    Task<Result<string>> GetRemoteUrlAsync(string wd);
    Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<Result> CommitFilesAsync(string message, bool isAmend, IReadOnlyList<string> paths, string wd);

    // Commits the paths with the lines left out of them, by file, left in the working tree, uncommitted
    Task<Result> CommitLinesAsync(
        string message,
        bool isAmend,
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, IReadOnlyList<ChosenLine>> leftOut,
        string wd
    );
    Task<Result<CommitDiff>> GetCommitDiffAsync(string commitId, Git.DiffOptions options, string wd);

    // The files the commit changed, as its diff has them, with no diff read, for the details pane
    Task<Result<IReadOnlyList<ChangedFile>>> GetChangedFilesAsync(string commitId, string wd);
    Task<Result<CommitDiff[]>> GetFileDiffAsync(string path, Git.DiffOptions options, string wd);
    Task<Result<CommitDiff[]>> GetLineHistoryAsync(
        string path,
        int firstLine,
        int lastLine,
        string reference,
        Git.DiffOptions options,
        string wd
    );
    Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd);
    Task<Result<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        Git.DiffOptions options,
        string wd
    );
    Task<Result<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        Git.DiffOptions options,
        string wd
    );
    Task<Result> RunDiffToolAsync(string path, string wd);
    Task<Result> RunMergeToolAsync(string path, string wd);

    Task<Result> PushBranchAsync(string name, string wd);

    // Force pushes a local branch over its remote branch, if origin still has that where the repo,
    // i.e. the screen the user saw, has it
    Task<Result> ForcePushBranchAsync(Repo repo, string name);
    Task<Result> PullCurrentBranchAsync(string wd);
    Task<Result<bool>> IsPullWayConfiguredAsync(string branchName, string wd);
    Task<Result> SetPullRebaseAsync(bool isRebase, string wd);
    Task<Result> PullBranchAsync(string name, string wd);
    Task<Result> SwitchToAsync(Repo repo, string branchName);
    Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName);
    Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName);
    Task<Result> RebaseBranchAsync(Repo repo, string branchName);
    Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd);
    Task<Result> CherryPickAsync(string sha, string wd);
    Task<Result> AbortOperationAsync(string wd);
    Task<Result> ContinueOperationAsync(string wd);
    Task<Result> SkipOperationAsync(string wd);
    Task<Result<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd);
    Task<Result<ConflictState>> GetConflictStateAsync(string wd);
    Task<Result<ConflictFile>> GetConflictFileAsync(string path, ConflictKind kind, bool isWithBase, string wd);
    Task<Result<ConflictSides>> GetConflictSidesAsync(string wd);
    Task<Result> ResolveConflictFileAsync(
        string path,
        ConflictKind kind,
        IReadOnlyList<HunkResolution> choices,
        string wd
    );
    Task<Result> UnresolveAsync(string path, string wd);
    Task<Result> UseWholeFileAsync(string path, bool isOurs, string wd);
    Task<Result> KeepConflictedFileAsync(string path, string wd);
    Task<Result> DeleteConflictedAsync(string path, string wd);

    // Deletes a branch on origin, here or both (a name is "" for a side not to delete), and records it
    // for Restore Deleted Branch, since git deletes a branch's reflog with it
    Task<Result> DeleteBranchAsync(Repo repo, string localName, string remoteName, bool isForce);
    Task<Result> DeleteRemoteBranchAsync(string name, string wd);
    Task<Result<Repo>> GetUpdatedWorktreesRepoAsync(Repo repo);

    // Which branches deleted on origin were squash merged or rebase merged there (Repo.MergedOnOrigin):
    // whether a branch is not known yet, and the repo with all known, a git command for each not known
    bool HasUnknownMergedOnOrigin(Repo repo);
    Task<Result<Repo>> GetMergedOnOriginRepoAsync(Repo repo);
    Task<Result> AddWorktreeAsync(string path, string branchName, bool isNewBranch, string startPoint, string wd);
    Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd);
    Task<Result> PruneWorktreesAsync(string wd);
    Task<Result<IReadOnlyList<string>>> GetIgnoredPathsAsync(IReadOnlyList<string> paths, string wd);
    Task<Result> UndoAllUncommittedChangesAsync(string wd);
    Task<Result> UndoUncommittedFileAsync(string path, string wd);

    // Takes the chosen lines of a file's uncommitted changes back in the working tree, the rest of its
    // changes left as they are; refused when the file has changed since the lines were shown
    Task<Result> DiscardLinesAsync(string path, IReadOnlyList<ChosenLine> lines, string wd);
    Task<Result> CleanWorkingFolderAsync(string wd);
    Task<Result<IReadOnlyList<string>>> GetFilesToCleanAsync(string wd);
    Task<Result> UndoCommitAsync(string id, int parent, string wd);
    Task<Result> UncommitLastCommitAsync(string wd);
    Task<Result> UncommitUntilCommitAsync(string id, string wd);

    // Takes back the last change of a branch (Repo.UndoSteps), or a redo, see UndoService
    Task<Result> UndoStepAsync(Repo repo, UndoStep step);

    // The lines of work no branch, tag or stash has any more, which the reflogs still mention
    Task<Result<IReadOnlyList<LostWork>>> GetLostWorkAsync(Repo repo);

    // The branches gmd deleted that can be brought back, newest first, and bringing one back
    Task<Result<IReadOnlyList<DeletedBranch>>> GetDeletedBranchesAsync(Repo repo);
    Task<Result> RestoreBranchAsync(Repo repo, DeletedBranch deleted, bool isLocal, bool isRemote);

    // Pulls a branch whose remote branch a force push rewrote (Repo.RemoteRewrites): its own commits
    // are moved onto the new version, rather than the two versions merged
    Task<Result> PullRewrittenAsync(Repo repo, RemoteRewrite rewrite);

    // Force pushes a rewritten remote branch back as it was before the rewrite, if no one pushed since
    Task<Result> RestoreOriginAsync(RemoteRewrite rewrite, string wd);
    Task<Result> CloneAsync(string uri, string path, string wd);
    Task<Result> InitRepoAsync(string path, string wd);
    Task<Result<CommitDiff>> GetStashDiffAsync(string name, Git.DiffOptions options, string wd);
    Task<Result> StashDropAsync(string name, string wd);

    // What the discards threw away, and the stashes dropped, newest first, each to be put back
    // (DiscardedChanges): its diff, and restoring it
    Task<Result<IReadOnlyList<DiscardedChange>>> GetDiscardedAsync(string wd);
    Task<Result<CommitDiff>> GetDiscardedDiffAsync(DiscardedChange change, Git.DiffOptions options, string wd);
    Task<Result> RestoreDiscardedAsync(DiscardedChange change, string wd);
    Task<Result<string>> GetChangeLogAsync(string? newRelease = null);
    Task<Result> AddTagAsync(string name, string commitId, string wd);
    Task<Result> AddAnnotatedTagAsync(string name, string message, string commitId, string wd);
    Task<Result> RemoveTagAsync(string name, bool isOnOrigin, string wd);
    Task<Result<IReadOnlyList<RepoTag>>> GetTagsAsync(string wd);
    Task<Result> PushTagAsync(string name, string wd);
    Task<Result> SwitchToCommitAsync(string commitId, string wd);
    Task<Result> SquashCommitsAsync(Repo repo, string id1, string id2, string msg);

    // An older commit not pushed yet, given a new message and the changes in the paths, none for a
    // new message alone, or taken out of the branch
    Task<Result> AmendOlderCommitAsync(Repo repo, string id, string message, IReadOnlyList<string> paths);
    Task<Result> DropCommitAsync(Repo repo, string id);
}

// A change the file monitor saw, with when it was told of the last of the changes it reports,
// which the debounce is timed by.
internal record ChangeEvent(DateTime TimeStamp)
{
    // A read of the repo that started after the change was told of saw it, since a change is told
    // of after it is made. One told of later may have been seen or not, so it is read again, which
    // costs a read now and then but never loses a change. The file's modification time cannot tell
    // the two apart instead: a rename, a move and a copy that keeps the time all leave one older
    // than the change, and a file system with a coarse clock rounds it down to before the read.
    public bool IsSeenBy(Repo repo) => TimeStamp < repo.RepoTimeStamp;
}
