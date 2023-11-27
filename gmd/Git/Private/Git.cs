using IOPath = System.IO.Path;

namespace gmd.Git.Private;

internal class Git : IGit
{
    readonly ILogService logService;
    readonly IBranchService branchService;
    readonly IStatusService statusService;
    readonly ICommitService commitService;
    readonly IDiffService diffService;
    readonly IRemoteService remoteService;
    readonly IRepoService repoService;
    readonly ITagService tagService;
    readonly IKeyValueService keyValueService;
    readonly IStashService stashService;
    readonly ICmd cmd;

    public Git(
        ILogService logService,
        IBranchService branchService,
        IStatusService statusService,
        ICommitService commitService,
        IDiffService diffService,
        IRemoteService remoteService,
        IRepoService repoService,
        ITagService tagService,
        IKeyValueService keyValueService,
        IStashService stashService,
        ICmd cmd)
    {
        this.logService = logService;
        this.branchService = branchService;
        this.statusService = statusService;
        this.commitService = commitService;
        this.diffService = diffService;
        this.remoteService = remoteService;
        this.repoService = repoService;
        this.tagService = tagService;
        this.keyValueService = keyValueService;
        this.stashService = stashService;
        this.cmd = cmd;
    }

    public R<string> RootPath(string path) => RootPathDir(path);

    public Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd) =>
        logService.GetLogAsync(maxCount, wd);
    public Task<R<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd) =>
        logService.GetMergeLogAsync(reference, wd);
    public Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) =>
        logService.GetFileAsync(reference, wd);
    public Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd) =>
        branchService.GetBranchesAsync(wd);
    public Task<R<Status>> GetStatusAsync(string wd) => statusService.GetStatusAsync(wd);
    public Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
        commitService.CommitAllChangesAsync(message, isAmend, wd);
    public Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd) =>
        diffService.GetCommitDiffAsync(commitId, wd);
    public Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd) =>
        diffService.GetFileDiffAsync(path, wd);
    public Task<R<CommitDiff>> GetPreviewMergeDiffAsync(string sha1, string sha2, string message, string wd) =>
        diffService.GetRefsDiffAsync(sha1, sha2, message, wd);
    public Task<R<CommitDiff>> GetUncommittedDiff(string wd) => diffService.GetUncommittedDiff(wd);
    public Task<R> FetchAsync(string wd) => remoteService.FetchAsync(wd);
    public Task<R> PushBranchAsync(string name, string wd) => remoteService.PushBranchAsync(name, wd);
    public Task<R> PushCurrentBranchAsync(bool isForce, string wd) => remoteService.PushCurrentBranchAsync(isForce, wd);
    public Task<R> PushRefForceAsync(string name, string wd) => remoteService.PushRefForceAsync(name, wd);
    public Task<R> PullRefAsync(string name, string wd) => remoteService.PullRefAsync(name, wd);
    public Task<R> PullCurrentBranchAsync(string wd) => remoteService.PullCurrentBranchAsync(wd);
    public Task<R> PullBranchAsync(string name, string wd) => remoteService.PullBranchAsync(name, wd);
    public Task<R> CloneAsync(string uri, string path, string wd) =>
        remoteService.CloneAsync(uri, path, wd);
    public Task<R> InitRepoAsync(string path, string wd) =>
        repoService.InitAsync(path, false);
    public Task<R> CheckoutAsync(string name, string wd) => branchService.CheckoutAsync(name, wd);
    public Task<R> MergeBranchAsync(string name, string wd) => branchService.MergeBranchAsync(name, wd);
    public Task<R> RebaseBranchAsync(string name, string wd) => branchService.RebaseBranchAsync(name, wd);
    public Task<R> CherryPickAsync(string sha, string wd) => branchService.CherryPickAsync(sha, wd);
    public Task<R> CreateBranchAsync(string name, bool isCheckout, string wd) =>
        branchService.CreateBranchAsync(name, isCheckout, wd);
    public Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd) =>
        branchService.CreateBranchFromCommitAsync(name, sha, isCheckout, wd);
    public Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        branchService.DeleteLocalBranchAsync(name, isForced, wd);
    public Task<R> DeleteRemoteBranchAsync(string name, string wd) =>
        remoteService.DeleteRemoteBranchAsync(name, wd);
    public Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd) => tagService.GetTagsAsync(wd);
    public Task<R> UndoAllUncommittedChangesAsync(string wd) =>
        commitService.UndoAllUncommittedChangesAsync(wd);
    public Task<R> UndoUncommittedFileAsync(string path, string wd) =>
        commitService.UndoUncommittedFileAsync(path, wd);
    public Task<R> CleanWorkingFolderAsync(string wd) => commitService.CleanWorkingFolderAsync(wd);
    public Task<R> UndoCommitAsync(string id, int parentIndex, string wd) => commitService.UndoCommitAsync(id, parentIndex, wd);
    public Task<R> UncommitLastCommitAsync(string wd) => commitService.UncommitLastCommitAsync(wd);
    public Task<R> UncommitUntilCommitAsync(string id, string wd) => commitService.UncommitUntilCommitAsync(id, wd);
    public Task<R<string>> GetValueAsync(string key, string wd) =>
       keyValueService.GetValueAsync(key, wd);
    public Task<R> SetValueAsync(string key, string value, string wd) =>
        keyValueService.SetValueAsync(key, value, wd);
    public Task<R> PushValueAsync(string key, string wd) =>
        keyValueService.PushValueAsync(key, wd);
    public Task<R> PullValueAsync(string key, string wd) =>
        keyValueService.PullValueAsync(key, wd);
    public Task<R> StashAsync(string wd) => stashService.StashAsync(wd);
    public Task<R> StashPopAsync(string name, string wd) => stashService.PopAsync(name, wd);
    public Task<R> StashDropAsync(string name, string wd) => stashService.DropAsync(name, wd);
    public Task<R<IReadOnlyList<Stash>>> GetStashesAsync(string wd) => stashService.ListAsync(wd);
    public Task<R<CommitDiff>> GetStashDiffAsync(string name, string wd) =>
        diffService.GetStashDiffAsync(name, wd);
    public Task<R> AddTagAsync(string name, string commitId, string wd) =>
        tagService.AddTagAsync(name, commitId, wd);
    public Task<R> RemoveTagAsync(string name, string wd) =>
        tagService.RemoveTagAsync(name, wd);

    public Task<R> PushTagAsync(string name, string wd) =>
        remoteService.PushTagAsync(name, wd);
    public Task<R> DeleteRemoteTagAsync(string name, string wd) =>
        remoteService.DeleteRemoteTagAsync(name, wd);

    public async Task<R<string>> Version()
    {
        if (!Try(out var output, out var e, await cmd.RunAsync("git", "version", "", true, true))) return e;
        return output.TrimPrefix("git version ");
    }

    public static R<string> RootPathDir(string path)
    {
        if (path == "")
        {
            path = Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(path))
        {
            return R.Error($"Folder does not exist: '{path}'");
        }

        var current = path.TrimSuffix("/").TrimSuffix("\\");
        if (path.EndsWith(".git"))
        {
            current = IOPath.GetDirectoryName(path) ?? path;
        }

        while (true)
        {
            string gitRepoPath = IOPath.Join(current, ".git");
            if (Directory.Exists(gitRepoPath))
            {
                return current;
            }
            string parent = IOPath.GetDirectoryName(current) ?? current;
            if (parent == current)
            {
                // Reached top/root volume folder
                break;
            }
            current = parent;
        }

        return R.Error($"No '.git' folder was found in:\n'{path}'\n or in any parent folders.");
    }
}