using gmd.Cui.Start;

namespace gmdTest.Cui.Start;

// What Init proposes and refuses, which Enter, pressed out of habit, would otherwise take as it is
[TestClass]
public class InitPathTest
{
    static readonly string Home = Path.Join(Path.GetTempPath(), "home");

    // The start folder, as the start page says, but in the home folder a folder in it, to be named:
    // Enter made the whole home folder a repo
    [TestMethod]
    public void TestTheHomeFolderIsNotProposed()
    {
        var downloads = Path.Join(Home, "Downloads");

        Assert.AreEqual(downloads, InitPath.Proposed(downloads, Home));
        Assert.AreEqual(Home + Path.DirectorySeparatorChar, InitPath.Proposed(Home, Home));
        Assert.AreEqual(
            Home + Path.DirectorySeparatorChar,
            InitPath.Proposed(Home + Path.DirectorySeparatorChar, Home)
        );
    }

    // However it is written
    [TestMethod]
    public void TestTheHomeFolderIsRefused()
    {
        Assert.IsTrue(InitPath.IsHome(Home, Home));
        Assert.IsTrue(InitPath.IsHome(Home + Path.DirectorySeparatorChar, Home));
        Assert.IsTrue(InitPath.IsHome(Path.Join(Home, "Downloads", ".."), Home));
        Assert.IsFalse(InitPath.IsHome(Path.Join(Home, "Downloads"), Home));
        Assert.IsFalse(InitPath.IsHome("", Home), "Empty is refused for being empty");
    }

    // A folder proposed to name one in, which Enter made a repo of: the folder of the recent repos
    [TestMethod]
    public void TestAFolderWithNoNameIsRefused()
    {
        Assert.IsTrue(InitPath.IsUnnamed(Path.Join(Home, "code") + Path.DirectorySeparatorChar));
        Assert.IsTrue(InitPath.IsUnnamed(Path.Join(Home, "code") + "/"));
        Assert.IsFalse(InitPath.IsUnnamed(Path.Join(Home, "code", "new")));
    }
}
