using gmd.Server.Private.Augmented.Private;

namespace gmdTest.Server.Private.Augmented.Private;

// The metadata's file, in the repository's common git dir, on the disk
[TestClass]
public class MetaDataFileTest
{
    string wd = "";

    [TestInitialize]
    public void Init()
    {
        wd = Path.Join(Path.GetTempPath(), $"gmdTest-metadatafile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(wd, ".git"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(wd))
            Directory.Delete(wd, true);
    }

    [TestMethod]
    public async Task TestWriteAndReadRoundTrip()
    {
        var file = new MetaDataFile();
        Assert.IsFalse(file.Exists(wd));

        AssertOk(await file.WriteAsync(wd, "{ \"a\": 1 }"));
        AssertOk(await file.WriteAsync(wd, "{ \"b\": 2 }"));

        Assert.IsTrue(file.Exists(wd));
        Assert.AreEqual("{ \"b\": 2 }", AssertOk(await file.ReadAsync(wd)));
        CollectionAssert.AreEqual(
            new[] { MetaDataFile.FileName },
            Directory.GetFiles(Path.Join(wd, ".git")).Select(Path.GetFileName).ToArray(),
            "In the git dir, and no temp file is left"
        );
    }

    // A linked worktree's '.git' is a file, and its git dir has a 'commondir' pointing at the main
    // repository's, where the file is, so that every worktree shares it, as they share the branches
    [TestMethod]
    public async Task TestALinkedWorktreeSharesTheFileOfItsRepository()
    {
        var commonDir = Path.Join(wd, ".git");
        var worktreeGitDir = Path.Join(commonDir, "worktrees", "other");
        Directory.CreateDirectory(worktreeGitDir);
        File.WriteAllText(Path.Join(worktreeGitDir, "commondir"), "../..\n");
        var worktree = Path.Join(wd, "other");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Join(worktree, ".git"), $"gitdir: {worktreeGitDir}\n");
        var file = new MetaDataFile();

        AssertOk(await file.WriteAsync(worktree, "{}"));

        Assert.IsTrue(File.Exists(Path.Join(commonDir, MetaDataFile.FileName)));
        Assert.IsTrue(file.Exists(wd));
    }

    [TestMethod]
    public async Task TestReadWithNoFileIsAnError()
    {
        AssertError(await new MetaDataFile().ReadAsync(wd));
    }
}
