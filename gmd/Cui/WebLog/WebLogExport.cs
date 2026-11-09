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
        repo = HideSyncedLocalBranches(repo);

        var pullRequestsResult = ReadPullRequests(pullRequestsPath);
        if (pullRequestsResult is not IReadOnlyList<PullRequest> pullRequests)
            return pullRequestsResult.Error;

        var links = await server.GetRemoteUrlAsync(repo.Path) is string url ? WebLinks.From(url) : null;
        // The repository's own name, e.g. 'gmd' of 'owner/gmd'
        var name = links != null ? new Uri(links.RepoUrl).AbsolutePath.Trim('/').Split('/')[^1] : "";
        var title = $"{(name != "" ? name : Path.GetFileName(repo.Path))} Branch Graph";

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

    // The repo without the local branches that are at their remote branch's commit: the page is of
    // what is on the remote, and the checkout it is made from has a local branch or two only since
    // git needs one checked out, each drawn as a tip of its own beside its remote branch's and
    // taking a column. The server always shows a branch with its local one, so they are taken out
    // of the view repo here, and the remote branch is left as one with no local branch.
    internal static Server.Repo HideSyncedLocalBranches(Server.Repo repo)
    {
        var shown = repo.ViewBranches.ToDictionary(b => b.Name);
        var hidden = repo
            .ViewBranches.Where(b =>
                !b.IsRemote
                && shown.TryGetValue(b.RemoteName, out var remote)
                && remote.TipId == b.TipId
                && !repo.ViewCommits.Any(c => c.BranchName == b.Name)
            )
            .Select(b => b.Name)
            .ToHashSet();
        if (hidden.Count == 0)
            return repo;

        return repo with
        {
            ViewBranches = repo
                .ViewBranches.Where(b => !hidden.Contains(b.Name))
                .Select(b => hidden.Contains(b.LocalName) ? b with { LocalName = "" } : b)
                .ToList(),
        };
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
