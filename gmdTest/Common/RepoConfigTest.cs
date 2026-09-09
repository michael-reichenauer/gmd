using gmd.Common;
using gmd.Common.Private;

namespace gmdTest.Common;

// The per-repository config over the real file store, on a repository layout written by hand
[TestClass]
public class RepoConfigTest
{
    string root = "";

    [TestInitialize]
    public void Init()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-repoconfig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [TestMethod]
    public void TestConfigIsStoredInTheGitFolder()
    {
        var main = Path.Join(root, "main");
        Directory.CreateDirectory(Path.Join(main, ".git"));
        var config = new RepoConfigImpl(new FileStore());

        config.Set(main, c => c.Branches = ["dev"]);

        Assert.IsTrue(File.Exists(Path.Join(main, ".git", ".gmdconfig")));
        CollectionAssert.AreEqual(new[] { "dev" }, config.Get(main).Branches);
    }

    // A linked worktree shares the repository's config: it is one file, in the common git dir,
    // and writing it through the worktree must not try to write into the worktree's '.git' file
    [TestMethod]
    public void TestLinkedWorktreeSharesTheRepositoryConfig()
    {
        var main = Path.Join(root, "main");
        var worktreeGitDir = Path.Join(main, ".git", "worktrees", "dev");
        Directory.CreateDirectory(worktreeGitDir);
        File.WriteAllText(Path.Join(worktreeGitDir, "commondir"), "../..\n");
        var worktree = Path.Join(root, "main-dev");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Join(worktree, ".git"), $"gitdir: {worktreeGitDir}\n");
        var config = new RepoConfigImpl(new FileStore());

        config.Set(main, c => c.Branches = ["dev"]);
        config.Set(worktree, c => c.BranchColors["dev"] = 3);

        Assert.IsTrue(File.Exists(Path.Join(main, ".git", ".gmdconfig")));
        Assert.IsFalse(Directory.Exists(Path.Join(worktree, ".git")), "The worktree's '.git' is still a file");
        Assert.AreEqual(0, Directory.GetFiles(worktreeGitDir).Count(f => f.EndsWith(".gmdconfig")));
        CollectionAssert.AreEqual(new[] { "dev" }, config.Get(worktree).Branches);
        Assert.AreEqual(3, config.Get(main).BranchColors["dev"]);
    }
}
