using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// What the folder browser shows while nothing is typed: the folders it starts from, the first open,
// the order of the rows, where the arrow keys go, and what is read from the disk when. Each is
// asserted as the rows FolderBrowseRows draws, 30 wide, the cursor's row marked '>', over a disk
// that is a dictionary.
[TestClass]
public class FolderTreeTest
{
    const string Home = "/home/me";

    // The folders in each folder, by name, and the repositories among them
    class FakeFolders : IFolderReader
    {
        readonly Dictionary<string, string[]> folders = new()
        {
            ["/home/me/code"] = ["web-shop", "gmd", ".worktrees", "Notes"],
            ["/home/me/code/gmd"] = ["src", "docs"],
            ["/home/me/code/gmd/src"] = [],
            ["/home/me"] = ["code", "Downloads"],
            ["/"] = ["home", "tmp"],
        };
        readonly HashSet<string> repos = ["/home/me/code/gmd", "/home/me/code/web-shop"];

        public List<string> Reads { get; } = [];

        public IReadOnlyList<string>? FoldersIn(string path)
        {
            Reads.Add(path);
            return folders.TryGetValue(path, out var names)
                ? names.Select(n => $"{path.TrimEnd('/')}/{n}").ToList()
                : null;
        }

        public bool IsRepo(string path) => repos.Contains(path);
    }

    static (FolderTree, FakeFolders) Tree()
    {
        var disk = new FakeFolders();
        return (new FolderTree(["/home/me/code", Home, "/"], Home, disk), disk);
    }

    // The folders to start from by their whole paths, home as '~', the first open with the cursor on
    // it, its folders in name order with case ignored, the ones starting with a '.' last, and the
    // repositories marked. Only the folder opened has been read.
    [TestMethod]
    public void TestTheFirstFolderIsOpen()
    {
        var (tree, disk) = Tree();

        Assert.AreEqual(
            """
            > ▾ ~/code
              │ ▸ gmd                 repo
              │ ▸ Notes
              │ ▸ web-shop            repo
              │ ▸ .worktrees
              ▸ ~
              ▸ /
            """,
            Picture(tree, 0)
        );
        CollectionAssert.AreEqual(new[] { "/home/me/code" }, disk.Reads);
    }

    // → opens a folder, reading it the first time; → on an open one goes into it, to its first row,
    // and on one with no folders in it stays, which no longer has the marker that it can be opened
    [TestMethod]
    public void TestRightOpensAFolderAndGoesIntoIt()
    {
        var (tree, disk) = Tree();

        Assert.AreEqual(1, tree.Expand(1));
        Assert.AreEqual(2, tree.Expand(1), "Into the open folder");
        Assert.AreEqual(3, tree.Expand(3), "src has no folders in it");

        Assert.AreEqual(
            """
              ▾ ~/code
              │ ▾ gmd                 repo
              │ │ ▸ docs
            > │ │   src
              │ ▸ Notes
            """,
            Picture(tree, 3, 5)
        );
        CollectionAssert.AreEqual(
            new[] { "/home/me/code", "/home/me/code/gmd", "/home/me/code/gmd/src" },
            disk.Reads,
            "Each read once"
        );
    }

    // ← closes an open folder, the cursor staying on it, then goes up to the folder it is in; on a
    // closed folder at the top, there is nowhere to go
    [TestMethod]
    public void TestLeftClosesAFolderThenGoesUp()
    {
        var (tree, _) = Tree();
        tree.Expand(1);

        Assert.AreEqual(1, tree.Collapse(2), "docs, closed, goes up to gmd");
        Assert.AreEqual(1, tree.Collapse(1), "gmd closes");
        Assert.AreEqual(0, tree.Collapse(1), "Up to ~/code");
        Assert.AreEqual(0, tree.Collapse(0), "~/code closes");
        Assert.AreEqual(1, tree.Collapse(1), "~ is at the top, and closed");

        Assert.AreEqual(
            """
              ▸ ~/code
            > ▸ ~
              ▸ /
            """,
            Picture(tree, 1)
        );
    }

    // A click on the marker opens or closes a folder, the cursor staying where it is
    [TestMethod]
    public void TestToggleOpensAndCloses()
    {
        var (tree, _) = Tree();

        tree.Toggle(0);
        Assert.AreEqual(3, tree.Rows.Count, "~/code closed");
        tree.Toggle(0);
        Assert.AreEqual(7, tree.Rows.Count, "And open again");
    }

    // What typing finds among is every folder listed so far, each once, though ~/code is also in ~:
    // the folders started from, and the folders in each folder read, also for a path typed
    [TestMethod]
    public void TestListedIsEveryFolderReadEachOnce()
    {
        var (tree, _) = Tree();
        tree.Expand(5); // ~

        CollectionAssert.AreEquivalent(
            new[]
            {
                "/home/me/code",
                "/home/me",
                "/",
                "/home/me/code/gmd",
                "/home/me/code/Notes",
                "/home/me/code/web-shop",
                "/home/me/code/.worktrees",
                "/home/me/Downloads",
            },
            tree.Listed.ToArray()
        );

        tree.FoldersIn("/");
        CollectionAssert.Contains(tree.Listed.ToArray(), "/tmp");
    }

    // A folder is read from the disk once, in name order, and one that cannot be read is null
    [TestMethod]
    public void TestFoldersInReadsOnce()
    {
        var (tree, disk) = Tree();

        CollectionAssert.AreEqual(new[] { "/home/me/code", "/home/me/Downloads" }, tree.FoldersIn(Home)!.ToArray());
        tree.FoldersIn(Home);
        Assert.IsNull(tree.FoldersIn("/nowhere"));
        tree.FoldersIn("/nowhere");

        CollectionAssert.AreEqual(new[] { "/home/me/code", Home, "/nowhere" }, disk.Reads);
    }

    static string Picture(FolderTree tree, int current, int count = int.MaxValue) =>
        string.Join(
            "\n",
            tree.Rows.Take(count)
                .Select((r, i) => ((i == current ? ">" : " ") + FolderBrowseRows.TreeRow(r, 30)).TrimEnd())
        );
}
