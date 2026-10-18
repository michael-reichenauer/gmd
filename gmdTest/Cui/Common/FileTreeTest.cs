using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// The tree of folders the file browser shows while nothing is typed: the order of its rows, the
// folders opened for the files the commit changed, and where the arrow keys and Enter go. Each
// tree is asserted as the rows FileBrowseRows draws, the cursor's row marked '>'.
[TestClass]
public class FileTreeTest
{
    static readonly string[] Files =
    [
        "README.md",
        "src/app/Main.cs",
        "src/app/View.cs",
        "src/Util.cs",
        "docs/guide.md",
        "build.sh",
        "Src2/x.cs",
    ];

    static readonly Dictionary<string, char> NoChanges = [];

    // Folders before files, each in name order with case ignored, all closed when nothing changed
    [TestMethod]
    public void TestFoldersComeFirstEachInNameOrder()
    {
        var tree = new FileTree(Files, NoChanges);

        Assert.AreEqual(
            """
            > ▸ docs
              ▸ src
              ▸ Src2
                build.sh
                README.md
            """,
            Picture(tree, tree.FirstChanged)
        );
    }

    // The folders of a changed file are open, so it is in sight, and the cursor starts on it. Each of
    // them says it holds a change, a file its letter.
    [TestMethod]
    public void TestTheFoldersOfAChangedFileAreOpenWithTheCursorOnIt()
    {
        var tree = new FileTree(Files, new Dictionary<string, char> { ["src/app/View.cs"] = 'M', ["build.sh"] = 'A' });

        Assert.AreEqual(
            """
              ▸ docs
              ▾ src                      •
              │ ▾ app                    •
              │ │   Main.cs
            > │ │   View.cs              M
              │   Util.cs
              ▸ Src2
                build.sh                 A
                README.md
            """,
            Picture(tree, tree.FirstChanged)
        );
    }

    // A change to a file that is not listed, e.g. one the commit deleted, marks and opens nothing
    [TestMethod]
    public void TestAChangeToAFileNotListedIsLeftOut()
    {
        var tree = new FileTree(Files, new Dictionary<string, char> { ["src/Gone.cs"] = 'D' });

        Assert.AreEqual(5, tree.Rows.Count);
        Assert.IsFalse(tree.Rows.Any(r => r.HasChanges || r.Change != ' '));
        Assert.AreEqual(0, tree.FirstChanged, "With no changed file listed, the cursor starts at the top");
    }

    // → opens a closed folder, the cursor staying on it, and on an open one goes to its first row
    [TestMethod]
    public void TestExpandOpensAFolderAndThenGoesIntoIt()
    {
        var tree = new FileTree(Files, NoChanges);

        var index = tree.Expand(1);
        Assert.AreEqual(1, index);
        index = tree.Expand(index);
        Assert.AreEqual(2, index);
        index = tree.Expand(index);

        Assert.AreEqual(
            """
              ▸ docs
              ▾ src
            > │ ▾ app
              │ │   Main.cs
              │ │   View.cs
              │   Util.cs
              ▸ Src2
                build.sh
                README.md
            """,
            Picture(tree, index)
        );
        Assert.AreEqual(3, tree.Expand(3), "On a file, → does nothing");
    }

    // ← closes an open folder, the cursor staying on it, and from a file or a closed folder goes up
    // to the folder it is in; at the top, it stays
    [TestMethod]
    public void TestCollapseClosesAFolderAndThenGoesUpToIt()
    {
        var tree = new FileTree(Files, new Dictionary<string, char> { ["src/app/Main.cs"] = 'M' });
        var main = tree.FirstChanged;
        Assert.AreEqual("src/app/Main.cs", tree.Rows[main].Path);

        var app = tree.Collapse(main);
        Assert.AreEqual("src/app", tree.Rows[app].Path, "Up from the file to its folder");
        Assert.AreEqual(app, tree.Collapse(app), "Which closes");
        var src = tree.Collapse(app);
        Assert.AreEqual("src", tree.Rows[src].Path, "Then up again");
        Assert.AreEqual(src, tree.Collapse(src));

        Assert.AreEqual(
            """
              ▸ docs
            > ▸ src                      •
              ▸ Src2
                build.sh
                README.md
            """,
            Picture(tree, src)
        );
        Assert.AreEqual(src, tree.Collapse(src), "A closed folder at the top stays");
        Assert.AreEqual(4, tree.Collapse(4), "As does a file at the top");
    }

    // Enter opens a folder or closes it; on a file it is the picking, which is the dialog's
    [TestMethod]
    public void TestToggleOpensAndClosesAFolder()
    {
        var tree = new FileTree(Files, NoChanges);

        tree.Toggle(0);
        Assert.AreEqual("docs/guide.md", tree.Rows[1].Path);
        tree.Toggle(0);
        Assert.AreEqual("src", tree.Rows[1].Path);
        tree.Toggle(4);
        Assert.AreEqual(5, tree.Rows.Count, "A file is no folder to open");
    }

    // The tree of a commit with no files, which an empty repository's first commit can be
    [TestMethod]
    public void TestATreeOfNoFiles()
    {
        var tree = new FileTree([], NoChanges);

        Assert.AreEqual(0, tree.Rows.Count);
        Assert.AreEqual(0, tree.FirstChanged);
        Assert.AreEqual(0, tree.Expand(0));
        Assert.AreEqual(0, tree.Collapse(0));
    }

    // Every file and folder, the rows of the tree with every folder open
    [TestMethod]
    public void TestAllCountIsEveryFileAndFolder()
    {
        Assert.AreEqual(Files.Length + 4, new FileTree(Files, NoChanges).AllCount);
    }

    // The rows as drawn 30 wide, with no space after them
    static string Picture(FileTree tree, int current) =>
        string.Join(
            "\n",
            tree.Rows.Select((r, i) => ((i == current ? ">" : " ") + FileBrowseRows.TreeRow(r, 30)).TrimEnd())
        );
}
