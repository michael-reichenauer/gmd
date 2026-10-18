namespace gmd.Cui.Common;

// The files of a commit as a tree of folders, which the file browser shows while nothing is typed:
// which folders are open, the rows that makes, and where the arrow keys go. Folders come before
// files, each in name order, case ignored, as a file manager lists them. The files the commit
// changed have the letter of their change, their folders say they hold one, and those folders are
// open as the tree is made, so the files most likely wanted are in sight. Plain, with no view, so it
// is unit testable.
class FileTree
{
    // A row: a folder or a file, how deep it is, whether a folder is open, and the letter of a
    // file's change in the commit (' ' for none), or for a folder whether it holds a changed file
    public record Row(string Path, string Name, int Depth, bool IsFolder, bool IsOpen, char Change, bool HasChanges);

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

    static readonly IComparer<Node> NameOrder = Comparer<Node>.Create(
        (a, b) =>
            a.IsFolder != b.IsFolder ? (a.IsFolder ? -1 : 1)
            : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name) is var c && c != 0 ? c
            : StringComparer.Ordinal.Compare(a.Name, b.Name)
    );

    readonly Node root = new("", "", null);
    readonly Dictionary<string, Node> nodes = [];
    readonly HashSet<string> open = [];
    List<(Node Node, int Depth)> shown = [];
    List<Row>? rows;

    public FileTree(IReadOnlyList<string> paths, IReadOnlyDictionary<string, char> changes)
    {
        foreach (var path in paths)
            Add(path);
        Sort(root);

        foreach (var (path, change) in changes)
        {
            if (!nodes.TryGetValue(path, out var file) || file.IsFolder)
                continue; // Not one of the files listed, e.g. one the commit deleted
            file.Change = change;
            for (var folder = file.Parent; folder != root && folder != null; folder = folder.Parent)
            {
                folder.HasChanges = true;
                open.Add(folder.Path);
            }
        }

        Update();
    }

    public IReadOnlyList<Row> Rows => rows ??= shown.Select(s => ToRow(s.Node, s.Depth)).ToList();

    // The rows there are with every folder open, i.e. every file and folder
    public int AllCount => nodes.Count;

    // The first file the commit changed, where the cursor starts, or the first row when it changed none
    public int FirstChanged => Math.Max(0, shown.FindIndex(s => !s.Node.IsFolder && s.Node.Change != ' '));

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

    Node? At(int index) => index >= 0 && index < shown.Count ? shown[index].Node : null;

    int IndexOf(Node node) => shown.FindIndex(s => s.Node == node);

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

    // The rows shown: each folder's own after it while it is open
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

    Row ToRow(Node node, int depth) =>
        new(node.Path, node.Name, depth, node.IsFolder, open.Contains(node.Path), node.Change, node.HasChanges);
}
