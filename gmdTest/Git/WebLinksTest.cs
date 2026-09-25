using gmd.Git;

namespace gmdTest.Git;

// The forms a remote URL takes, and the web pages each hosting service has below the repository
[TestClass]
public class WebLinksTest
{
    [TestMethod]
    [DataRow("https://github.com/user/repo.git", "https://github.com/user/repo")]
    [DataRow("https://github.com/user/repo", "https://github.com/user/repo")]
    [DataRow("https://github.com/user/repo/", "https://github.com/user/repo")]
    [DataRow("git@github.com:user/repo.git", "https://github.com/user/repo")]
    [DataRow("github.com:user/repo.git", "https://github.com/user/repo")]
    [DataRow("ssh://git@github.com/user/repo.git", "https://github.com/user/repo")]
    [DataRow("ssh://git@gitlab.example.com:2222/group/sub/repo.git", "https://gitlab.example.com/group/sub/repo")]
    [DataRow("https://user:token@gitlab.com/group/repo.git", "https://gitlab.com/group/repo")]
    [DataRow("http://git.example.com:8080/team/repo.git", "http://git.example.com:8080/team/repo")]
    [DataRow("git://git.example.com/team/repo.git", "https://git.example.com/team/repo")]
    [DataRow("https://org@dev.azure.com/org/project/_git/repo", "https://dev.azure.com/org/project/_git/repo")]
    [DataRow("git@ssh.dev.azure.com:v3/org/project/repo", "https://dev.azure.com/org/project/_git/repo")]
    [DataRow("org@vs-ssh.visualstudio.com:v3/org/project/repo", "https://org.visualstudio.com/project/_git/repo")]
    [DataRow("https://org.visualstudio.com/project/_git/repo", "https://org.visualstudio.com/project/_git/repo")]
    public void TestTheRepositoryPageOfARemote(string remote, string page)
    {
        Assert.AreEqual(page, WebLinks.From(remote)?.RepoUrl);
    }

    // A remote in a folder next door, which is what the tests and some setups use, has no web page
    [TestMethod]
    [DataRow("/home/user/repos/origin.git")]
    [DataRow("../origin.git")]
    [DataRow("file:///home/user/repos/origin.git")]
    [DataRow(@"C:\repos\origin.git")]
    [DataRow("")]
    public void TestALocalRemoteHasNoWebPage(string remote)
    {
        Assert.IsNull(WebLinks.From(remote));
    }

    [TestMethod]
    [DataRow("git@github.com:user/repo.git", "GitHub")]
    [DataRow("git@github.example.com:user/repo.git", "GitHub")] // GitHub Enterprise
    [DataRow("git@gitlab.com:user/repo.git", "GitLab")]
    [DataRow("git@bitbucket.org:user/repo.git", "Bitbucket")]
    [DataRow("git@ssh.dev.azure.com:v3/org/project/repo", "AzureDevOps")]
    [DataRow("git@codeberg.org:user/repo.git", "Gitea")]
    [DataRow("git@git.example.com:user/repo.git", "Unknown")]
    public void TestTheHostIsKnownByName(string remote, string host)
    {
        Assert.AreEqual(host, WebLinks.From(remote)?.Host.ToString());
    }

    [TestMethod]
    public void TestGitHub()
    {
        var links = WebLinks.From("git@github.com:user/repo.git")!;

        Assert.AreEqual("https://github.com/user/repo/tree/feature/login", links.Branch("feature/login"));
        Assert.AreEqual("https://github.com/user/repo/commit/abc123", links.Commit("abc123"));
        Assert.AreEqual(
            "https://github.com/user/repo/compare/dev...feature/login?expand=1",
            links.NewPullRequest("feature/login", "dev")
        );
    }

    [TestMethod]
    public void TestGitLab()
    {
        var links = WebLinks.From("git@gitlab.com:group/repo.git")!;

        Assert.AreEqual("https://gitlab.com/group/repo/-/tree/feature/login", links.Branch("feature/login"));
        Assert.AreEqual("https://gitlab.com/group/repo/-/commit/abc123", links.Commit("abc123"));
        Assert.AreEqual(
            "https://gitlab.com/group/repo/-/merge_requests/new?merge_request%5Bsource_branch%5D=feature%2Flogin"
                + "&merge_request%5Btarget_branch%5D=dev",
            links.NewPullRequest("feature/login", "dev")
        );
    }

    [TestMethod]
    public void TestBitbucket()
    {
        var links = WebLinks.From("git@bitbucket.org:team/repo.git")!;

        Assert.AreEqual("https://bitbucket.org/team/repo/branch/feature/login", links.Branch("feature/login"));
        Assert.AreEqual("https://bitbucket.org/team/repo/commits/abc123", links.Commit("abc123"));
        Assert.AreEqual(
            "https://bitbucket.org/team/repo/pull-requests/new?source=feature%2Flogin&dest=dev",
            links.NewPullRequest("feature/login", "dev")
        );
    }

    [TestMethod]
    public void TestAzureDevOps()
    {
        var links = WebLinks.From("git@ssh.dev.azure.com:v3/org/project/repo")!;

        Assert.AreEqual(
            "https://dev.azure.com/org/project/_git/repo?version=GBfeature%2Flogin",
            links.Branch("feature/login")
        );
        Assert.AreEqual("https://dev.azure.com/org/project/_git/repo/commit/abc123", links.Commit("abc123"));
        Assert.AreEqual(
            "https://dev.azure.com/org/project/_git/repo/pullrequestcreate?sourceRef=feature%2Flogin&targetRef=dev",
            links.NewPullRequest("feature/login", "dev")
        );
    }

    [TestMethod]
    public void TestGitea()
    {
        var links = WebLinks.From("https://codeberg.org/user/repo.git")!;

        Assert.AreEqual("https://codeberg.org/user/repo/src/branch/feature/login", links.Branch("feature/login"));
        Assert.AreEqual("https://codeberg.org/user/repo/commit/abc123", links.Commit("abc123"));
        Assert.AreEqual(
            "https://codeberg.org/user/repo/compare/dev...feature/login",
            links.NewPullRequest("feature/login", "dev")
        );
    }

    // A self-hosted service with no telling name: its repository page, but nothing below it, since
    // a guess would open a page that is not there
    [TestMethod]
    public void TestAnUnknownHostHasOnlyTheRepositoryPage()
    {
        var links = WebLinks.From("git@git.example.com:team/repo.git")!;

        Assert.AreEqual("https://git.example.com/team/repo", links.RepoUrl);
        Assert.IsNull(links.Branch("dev"));
        Assert.IsNull(links.Commit("abc123"));
        Assert.IsNull(links.NewPullRequest("dev", "main"));
        Assert.AreEqual("git.example.com", links.HostName);
    }

    // Git allows '#', '%' and '&' in a branch name, which would otherwise end the path or the query
    [TestMethod]
    public void TestABranchNameIsEscaped()
    {
        var links = WebLinks.From("git@github.com:user/repo.git")!;

        Assert.AreEqual("https://github.com/user/repo/tree/fix/%23123%26more", links.Branch("fix/#123&more"));
    }
}
