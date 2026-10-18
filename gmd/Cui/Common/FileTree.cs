namespace gmd.Cui.Common;

// What the file browser shows while nothing is typed: the files the commit changed, under a heading
// that says so, since they are the ones most likely wanted, and then every file of the commit as a
// tree of folders, all closed to begin with, so that it looks the same whatever the commit changed.
// Which folders are open, the rows that makes, and where the arrow keys go, past the headings, which
// are no rows to be on. Folders come before files, each in name order, case ignored, as a file
// manager lists them. A changed file has the letter of its change in the tree too, and a folder
// holding one says so. Plain, with no view, so it is unit testable.
class FileTree
{
    public enum RowKind
    {
        Heading,
        Changed, // A file the commit changed, in the list of them above the tree
        Folder,
        File,
    }

    // A row: a heading, a changed file above the tree, or a folder or a file of the tree, how deep it
    // is there, whether a folder is open, and the letter of a file's change in the commit (' ' for
    // none), or for a folder whether it holds a changed file
    public record Row(RowKind Kind, string Path, string Name, int Depth, bool IsOpen, char Change, bool HasChanges)
    {
        public bool IsHeading => Kind == RowKind.Heading;
        public bool IsFolder => Kind == RowKind.Folder;
    }

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
        public List<Node> Children { get; } = [];
        public bool IsFolder => Children.Count > 0; // Git has no empty folders
        public char Change { get; set; } = ' ';
        public bool HasChanges { get; set; }
    }

    public const string AllFilesHeading = "All files";

    static readonly IComparer<Node> NameOrder = Comparer<Node>.Create(
        (a, b) =>
            a.IsFolder != b.IsFolder ? (a.IsFolder ? -1 : 1)
            : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name) is var c && c != 0 ? c
            : StringComparer.Ordinal.Compare(a.Name, b.Name)
    );

    readonly Node root = new("", "", null);
    readonly Dictionary<string, Node> nodes = [];
    readonly HashSet<string> open = [];
    readonly List<Row> changed = []; // The changed files under their heading, then the tree's; none when none
    List<(Node Node, int Depth)> shown = [];
    List<Row>? rows;

    // changedHeading says what the changes are, e.g. 'Changed in commit 1be33bd1'
    public FileTree(IReadOnlyList<string> paths, IReadOnlyDictionary<string, char> changes, string changedHeading)
    {
        foreach (var path in paths)
            Add(path);
        Sort(root);

        List<Node> changedFiles = [];
        foreach (var (path, change) in changes)
        {
            if (!nodes.TryGetValue(path, out var file) || file.IsFolder)
                continue; // Not one of the files listed, e.g. one the commit deleted
            file.Change = change;
            changedFiles.Add(file);
            for (var folder = file.Parent; folder != root && folder != null; folder = folder.Parent)
                folder.HasChanges = true;
        }

        if (changedFiles.Count > 0)
        {
            changed.Add(Heading(changedHeading));
            changed.AddRange(
                changedFiles
                    .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(f => f.Path, StringComparer.Ordinal)
                    .Select(f => new Row(RowKind.Changed, f.Path, f.Path, 0, false, f.Change, false))
            );
            changed.Add(Heading(AllFilesHeading));
        }

        Update();
    }

    public IReadOnlyList<Row> Rows => rows ??= [.. changed, .. shown.Select(s => ToRow(s.Node, s.Depth))];

    // The rows there are with every folder open, i.e. every file and folder, and the changed files
    public int AllCount => changed.Count + nodes.Count;

    // Where the cursor starts: on the first changed file, or the first row when none
    public int First => changed.Count > 0 ? 1 : 0;

    // Where moving the cursor by the rows takes it, within them and past a heading
    public int Step(int index, int move)
    {
        var count = Rows.Count;
        if (count == 0)
            return 0;
        var to = Math.Clamp(index + move, 0, count - 1);
        var direction = move < 0 ? -1 : 1;
        for (var i = to; i >= 0 && i < count; i += direction)
        {
            if (!Rows[i].IsHeading)
                return i;
        }
        for (var i = to; i >= 0 && i < count; i -= direction)
        {
            if (!Rows[i].IsHeading)
                return i;
        }
        return index;
    }

    // What Enter does on a folder: opens or closes it, the cursor staying on it
    public void Toggle(int index)
    {
        if (At(index) is not Node folder || !folder.IsFolder)
            return;
        if (!open.Remove(folder.Path))
            open.Add(folder.Path);
        Update();
    }

    // What → does: opens a closed folder, or goes into an open one, to its first row. The row the
    // cursor goes to.
    public int Expand(int index)
    {
        if (At(index) is not Node folder || !folder.IsFolder)
            return index;
        if (!open.Add(folder.Path))
            return index + 1;
        Update();
        return index;
    }

    // What ← does: closes an open folder, or goes up to the folder of a file or of a closed folder.
    // The row the cursor goes to.
    public int Collapse(int index)
    {
        if (At(index) is not Node node)
            return index;
        if (node.IsFolder && open.Remove(node.Path))
        {
            Update();
            return index;
        }
        return node.Parent is Node parent && parent != root ? IndexOf(parent) : index;
    }

    // The node of a row of the tree, null for a heading or a changed file above it
    Node? At(int index) => index - changed.Count is var i && i >= 0 && i < shown.Count ? shown[i].Node : null;

    int IndexOf(Node node) => changed.Count + shown.FindIndex(s => s.Node == node);

    void Add(string path)
    {
        var parent = root;
        var parts = path.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            var nodePath = string.Join('/', parts, 0, i + 1);
            if (!nodes.TryGetValue(nodePath, out var node))
            {
                node = new Node(nodePath, parts[i], parent);
                nodes[nodePath] = node;
                parent.Children.Add(node);
            }
            parent = node;
        }
    }

    static void Sort(Node node)
    {
        node.Children.Sort(NameOrder);
        foreach (var child in node.Children)
            Sort(child);
    }

    // The rows of the tree: each folder's own after it while it is open
    void Update()
    {
        List<(Node, int)> rows = [];
        void AddRows(Node folder, int depth)
        {
            foreach (var child in folder.Children)
            {
                rows.Add((child, depth));
                if (child.IsFolder && open.Contains(child.Path))
                    AddRows(child, depth + 1);
            }
        }
        AddRows(root, 0);
        shown = rows;
        this.rows = null;
    }

    static Row Heading(string text) => new(RowKind.Heading, "", text, 0, false, ' ', false);

    Row ToRow(Node node, int depth) =>
        new(
            node.IsFolder ? RowKind.Folder : RowKind.File,
            node.Path,
            node.Name,
            depth,
            open.Contains(node.Path),
            node.Change,
            node.HasChanges
        );
}
