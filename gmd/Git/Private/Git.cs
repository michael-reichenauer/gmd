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
    private readonly ICmd cmd;

    public Git(
        ILogService logService,
        IBranchService branchService,
        IStatusService statusService,
        ICommitService commitService,
        IDiffService diffService,
        IRemoteService remoteService,
        ICmd cmd)
    {
        this.logService = logService;
        this.branchService = branchService;
        this.statusService = statusService;
        this.commitService = commitService;
        this.diffService = diffService;
        this.remoteService = remoteService;
        this.cmd = cmd;
    }

    public R<string> RootPath(string path) => RootPathDir(path);

    public Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd) =>
        logService.GetLogAsync(maxCount, wd);

    public Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd) =>
        branchService.GetBranchesAsync(wd);
    public Task<R<Status>> GetStatusAsync(string wd) => statusService.GetStatusAsync(wd);
    public Task<R> CommitAllChangesAsync(string message, string wd) =>
        commitService.CommitAllChangesAsync(message, wd);
    public Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd) =>
        diffService.GetCommitDiffAsync(commitId, wd);
    public Task<R<CommitDiff>> GetUncommittedDiff(string wd) => diffService.GetUncommittedDiff(wd);
    public Task<R> FetchAsync(string wd) => remoteService.FetchAsync(wd);
    public Task<R> PushBranchAsync(string name, string wd) => remoteService.PushBranchAsync(name, wd);
    public Task<R> PushRefForceAsync(string name, string wd) => remoteService.PushRefForceAsync(name, wd);
    public Task<R> PullRefAsync(string name, string wd) => remoteService.PullRefAsync(name, wd);
    public Task<R> PullCurrentBranchAsync(string wd) => remoteService.PullCurrentBranchAsync(wd);
    public Task<R> PullBranchAsync(string name, string wd) => remoteService.PullBranchAsync(name, wd);
    public Task<R> CloneAsync(string uri, string path, string wd) =>
        remoteService.CloneAsync(uri, path, wd);
    public Task<R> CheckoutAsync(string name, string wd) => branchService.CheckoutAsync(name, wd);
    public Task<R> MergeBranch(string name, string wd) => branchService.MergeBranch(name, wd);
    public Task<R> CreateBranchAsync(string name, bool isCheckout, string wd) =>
        branchService.CreateBranchAsync(name, isCheckout, wd);
    public Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd) =>
        branchService.CreateBranchFromCommitAsync(name, sha, isCheckout, wd);
    public Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        branchService.DeleteLocalBranchAsync(name, isForced, wd);
    public Task<R> DeleteRemoteBranchAsync(string name, string wd) =>
        remoteService.DeleteRemoteBranchAsync(name, wd);


    public async Task<R<string>> Version()
    {
        if (!Try(out var output, out var e, await cmd.RunAsync("git", "version", "", true))) return e;
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

        var current = path;
        if (path.EndsWith(".git") || path.EndsWith(".git/") || path.EndsWith(".git\\"))
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