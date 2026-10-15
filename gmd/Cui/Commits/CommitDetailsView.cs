using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.Commits;

interface ICommitDetailsView
{
    ContentView View { get; }

    void Set(Repo repo, Commit commit, Branch branch);
    void SetRows(IReadOnlyList<Text> rows);
}

class CommitDetailsView : ICommitDetailsView
{
    internal static readonly int ContentHeight = 11;

    readonly ContentView contentView;
    readonly IBranchColorService branchColorService;
    IReadOnlyList<Text> rows = [];

    public CommitDetailsView(IBranchColorService branchColorService)
    {
        contentView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = Pos.AnchorEnd(ContentHeight),
            Height = 0,
            Width = Dim.Fill(),
            IsTopBorder = true,
            IsShowCursor = false,
            IsScrollMode = true,
            IsFocus = false,
            IsKeysForwarded = true, // By the log, and by the blame
        };

        this.branchColorService = branchColorService;
    }

    public ContentView View => contentView;

    public void Set(Repo repo, Server.Commit commit, Branch branch) =>
        UI.RunInBackground(async () =>
        {
            await Task.Yield();
            SetRows(CommitDetailsRows.Of(repo, commit, branch, branchColorService));
        });

    // For a caller that builds its own rows, i.e. has no Server.Commit to render. The blame view
    // uses this for a commit that is not in the shown log, where all it has is what git blame said.
    public void SetRows(IReadOnlyList<Text> rows)
    {
        this.rows = rows;
        contentView.SetNeedsDisplay();
        contentView.MoveToTop();
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width) =>
        (rows.Skip(firstIndex).Take(count), rows.Count);
}
