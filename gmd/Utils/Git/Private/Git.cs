using System.IO;
using gmd.Utils;

namespace gmd.Utils.Git.Private;

internal class Git : IGit
{
    private ILogService logService;
    private IBranchService branchService;
    private IStatusService statusService;

    private string rootPath = "";
    private ICmd cmd;

    public Git(string path)
    {
        rootPath = WorkingTreeRoot(path).Or("");
        cmd = new Cmd(rootPath);

        logService = new LogService(cmd);
        branchService = new BranchService(cmd);
        statusService = new StatusService(cmd);
    }

    public Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount = 30000) =>
        logService.GetLogAsync(maxCount);

    public Task<R<IReadOnlyList<Branch>>> GetBranchesAsync() => branchService.GetBranchesAsync();
    public Task<R<Status>> GetStatusAsync() => statusService.GetStatusAsync();

    public static R<string> WorkingTreeRoot(string path)
    {
        if (path == "")
        {
            path = Directory.GetCurrentDirectory();
        }

        var current = path;
        if (path.EndsWith(".git") || path.EndsWith(".git/") || path.EndsWith(".git\\"))
        {
            current = Path.GetDirectoryName(path) ?? path;
        }

        while (true)
        {
            string gitRepoPath = Path.Join(current, ".git");
            if (Directory.Exists(gitRepoPath))
            {
                return current;
            }
            string parent = Path.GetDirectoryName(current) ?? current;
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