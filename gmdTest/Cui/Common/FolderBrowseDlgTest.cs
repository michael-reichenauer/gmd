using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// The folders the folder browser starts from. Paths only, nothing is looked up on disk.
[TestClass]
public class FolderBrowseDlgTest
{
    // The folders given first, in the order given, which puts the most likely one at the top, then
    // home and the roots of the file system
    [TestMethod]
    public void TestRootsAreTheGivenFoldersThenHomeThenTheDrives()
    {
        var roots = FolderBrowseDlg.Roots(["/home/me/Downloads", "/home/me/code"], "/home/me", ["/"]);

        CollectionAssert.AreEqual(new[] { "/home/me/Downloads", "/home/me/code", "/home/me", "/" }, roots.ToArray());
    }

    // A folder given twice, or given and also home, is listed once, where it first comes
    [TestMethod]
    public void TestEachRootIsListedOnce()
    {
        var roots = FolderBrowseDlg.Roots(["/home/me", "/home/me/code", "/home/me/code/"], "/home/me", ["/", "/"]);

        CollectionAssert.AreEqual(new[] { "/home/me", "/home/me/code", "/" }, roots.ToArray());
    }
}
