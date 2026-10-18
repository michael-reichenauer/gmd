using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// What the file browser shows while nothing is typed: the files the commit changed under a heading,
// then the tree of folders, the order of its rows, and where the arrow keys and Enter go. Each is
// asserted as the rows FileBrowseRows draws, 30 wide, the cursor's row marked '>'.
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

    static FileTree Tree(IReadOnlyDictionary<string, char> changes) => new(Files, changes, "Changed in c1");

    // Folders before files, each in name order with case ignored, all closed when nothing changed
    [TestMethod]
    public void TestFoldersComeFirstEachInNameOrder()
    {
        var tree = Tree(NoChanges);

        Assert.AreEqual(
            """
            > ▸ docs
              ▸ src
              ▸ Src2
                build.sh
                README.md
            """,
            Picture(tree, tree.First)
        );
    }

    // The files the commit changed come first, under the heading given, in path order, and the cursor
    // starts on the first of them. Then the tree, all closed whatever changed, the same every time,
    // where a changed file has its letter too, and a folder holding one a '•'.
    [TestMethod]
    public void TestTheChangedFilesComeFirstUnderAHeading()
    {
        var tree = Tree(new Dictionary<string, char> { ["src/app/View.cs"] = 'M', ["build.sh"] = 'A' });

        Assert.AreEqual(
            """
              Changed in c1 ──────────────
            >   build.sh                 A
                src/app/View.cs          M
              All files ──────────────────
              ▸ docs
              ▸ src                      •
              ▸ Src2
                build.sh                 A
                README.md
            """,
            Picture(tree, tree.First)
        );
    }

    // A change to a file that is not listed, e.g. one the commit deleted, adds and marks nothing
    [TestMethod]
    public void TestAChangeToAFileNotListedIsLeftOut()
    {
        var tree = Tree(new Dictionary<string, char> { ["src/Gone.cs"] = 'D' });

        Assert.AreEqual(5, tree.Rows.Count, "No headings either");
        Assert.IsFalse(tree.Rows.Any(r => r.HasChanges || r.Change != ' '));
        Assert.AreEqual(0, tree.First, "With no changed file listed, the cursor starts at the top");
    }

    // ↑ ↓ and the page keys go past a heading, which is no row to be on, and stop at either end
    [TestMethod]
    public void TestTheCursorStepsPastTheHeadings()
    {
        var tree = Tree(new Dictionary<string, char> { ["src/app/View.cs"] = 'M', ["build.sh"] = 'A' });

        Assert.AreEqual(1, tree.Step(1, -1), "The heading above the first changed file");
        Assert.AreEqual(4, tree.Step(2, 1), "Past the heading of the tree");
        Assert.AreEqual(2, tree.Step(4, -1), "And back");
        Assert.AreEqual(1, tree.Step(6, -10), "A page up to the top");
        Assert.AreEqual(8, tree.Step(1, 10), "A page down to the bottom");
        Assert.AreEqual(1, tree.Step(0, 0), "Off a heading");
        Assert.AreEqual(3, Tree(NoChanges).Step(2, 1), "With no headings, a row at a time");
    }

    // → opens a closed folder, the cursor staying on it, and on an open one goes to its first row
    [TestMethod]
    public void TestExpandOpensAFolderAndThenGoesIntoIt()
    {
        var tree = Tree(NoChanges);

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
    // to the folder it is in; at the top, it stays, as on a changed file above the tree
    [TestMethod]
    public void TestCollapseClosesAFolderAndThenGoesUpToIt()
    {
        var tree = Tree(new Dictionary<string, char> { ["src/app/Main.cs"] = 'M' });
        var main = tree.Expand(tree.Expand(tree.Expand(tree.Expand(4))));
        Assert.AreEqual("src/app/Main.cs", tree.Rows[main].Path);
        Assert.AreEqual(FileTree.RowKind.File, tree.Rows[main].Kind, "The one in the tree");

        var app = tree.Collapse(main);
        Assert.AreEqual("src/app", tree.Rows[app].Path, "Up from the file to its folder");
        Assert.AreEqual(app, tree.Collapse(app), "Which closes");
        var src = tree.Collapse(app);
        Assert.AreEqual("src", tree.Rows[src].Path, "Then up again");
        Assert.AreEqual(src, tree.Collapse(src));

        Assert.AreEqual(
            """
              Changed in c1 ──────────────
                src/app/Main.cs          M
              All files ──────────────────
              ▸ docs
            > ▸ src                      •
              ▸ Src2
                build.sh
                README.md
            """,
            Picture(tree, src)
        );
        Assert.AreEqual(src, tree.Collapse(src), "A closed folder at the top stays");
        Assert.AreEqual(6, tree.Collapse(6), "As does a file at the top");
        Assert.AreEqual(1, tree.Collapse(1), "And a changed file above the tree");
        Assert.AreEqual(1, tree.Expand(1));
    }

    // Enter opens a folder or closes it; on a file it is the picking, which is the dialog's
    [TestMethod]
    public void TestToggleOpensAndClosesAFolder()
    {
        var tree = Tree(NoChanges);

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
        var tree = new FileTree([], NoChanges, "Changed in c1");

        Assert.AreEqual(0, tree.Rows.Count);
        Assert.AreEqual(0, tree.First);
        Assert.AreEqual(0, tree.Step(0, 1));
        Assert.AreEqual(0, tree.Expand(0));
        Assert.AreEqual(0, tree.Collapse(0));
    }

    // Every file and folder, the rows of the tree with every folder open, and the changed files with
    // their headings
    [TestMethod]
    public void TestAllCountIsEveryRowThereCanBe()
    {
        Assert.AreEqual(Files.Length + 4, Tree(NoChanges).AllCount);
        Assert.AreEqual(Files.Length + 4 + 1 + 2, Tree(new Dictionary<string, char> { ["build.sh"] = 'A' }).AllCount);
    }

    // The rows as drawn 30 wide, with no space after them
    static string Picture(FileTree tree, int current) =>
        string.Join(
            "\n",
            tree.Rows.Select((r, i) => ((i == current ? ">" : " ") + FileBrowseRows.TreeRow(r, 30)).TrimEnd())
        );
}
