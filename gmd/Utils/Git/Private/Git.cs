using IOPath = System.IO.Path;

namespace gmd.Utils.Git.Private;

internal class Git : IGit
{
    readonly ILogService logService;
    readonly IBranchService branchService;
    readonly IStatusService statusService;
    readonly ICommitService commitService;
    readonly IDiffService diffService;
    readonly IRemoteService remoteService;

    private string rootPath = "";
    private ICmd cmd;

    public string Path => rootPath;

    public Git(string path)
    {
        rootPath = WorkingTreeRoot(path).Or("");
        cmd = new Cmd(rootPath);

        logService = new LogService(cmd);
        branchService = new BranchService(cmd);
        statusService = new StatusService(cmd);
        commitService = new CommitService(cmd);
        diffService = new DiffService(cmd, statusService);
        remoteService = new RemoteService(cmd);
    }

    public Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount = 30000) =>
        logService.GetLogAsync(maxCount);

    public Task<R<IReadOnlyList<Branch>>> GetBranchesAsync() => branchService.GetBranchesAsync();
    public Task<R<Status>> GetStatusAsync() => statusService.GetStatusAsync();
    public Task<R> CommitAllChangesAsync(string message) => commitService.CommitAllChangesAsync(message);
    public Task<R<CommitDiff>> GetCommitDiffAsync(string commitId) => diffService.GetCommitDiffAsync(commitId);
    public Task<R<CommitDiff>> GetUncommittedDiff() => diffService.GetUncommittedDiff();
    public Task<R> FetchAsync() => remoteService.FetchAsync();
    public Task<R> PushBranchAsync(string name) => remoteService.PushBranchAsync(name);
    public Task<R> PushRefForceAsync(string name) => remoteService.PushRefForceAsync(name);
    public Task<R> PullRefAsync(string name) => remoteService.PullRefAsync(name);
    public Task<R> DeleteRemoteBranchAsync(string name) => remoteService.DeleteRemoteBranchAsync(name);
    public Task<R> PullCurrentBranchAsync() => remoteService.PullCurrentBranchAsync();
    public Task<R> PullBranchAsync(string name) => remoteService.PullBranchAsync(name);
    public Task<R> CloneAsync(string uri, string path) => remoteService.CloneAsync(uri, path);
    public Task<R> CheckoutAsync(string name) => branchService.CheckoutAsync(name);
    public Task<R> MergeBranch(string name) => branchService.MergeBranch(name);

    public static R<string> WorkingTreeRoot(string path)
    {
        if (path == "")
        {
            path = Directory.GetCurrentDirectory();
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

        return R.NoValue;
    }
}