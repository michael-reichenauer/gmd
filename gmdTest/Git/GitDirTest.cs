using gmd.Git;

namespace gmdTest.Git;

// The layouts git leaves on disk, written by hand so no repository or git executable is needed:
// a '.git' folder for the main worktree, a '.git' file with a 'gitdir:' pointer for a linked
// worktree (which also has a 'commondir' file in its gitdir) and for a submodule (which does not).
[TestClass]
public class GitDirTest
{
    string root = "";

    [TestInitialize]
    public void Init()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-gitdir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    string Main => Path.Join(root, "main");
    string MainGitDir => Path.Join(Main, ".git");

    string CreateMain()
    {
        Directory.CreateDirectory(Path.Join(MainGitDir, "refs"));
        return Main;
    }

    // A linked worktree at 'path', exactly as 'git worktree add' lays it out
    string CreateLinkedWorktree(string path, string name)
    {
        var gitDir = Path.Join(MainGitDir, "worktrees", name);
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Join(gitDir, "commondir"), "../..\n");
        File.WriteAllText(Path.Join(gitDir, "HEAD"), "ref: refs/heads/dev\n");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Join(path, ".git"), $"gitdir: {gitDir}\n");
        return path;
    }

    static GitDirInfo Value(R<GitDirInfo> result)
    {
        Assert.IsTrue(Try(out var info, out var e, result), $"{e}");
        return info;
    }

    [TestMethod]
    public void TestMainWorktreeKeepsEverythingInItsGitFolder()
    {
        var main = CreateMain();

        var info = Value(GitDir.Resolve(main));

        Assert.AreEqual(main, info.RootPath);
        Assert.AreEqual(MainGitDir, info.GitDirPath);
        Assert.AreEqual(MainGitDir, info.CommonDirPath);
        Assert.IsFalse(info.IsLinkedWorktree);
    }

    [TestMethod]
    public void TestLinkedWorktreeFollowsTheGitDirFileAndTheCommonDirFile()
    {
        CreateMain();
        var worktree = CreateLinkedWorktree(Path.Join(root, "main-dev"), "dev");

        var info = Value(GitDir.Resolve(worktree));

        Assert.AreEqual(worktree, info.RootPath);
        Assert.AreEqual(Path.GetFullPath(Path.Join(MainGitDir, "worktrees", "dev")), info.GitDirPath);
        Assert.AreEqual(Path.GetFullPath(MainGitDir), info.CommonDirPath);
        Assert.IsTrue(info.IsLinkedWorktree);
    }

    // A submodule's pointer is relative to the folder holding the '.git' file, and there is no
    // 'commondir': a submodule is a repository of its own
    [TestMethod]
    public void TestRelativeGitDirPointerIsResolvedAgainstTheRootAndIsNotLinked()
    {
        var sub = Path.Join(root, "sub");
        var gitDir = Path.Join(sub, "modules", "sub");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Join(sub, ".git"), "gitdir: modules/sub\n");

        var info = Value(GitDir.Resolve(sub));

        Assert.AreEqual(Path.GetFullPath(gitDir), info.GitDirPath);
        Assert.AreEqual(Path.GetFullPath(gitDir), info.CommonDirPath);
        Assert.IsFalse(info.IsLinkedWorktree);
    }

    [TestMethod]
    public void TestResolveFailsWithoutAGitFolderOrFile()
    {
        Assert.IsFalse(Try(out var _, out var _, GitDir.Resolve(root)));
    }

    [TestMethod]
    public void TestFindWalksUpToTheRoot()
    {
        var main = CreateMain();
        var sub = Path.Join(main, "a", "b");
        Directory.CreateDirectory(sub);

        Assert.AreEqual(main, Value(GitDir.Find(sub)).RootPath);
        Assert.AreEqual(main, Value(GitDir.Find(main)).RootPath);
    }

    // A worktree nested inside the main repository's folder (where Claude Code puts its own) is a
    // root of its own, so the walk stops there rather than at the main repository above it
    [TestMethod]
    public void TestFindStopsAtANestedLinkedWorktree()
    {
        var main = CreateMain();
        var nested = CreateLinkedWorktree(Path.Join(main, ".claude", "worktrees", "x"), "x");
        var sub = Path.Join(nested, "sub");
        Directory.CreateDirectory(sub);

        var info = Value(GitDir.Find(sub));

        Assert.AreEqual(nested, info.RootPath);
        Assert.IsTrue(info.IsLinkedWorktree);
        Assert.AreEqual(Path.GetFullPath(MainGitDir), info.CommonDirPath);
    }

    // The '.git' folder itself names the repository it belongs to
    [TestMethod]
    public void TestFindOfTheGitFolderIsTheRepository()
    {
        var main = CreateMain();

        Assert.AreEqual(main, Value(GitDir.Find(MainGitDir)).RootPath);
    }

    [TestMethod]
    public void TestFindFailsOutsideAnyRepository()
    {
        Assert.IsFalse(Try(out var _, out var _, GitDir.Find(root)), "No '.git' anywhere above the temp root");
        Assert.IsFalse(Try(out var _, out var _, GitDir.Find(Path.Join(root, "missing"))), "Folder does not exist");
    }
}
