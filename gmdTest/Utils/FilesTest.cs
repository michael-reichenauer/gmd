namespace gmdTest.Utils;

[TestClass]
public class FilesTest
{
    string root = "";

    [TestInitialize]
    public void Init()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-files-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    // A proposed folder is numbered past the ones that exist, so a dialog never proposes a folder
    // that is taken
    [TestMethod]
    public void TestUniqueFolderPathNumbersATakenName()
    {
        Assert.AreEqual(Path.Join(root, "repo-dev"), Files.UniqueFolderPath(root, "repo-dev"));

        Directory.CreateDirectory(Path.Join(root, "repo-dev"));
        Assert.AreEqual(Path.Join(root, "repo-dev-1"), Files.UniqueFolderPath(root, "repo-dev"));

        File.WriteAllText(Path.Join(root, "repo-dev-1"), "a file counts as taken too");
        Assert.AreEqual(Path.Join(root, "repo-dev-2"), Files.UniqueFolderPath(root, "repo-dev"));
    }

    [TestMethod]
    public void TestIsSamePathIgnoresTrailingSeparatorsAndRelativeSteps()
    {
        Assert.IsTrue(Files.IsSamePath(root, root + Path.DirectorySeparatorChar));
        Assert.IsTrue(Files.IsSamePath(root, Path.Join(root, "sub", "..")));
        Assert.IsFalse(Files.IsSamePath(root, Path.Join(root, "sub")));
    }

    [TestMethod]
    public void TestShortHomePathWritesHomeAsTilde()
    {
        var home = Path.Join(root, "me");

        Assert.AreEqual("~", Files.ShortHomePath(home, home));
        Assert.AreEqual("~", Files.ShortHomePath(home + Path.DirectorySeparatorChar, home));
        Assert.AreEqual(Path.Join("~", "code", "gmd"), Files.ShortHomePath(Path.Join(home, "code", "gmd"), home));
        Assert.AreEqual("~", Files.ShortHomePath(home, home + Path.DirectorySeparatorChar));
    }

    // Only home itself and what is inside it: a sibling that starts with the same letters, a path
    // outside it and no home at all leave the path as it is
    [TestMethod]
    public void TestShortHomePathLeavesOtherPathsAlone()
    {
        var home = Path.Join(root, "me");

        Assert.AreEqual(Path.Join(root, "meow"), Files.ShortHomePath(Path.Join(root, "meow"), home));
        Assert.AreEqual(root, Files.ShortHomePath(root, home));
        Assert.AreEqual(home, Files.ShortHomePath(home, ""));
    }
}
