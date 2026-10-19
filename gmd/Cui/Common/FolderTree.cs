using gmd.Git;

namespace gmd.Cui.Common;

// The disk as the folder browser reads it, so that FolderTree and what typing finds are tested with
// no disk
interface IFolderReader
{
    // The folders in a folder, by their whole paths, in any order; null when it is no folder that
    // can be read
    IReadOnlyList<string>? FoldersIn(string path);

    // Whether a folder is the working tree of a repository
    bool IsRepo(string path);
}

class FolderReader : IFolderReader
{
    public IReadOnlyList<string>? FoldersIn(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).ToList();
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            // Not there, no access to it, or some other error reading it
            return null;
        }
    }

    public bool IsRepo(string path) => GitDir.IsRoot(path);
}

// What the folder browser shows while nothing is typed: the folders it starts from, each by its whole
// path ('~/code', '~', '/'), the first of them open, and under each folder opened the folders in it.
// A folder is read from the disk only as it is opened, and once, so opening the browser reads only
// the first, and a folder listed is looked into only for its .git, which says it is a repository.
// Until read, a folder is drawn as one that can be opened; once read, one with no folders in it is
// not. Folders are in name order, case ignored, those whose name starts with a '.' last: seldom the
// one wanted, but there to be had, as .worktrees is. Which folders are open, the rows that makes, and
// where the arrow keys go. Plain, the disk read through IFolderReader, so it is unit testable.
class FolderTree
{
    // A row: a folder by its whole path and the name shown, how deep it is, whether it is open,
    // whether it is known to have no folders in it, and whether it is a repository
    public record Row(string Path, string Name, int Depth, bool IsOpen, bool IsEmpty, bool IsRepo);

    class Node
    {
        public Node(string path, string name, Node? parent)
        {
            Path = path;
            Name = name;
            Parent = parent;
        }

        public string Path { get; }
        public string Name { get; }
        public Node? Parent { get; }
        public List<Node>? Folders { get; set; } // Null until read
        public bool IsOpen { get; set; }
    }

    static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    static readonly IComparer<string> NameOrder = Comparer<string>.Create(
        (a, b) =>
            IsHidden(a) != IsHidden(b) ? (IsHidden(a) ? 1 : -1)
            : StringComparer.OrdinalIgnoreCase.Compare(a, b) is var c && c != 0 ? c
            : StringComparer.Ordinal.Compare(a, b)
    );

    readonly IFolderReader reader;
    readonly List<Node> roots;
    readonly Dictionary<string, IReadOnlyList<string>?> read = []; // The folders in each folder read, in name order
    readonly Dictionary<string, bool> repos = [];
    List<(Node Node, int Depth)> shown = [];
    List<Row>? rows;

    // The roots are the folders to start from, the most likely first, and home is what '~' is
    public FolderTree(IReadOnlyList<string> roots, string home, IFolderReader reader)
    {
        this.reader = reader;
        this.roots = roots.Select(r => new Node(r, Files.ShortHomePath(r, home), null)).ToList();
        Update();
        Expand(0);
    }

    public IReadOnlyList<Row> Rows =>
        rows ??= shown
            .Select(s => new Row(
                s.Node.Path,
                s.Node.Name,
                s.Depth,
                s.Node.IsOpen,
                s.Node.Folders?.Count == 0,
                IsRepo(s.Node.Path)
            ))
            .ToList();

    // Every folder listed so far, in the tree or for a path typed, each once: what typing finds
    // among
    public IReadOnlyList<string> Listed =>
        roots.Select(r => r.Path).Concat(read.Values.SelectMany(f => f ?? [])).Distinct().ToList();

    // The folders in a folder in name order, read from the disk the first time; null when it is no
    // folder that can be read
    public IReadOnlyList<string>? FoldersIn(string path)
    {
        if (!read.TryGetValue(path, out var folders))
        {
            folders = reader.FoldersIn(path)?.OrderBy(Name, NameOrder).ToList();
            read[path] = folders;
        }
        return folders;
    }

    public bool IsRepo(string path)
    {
        if (!repos.TryGetValue(path, out var isRepo))
        {
            isRepo = reader.IsRepo(path);
            repos[path] = isRepo;
        }
        return isRepo;
    }

    // The name of the folder a path is, its last part
    public static string Name(string path) => Path.GetFileName(path.TrimEnd(Separators));

    // What a click on a folder's marker does: opens or closes it, the cursor staying on it
    public void Toggle(int index)
    {
        if (At(index) is not Node folder)
            return;
        if (folder.IsOpen)
            folder.IsOpen = false;
        else
            folder.IsOpen = Read(folder).Count > 0;
        Update();
    }

    // What → does: opens a closed folder, reading it the first time, or goes into an open one, to its
    // first row. The row the cursor goes to, the same one for a folder with no folders in it.
    public int Expand(int index)
    {
        if (At(index) is not Node folder)
            return index;
        if (folder.IsOpen)
            return index + 1;
        folder.IsOpen = Read(folder).Count > 0;
        Update();
        return index;
    }

    // What ← does: closes an open folder, or goes up to the folder a closed one is in. The row the
    // cursor goes to.
    public int Collapse(int index)
    {
        if (At(index) is not Node node)
            return index;
        if (node.IsOpen)
        {
            node.IsOpen = false;
            Update();
            return index;
        }
        return node.Parent is Node parent ? shown.FindIndex(s => s.Node == parent) : index;
    }

    Node? At(int index) => index >= 0 && index < shown.Count ? shown[index].Node : null;

    static bool IsHidden(string name) => name.StartsWith('.');

    List<Node> Read(Node folder) =>
        folder.Folders ??= (FoldersIn(folder.Path) ?? []).Select(p => new Node(p, Name(p), folder)).ToList();

    // The rows of the tree: each folder's own after it while it is open
    void Update()
    {
        List<(Node, int)> rows = [];
        void AddRows(IEnumerable<Node> folders, int depth)
        {
            foreach (var folder in folders)
            {
                rows.Add((folder, depth));
                if (folder.IsOpen)
                    AddRows(folder.Folders ?? [], depth + 1);
            }
        }
        AddRows(roots, 0);
        shown = rows;
        this.rows = null;
    }
}
