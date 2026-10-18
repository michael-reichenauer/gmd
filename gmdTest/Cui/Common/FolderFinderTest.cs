using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// Which folders the folder browser lists for what has been typed: a path browses the folder it
// names, anything else is words found among the folders listed so far
[TestClass]
public class FolderFinderTest
{
    const string Home = "/home/me";

    // A path starts with '~' or at the root; a name, or folders and a name, are words to find
    [TestMethod]
    public void TestAPathStartsWithHomeOrTheRoot()
    {
        Assert.IsTrue(FolderFinder.IsPath("~"));
        Assert.IsTrue(FolderFinder.IsPath("~/co"));
        Assert.IsTrue(FolderFinder.IsPath("/tmp"));
        Assert.IsFalse(FolderFinder.IsPath("code"));
        Assert.IsFalse(FolderFinder.IsPath("code/gmd"));
        Assert.IsFalse(FolderFinder.IsPath("~me"), "A user's home, which gmd does not look up");
    }

    // The folder up to the last separator, '~' as home, and what is typed after it
    [TestMethod]
    public void TestAPathIsAFolderAndTheStartOfAName()
    {
        Assert.AreEqual(("/home/me/", "co"), FolderFinder.Split("~/co", Home));
        Assert.AreEqual(("/home/me/code/", ""), FolderFinder.Split("~/code/", Home));
        Assert.AreEqual(("/home/", "me"), FolderFinder.Split("~", Home), "Home, as a name in its folder");
        Assert.AreEqual(("/", ""), FolderFinder.Split("/", Home));
        Assert.AreEqual(("/tmp/", "x"), FolderFinder.Split("/tmp/x", Home));
    }

    // The folders whose name starts with what is typed first, then those whose name has it, each in
    // the order given, which is the tree's, case ignored
    [TestMethod]
    public void TestAPathListsTheFoldersStartingWithTheNameFirst()
    {
        string[] folders = ["/home/me/code", "/home/me/Documents", "/home/me/Downloads", "/home/me/.dotnet"];

        CollectionAssert.AreEqual(
            new[] { "/home/me/Documents", "/home/me/Downloads", "/home/me/code", "/home/me/.dotnet" },
            FolderFinder.InFolder("/home/me/", "d", folders).ToArray()
        );
    }

    // With nothing after the separator, the folder itself first, so that Enter picks it, then every
    // folder in it; and nothing at all for a folder that cannot be read
    [TestMethod]
    public void TestAFolderTypedComesFirst()
    {
        string[] folders = ["/home/me/code/gmd", "/home/me/code/web"];

        CollectionAssert.AreEqual(
            new[] { "/home/me/code", "/home/me/code/gmd", "/home/me/code/web" },
            FolderFinder.InFolder("/home/me/code/", "", folders).ToArray()
        );
        CollectionAssert.AreEqual(new[] { "/" }, FolderFinder.InFolder("/", "", []).ToArray(), "The root as it is");
        Assert.AreEqual(0, FolderFinder.InFolder("/nowhere/", "", null).Count);
    }

    // Words are found in the path as shown, '~' for home, in any order and case: the names starting
    // with the first word first, then the names having it, then the folders it is in alone, each the
    // less deep first, then in path order
    [TestMethod]
    public void TestWordsAreFoundInThePathAsShown()
    {
        string[] listed =
        [
            "/home/me/code/gmd/gmdTest",
            "/home/me/code",
            "/home/me/code/gmd",
            "/home/me/code/web-gmd",
            "/home/me/code/gmd/docs",
            "/tmp",
        ];

        CollectionAssert.AreEqual(
            new[]
            {
                "/home/me/code/gmd",
                "/home/me/code/gmd/gmdTest",
                "/home/me/code/web-gmd",
                "/home/me/code/gmd/docs",
            },
            FolderFinder.Find(listed, "GMD", Home).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "/home/me/code/gmd/docs" },
            FolderFinder.Find(listed, "doc ~/code", Home).ToArray(),
            "Every word, '~' too"
        );
        Assert.AreEqual(0, FolderFinder.Find(listed, "/home/me", Home).Count, "Shown as '~'");
    }
}
