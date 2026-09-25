using gmd.Cui.Common;
using gmd.Git;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IWebCommands
{
    void OpenRepo();
    void OpenBranch(string name);
    void OpenCommit(string commitId);
    void OpenNewPullRequest(string name);
}

// Opening the repository, a branch or a commit on the web site of the service hosting the remote,
// and the page for opening a pull request of a branch, for what a terminal cannot show: the review,
// the discussion and the checks. The links are made from the remote's URL (WebLinks); where there
// is no browser to open them in, e.g. over ssh, the link is copied instead, for opening it on the
// machine the user is sitting at.
//
// Whether an item can act is decided by the static methods, for the menus and the tests, which
// need no view.
class WebCommands : IWebCommands
{
    readonly IViewRepo repo;
    readonly IServer server;
    readonly IBrowserService browser;
    readonly IClipboardService clipboard;
    readonly IProgress progress;
    readonly IStatusLine status;

    public WebCommands(
        IViewRepo repo,
        IServer server,
        IBrowserService browser,
        IClipboardService clipboard,
        IProgress progress,
        IStatusLine status
    )
    {
        this.repo = repo;
        this.server = server;
        this.browser = browser;
        this.clipboard = clipboard;
        this.progress = progress;
        this.status = status;
    }

    public void OpenRepo() => Open("the repository", links => links.RepoUrl);

    public void OpenBranch(string name) => Open("a branch", links => links.Branch(RemoteName(repo.Repo, name)));

    public void OpenCommit(string commitId) => Open("a commit", links => links.Commit(commitId));

    public void OpenNewPullRequest(string name) =>
        Open(
            "a pull request",
            links => links.NewPullRequest(RemoteName(repo.Repo, name), PullRequestBase(repo.Repo, name))
        );

    // The branch's name on the remote, i.e. its remote branch without 'origin/', empty when it has
    // none there, which is what a link to it needs
    internal static string RemoteName(Repo repo, string name)
    {
        var branch = repo.BranchByName[name];
        var primary = repo.BranchByName[branch.PrimaryName];
        return primary.IsRemote ? primary.Name.TrimPrefix("origin/") : "";
    }

    // What a pull request of a branch goes into: the branch it was made from, which gmd knows and
    // the service does not, so that a feature made from 'dev' is not offered to 'main' by default.
    // The main branch when that one is not on the remote. The parent is the primary branch's, since
    // a local branch's parent is its own remote branch.
    internal static string PullRequestBase(Repo repo, string name)
    {
        var primary = repo.BranchByName[repo.BranchByName[name].PrimaryName];
        var parentName = primary.ParentBranchName;
        if (parentName != "" && repo.BranchByName.ContainsKey(parentName) && RemoteName(repo, parentName) != "")
            return RemoteName(repo, parentName);

        var main = repo.AllBranches.First(b => b.IsMainBranch);
        return RemoteName(repo, main.Name) is var mainName && mainName != "" ? mainName : main.NiceName;
    }

    internal static bool CanOpenBranch(Repo repo, string name) => RemoteName(repo, name) != "";

    internal static string WhyNoOpenBranch(Repo repo, string name)
    {
        var branch = repo.BranchByName[name];
        return !HasRemote(repo) ? NoRemote
            : !branch.IsGitBranch ? Why.Deleted(branch)
            : $"'{branch.NiceNameUnique}' is not on the remote: push it with p first";
    }

    internal static bool CanOpenNewPullRequest(Repo repo, string name) =>
        CanOpenBranch(repo, name) && RemoteName(repo, name) != PullRequestBase(repo, name);

    internal static string WhyNoNewPullRequest(Repo repo, string name) =>
        !CanOpenBranch(repo, name) ? WhyNoOpenBranch(repo, name)
        : repo.BranchByName[name].IsMainBranch ? "The main branch is what pull requests go into"
        : "The branch has no other branch on the remote to go into";

    // A commit gmd knows is not pushed would open a page that is not there
    internal static bool CanOpenCommit(Repo repo, Server.Commit commit) =>
        HasRemote(repo) && !commit.IsUncommitted && !commit.IsAhead;

    internal static string WhyNoOpenCommit(Repo repo, Server.Commit commit) =>
        !HasRemote(repo) ? NoRemote
        : commit.IsUncommitted ? "The uncommitted changes are not on the remote: move to a commit first"
        : "The commit is not pushed yet: push it with p first";

    const string NoRemote = "The repository has no remote branches";

    static bool HasRemote(Repo repo) => repo.AllBranches.Any(b => b.IsRemote);

    void Open(string what, Func<WebLinks, string?> linkOf) =>
        Do(async () =>
        {
            var remote = await server.GetRemoteUrlAsync(repo.Path);
            if (remote is not string remoteUrl)
                return new Error("Failed to read the remote", remote.Error);
            if (remoteUrl == "")
                return new Notice("The repository has no remote 'origin'");
            if (WebLinks.From(remoteUrl) is not WebLinks links)
                return new Notice($"The remote is {remoteUrl}, which has no web page");
            if (linkOf(links) is not string url)
                return new Notice($"gmd knows no web page for {what} on {links.HostName}, only for the repository");

            return await OpenOrCopyAsync(url);
        });

    async Task<Result> OpenOrCopyAsync(string url)
    {
        if (await browser.OpenAsync(url) is not Error e)
        {
            status.Info($"Opened {url}");
            return Result.Ok;
        }

        if (clipboard.Set(url) is Error copyError)
            return new Error($"{e.Message}, and the link could not be copied either:\n{url}", copyError);

        status.Notice($"{e.Message}: copied the link to the clipboard");
        return Result.Ok;
    }

    void Do(Func<Task<Result>> action) => CommandRunner.Do(progress, status, repo, action);
}
