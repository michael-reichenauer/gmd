using gmd.Cui.Common;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Common;

// The rows of the folder browser, as text and colors: the tree's, and the folders found for a text
[TestClass]
public class FolderBrowseRowsTest
{
    // The guides and the marker dark, '▾' open, '▸' closed and nothing for a folder with no folders in
    // it, the name light blue, and at the right a dark 'repo' for a repository
    [TestMethod]
    public void TestATreeRow()
    {
        var open = FolderBrowseRows.TreeRow(new("/c/gmd", "gmd", 1, true, false, true), 24);
        var closed = FolderBrowseRows.TreeRow(new("/c/gmd/src", "src", 2, false, false, false), 24);
        var empty = FolderBrowseRows.TreeRow(new("/c/gmd/docs", "docs", 2, false, true, false), 24);

        Assert.AreEqual(" │ ▾ gmd           repo ", open.ToString());
        Assert.AreEqual(" D D CCC           DDDD", TextColors.Of(open));
        Assert.AreEqual(" │ │ ▸ src              ", closed.ToString());
        Assert.AreEqual(" │ │   docs             ", empty.ToString());
        Assert.AreEqual(24, empty.Length, "The width of the list");
    }

    // A name too long for the row is cut at its end, before the mark
    [TestMethod]
    public void TestATreeRowWithALongNameIsCut()
    {
        var row = FolderBrowseRows.TreeRow(new("/c/a-very-long-name", "a-very-long-name", 0, false, false, true), 20);

        Assert.AreEqual(" ▸ a-very-lon… repo ", row.ToString());
    }

    // The marker is where a click opens or closes the folder, after the guides
    [TestMethod]
    public void TestTheMarkerIsAfterTheGuides()
    {
        var row = new FolderTree.Row("/c/gmd/src", "src", 2, false, false, false);

        Assert.IsFalse(FolderBrowseRows.IsOnMarker(row, 4), "On a guide");
        Assert.IsTrue(FolderBrowseRows.IsOnMarker(row, 5));
        Assert.IsTrue(FolderBrowseRows.IsOnMarker(row, 6));
        Assert.IsFalse(FolderBrowseRows.IsOnMarker(row, 7), "On the name");
    }

    // A folder found: the folders it is in dark, its name light blue, the words typed yellow, and
    // the root, which is all separator, as a name
    [TestMethod]
    public void TestAFoundRow()
    {
        var row = FolderBrowseRows.FoundRow("~/code/gmd", true, ["gm"], 24);
        var root = FolderBrowseRows.FoundRow("/", false, [], 24);

        Assert.AreEqual(" ~/code/gmd        repo ", row.ToString());
        Assert.AreEqual(" DDDDDDDyyC        DDDD", TextColors.Of(row));
        Assert.AreEqual(" C", TextColors.Of(root));
    }

    // A path too long for the row is cut at its start, so the name is kept
    [TestMethod]
    public void TestAFoundRowWithALongPathKeepsItsName()
    {
        var row = FolderBrowseRows.FoundRow("~/code/some/deep/folder", false, [], 20);

        Assert.AreEqual(" …/deep/folder      ", row.ToString());
    }
}
