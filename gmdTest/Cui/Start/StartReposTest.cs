using gmd.Common;
using gmd.Cui.Start;

namespace gmdTest.Cui.Start;

// What the start page lists, on folders laid out by hand: a '.git' folder is all a repository needs
// to be listed, since listing starts no git process. 'home' is a folder of the temp root, so the
// paths read '~/...'.
[TestClass]
public class StartReposTest
{
    string root = "";

    [TestInitialize]
    public void Init()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-start-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    string Home => Path.Join(root, "home");

    string Repo(params string[] parts)
    {
        var path = Path.Join([Home, .. parts]);
        Directory.CreateDirectory(Path.Join(path, ".git"));
        return path;
    }

    string Folder(params string[] parts)
    {
        var path = Path.Join([Home, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    static string Tilde(params string[] parts) => Path.Join(["~", .. parts]);

    [TestMethod]
    public void TestRecentRepositoriesAreListedByNameAndShortPath()
    {
        var gmd = Repo("code", "gmd");
        var dotfiles = Repo("dotfiles");

        var repos = StartRepos.RecentOf([gmd, dotfiles], Home);

        CollectionAssert.AreEqual(new[] { "gmd", "dotfiles" }, repos.Select(r => r.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { Tilde("code", "gmd"), Tilde("dotfiles") },
            repos.Select(r => r.ShownPath).ToArray()
        );
        Assert.IsTrue(repos.All(r => r.IsRecent));
    }

    // A recent folder that is gone, or is there but no longer a repository, would only fail when
    // opened, and the same folder written twice is one repository
    [TestMethod]
    public void TestGoneAndNoLongerRepositoriesAndDuplicatesAreLeftOut()
    {
        var gmd = Repo("code", "gmd");
        var plain = Folder("code", "plain");
        var gone = Path.Join(Home, "code", "gone");

        var repos = StartRepos.RecentOf([gmd, plain, gone, gmd + Path.DirectorySeparatorChar], Home);

        CollectionAssert.AreEqual(new[] { gmd }, repos.Select(r => r.Path).ToArray());
    }

    // A linked worktree's '.git' is a file, and it is a repository to open like any other
    [TestMethod]
    public void TestALinkedWorktreeIsListed()
    {
        var worktree = Folder("code", "gmd-dev");
        File.WriteAllText(Path.Join(worktree, ".git"), "gitdir: /somewhere/.git/worktrees/gmd-dev\n");

        var repos = StartRepos.RecentOf([worktree], Home);

        CollectionAssert.AreEqual(new[] { "gmd-dev" }, repos.Select(r => r.Name).ToArray());
    }

    [TestMethod]
    public void TestRecentListIsCappedAtTheConfigMax()
    {
        var paths = Enumerable.Range(0, Config.MaxRecentFolders + 3).Select(i => Repo($"r{i}")).ToList();

        var repos = StartRepos.RecentOf(paths, Home);

        Assert.AreEqual(Config.MaxRecentFolders, repos.Count);
        Assert.AreEqual("r0", repos[0].Name);
    }

    // The repositories in the folder gmd was started in, by name, except dot folders, plain folders
    // and the ones already listed as recent
    [TestMethod]
    public void TestRepositoriesInTheStartFolderAreListedByName()
    {
        var code = Folder("code");
        var web = Repo("code", "web-shop");
        Repo("code", "Api");
        Repo("code", "zeta");
        Repo("code", ".hidden");
        Folder("code", "notes");

        var repos = StartRepos.Of([web], code, Home);

        CollectionAssert.AreEqual(new[] { "web-shop" }, repos.Recent.Select(r => r.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "Api", "zeta" }, repos.InFolder.Select(r => r.Name).ToArray());
        Assert.IsFalse(repos.InFolder.Any(r => r.IsRecent));
        Assert.AreEqual(0, repos.MoreInFolder);
        Assert.AreEqual(Tilde("code"), repos.ShownFolder);
        Assert.IsFalse(repos.IsFolderInRepo);
    }

    [TestMethod]
    public void TestRepositoriesInTheStartFolderAreCappedAndTheRestCounted()
    {
        var code = Folder("code");
        for (int i = 0; i < StartRepos.MaxInFolder + 4; i++)
            Repo("code", $"repo{i:00}");

        var repos = StartRepos.Of([], code, Home);

        Assert.AreEqual(StartRepos.MaxInFolder, repos.InFolder.Count);
        Assert.AreEqual("repo00", repos.InFolder[0].Name);
        Assert.AreEqual(4, repos.MoreInFolder);
    }

    // Only '-m' shows the start page inside a repository, which is then not said to be no repository
    [TestMethod]
    public void TestAFolderInsideARepositoryIsSaid()
    {
        var gmd = Repo("code", "gmd");
        var src = Folder("code", "gmd", "src");

        Assert.IsTrue(StartRepos.Of([], src, Home).IsFolderInRepo);
        Assert.IsTrue(StartRepos.Of([], gmd, Home).IsFolderInRepo);
    }

    [TestMethod]
    public void TestAMissingStartFolderListsNothingFromIt()
    {
        var gmd = Repo("code", "gmd");

        var repos = StartRepos.Of([gmd], Path.Join(Home, "missing"), Home);

        Assert.AreEqual(1, repos.Recent.Count);
        Assert.AreEqual(0, repos.InFolder.Count);
    }
}
