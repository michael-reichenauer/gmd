using System.Text.Json;
using gmd.Cui.Common;
using gmd.Cui.Graphs;
using gmd.Git;
using gmd.Server;

namespace gmd.Cui.WebLog;

// Writes the web log page of a repo, see WebLogPage, for the command line's --web-log, which a
// GitHub Action runs to publish it. It shows every active branch, as Show All Active does in the
// log view, read as gmd reads the repo, with no terminal.
interface IWebLogExport
{
    Task<Result> WriteAsync(
        string repoPath,
        string outPath,
        string pullRequestsPath,
        int maxRows,
        IReadOnlyList<string> hiddenBranches
    );
}

class WebLogExport : IWebLogExport
{
    readonly IServer server;
    readonly IGraphCreater graphCreater;
    readonly IGraphWriter graphWriter;
    readonly IBranchColorService branchColorService;

    internal WebLogExport(
        IServer server,
        IGraphCreater graphCreater,
        IGraphWriter graphWriter,
        IBranchColorService branchColorService
    )
    {
        this.server = server;
        this.graphCreater = graphCreater;
        this.graphWriter = graphWriter;
        this.branchColorService = branchColorService;
    }

    public async Task<Result> WriteAsync(
        string repoPath,
        string outPath,
        string pullRequestsPath,
        int maxRows,
        IReadOnlyList<string> hiddenBranches
    )
    {
        var readResult = await server.GetRepoAsync(repoPath, []);
        if (readResult is not Server.Repo read)
            return new Error($"Failed to read the repo in {repoPath}", readResult.Error);
        var repo = server.ShowBranch(read, "", false, ShowBranches.AllActive);
        repo = HideBranches(repo, hiddenBranches);

        var pullRequestsResult = ReadPullRequests(pullRequestsPath);
        if (pullRequestsResult is not IReadOnlyList<PullRequest> pullRequests)
            return pullRequestsResult.Error;

        var links = await server.GetRemoteUrlAsync(repo.Path) is string url ? WebLinks.From(url) : null;
        var title = links != null ? new Uri(links.RepoUrl).AbsolutePath.Trim('/') : Path.GetFileName(repo.Path);

        var graph = graphCreater.Create(repo);
        var html = WebLogPage.Html(
            repo,
            graph,
            graphWriter,
            b => branchColorService.GetColor(repo, b),
            links,
            pullRequests,
            title,
            DateTime.UtcNow,
            maxRows
        );

        return Result.Catch(() =>
        {
            if (Path.GetDirectoryName(Path.GetFullPath(outPath)) is string folder)
                Directory.CreateDirectory(folder);
            File.WriteAllText(outPath, html);
        });
    }

    // The repo with these branches hidden, named as on the remote or locally, e.g. a branch of data
    // that is no work of anyone's; a name of no branch shown is passed over
    Server.Repo HideBranches(Server.Repo repo, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            var branch = repo.ViewBranches.FirstOrDefault(b =>
                b.Name == name || (b.IsRemote && b.Name[(b.Name.IndexOf('/') + 1)..] == name)
            );
            if (branch != null && !branch.IsMainBranch)
                repo = server.HideBranch(repo, branch.Name);
        }
        return repo;
    }

    // The open pull requests, as 'gh pr list --json number,title,headRefName,url,isDraft,isCrossRepository' writes
    // them, none when no file is named
    internal static Result<IReadOnlyList<PullRequest>> ReadPullRequests(string path)
    {
        if (path == "")
            return Array.Empty<PullRequest>();
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PullRequest>>(File.ReadAllText(path), options) ?? [];
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Failed to read the pull requests in {path}", e);
        }
    }
}
