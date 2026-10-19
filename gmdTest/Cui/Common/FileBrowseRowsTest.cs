using gmd.Cui.Common;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Common;

// The rows of the file browser, as text and colors: the tree's, and the files found for a text
[TestClass]
public class FileBrowseRowsTest
{
    // A folder's marker and its guides dark and its name light blue, a file's name white and lined up with
    // the folders beside it, and at the right a changed file's letter or the '•' of a folder holding one
    [TestMethod]
    public void TestATreeRow()
    {
        var folder = FileBrowseRows.TreeRow(new(FileTree.RowKind.Folder, "src/app", "app", 1, true, ' ', true), 20);
        var file = FileBrowseRows.TreeRow(
            new(FileTree.RowKind.File, "src/app/Main.cs", "Main.cs", 2, false, 'M', false),
            20
        );
        var plain = FileBrowseRows.TreeRow(
            new(FileTree.RowKind.File, "src/Util.cs", "Util.cs", 1, false, ' ', false),
            20
        );

        Assert.AreEqual(" │ ▾ app          • ", folder.ToString());
        Assert.AreEqual(" D D CCC          Y", TextColors.Of(folder));
        Assert.AreEqual(" │ │   Main.cs    M ", file.ToString());
        Assert.AreEqual(" D D   WWWWWWW    Y", TextColors.Of(file));
        Assert.AreEqual(" │   Util.cs        ", plain.ToString());
        Assert.AreEqual(20, plain.Length, "The width of the list");
    }

    // A name too long for the row is cut at its end, keeping the mark at the right
    [TestMethod]
    public void TestATreeRowWithALongNameIsCut()
    {
        var row = FileBrowseRows.TreeRow(
            new(FileTree.RowKind.File, "AVeryLongFileName.cs", "AVeryLongFileName.cs", 0, false, 'A', false),
            20
        );

        Assert.AreEqual("   AVeryLongFile… A ", row.ToString());
    }

    // A heading, white with a dark rule after it, and a changed file above the tree, its path lined
    // up with the names of the tree, its folders dark
    [TestMethod]
    public void TestAHeadingAndAChangedFile()
    {
        var heading = FileBrowseRows.TreeRow(new(FileTree.RowKind.Heading, "", "All files", 0, false, ' ', false), 20);
        var changed = FileBrowseRows.TreeRow(
            new(FileTree.RowKind.Changed, "src/a.cs", "src/a.cs", 0, false, 'M', false),
            20
        );

        Assert.AreEqual(" All files ──────── ", heading.ToString());
        Assert.AreEqual(" WWW WWWWW DDDDDDDD", TextColors.Of(heading));
        Assert.AreEqual("   src/a.cs       M ", changed.ToString());
        Assert.AreEqual("   DDDDWWWW       Y", TextColors.Of(changed));
    }

    // A file found: its folders dark, its name white, and the words typed in yellow wherever they
    // are, in any case
    [TestMethod]
    public void TestAFoundRowMarksTheWords()
    {
        var row = FileBrowseRows.FoundRow("gmd/Cui/ListDlg.cs", 'M', ["cui", "DLG"], 26);

        Assert.AreEqual(" gmd/Cui/ListDlg.cs     M ", row.ToString());
        Assert.AreEqual(" DDDDyyyDWWWWyyyWWW     Y", TextColors.Of(row));
    }

    // A path too long for the row is cut at its start, where its folders are, so its name is kept
    [TestMethod]
    public void TestAFoundRowWithALongPathKeepsItsName()
    {
        var row = FileBrowseRows.FoundRow("gmd/Server/Private/Augmented/Commit.cs", ' ', ["commit"], 24);

        Assert.AreEqual(" …Augmented/Commit.cs   ", row.ToString());
    }
}
