using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Commits;

interface ICommitDetailsView
{
    ContentView View { get; }

    void Set(Repo repo, Commit commit, Branch branch);
    void SetRows(IReadOnlyList<Text> rows);
}

// The pane below the log, and the blame, with the details of the commit on the cursor's row. Its
// rows are built at once, and the files the commit changed are added below them once read, which
// takes a git command, see ShowChangedFilesAsync.
class CommitDetailsView : ICommitDetailsView
{
    internal static readonly int ContentHeight = 11;

    // How long the cursor is to rest on a commit before its files are read, so that holding a
    // cursor key down the log runs no git command for each row it passes
    static readonly TimeSpan ReadFilesDelay = TimeSpan.FromMilliseconds(150);

    // A commit's files never change, so they are kept once read, for this many commits
    const int MaxCachedCommits = 200;

    readonly ContentView contentView;
    readonly IBranchColorService branchColorService;
    readonly IServer server;
    readonly Dictionary<(string Path, string Id), IReadOnlyList<ChangedFile>> filesByCommit = [];
    IReadOnlyList<Text> rows = [];

    // The commit shown, of which repo, so that files read for another are not shown for it
    (string Path, string Id) shown = ("", "");

    public CommitDetailsView(IBranchColorService branchColorService, IServer server)
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
        this.server = server;
    }

    public ContentView View => contentView;

    public void Set(Repo repo, Server.Commit commit, Branch branch)
    {
        shown = (repo.Path, commit.Id);
        filesByCommit.TryGetValue(shown, out var files);
        ShowRows(CommitDetailsRows.Of(repo, commit, branch, branchColorService, files), isMoveToTop: true);

        if (files == null && CommitDetailsRows.HasChangedFiles(commit))
            ShowChangedFilesAsync(repo, commit, branch).RunInBackground();
    }

    // For a caller that builds its own rows, i.e. has no Server.Commit to render. The blame view
    // uses this for a commit that is not in the shown log, where all it has is what git blame said.
    public void SetRows(IReadOnlyList<Text> rows)
    {
        shown = ("", "");
        ShowRows(rows, isMoveToTop: true);
    }

    // Reads the files once the cursor has rested on the commit, and adds them below the rest of its
    // details if it is still the one shown. Neither await leaves the UI thread, since Terminal.Gui
    // posts what follows an await back to its main loop. The rows are not scrolled back to the top,
    // since the user may have scrolled them while the files were read.
    async Task ShowChangedFilesAsync(Repo repo, Server.Commit commit, Branch branch)
    {
        var key = (repo.Path, commit.Id);
        await Task.Delay(ReadFilesDelay);
        if (shown != key)
            return; // The cursor moved on

        var result = await server.GetChangedFilesAsync(commit.Id, repo.Path);
        if (result is not IReadOnlyList<ChangedFile> files)
        {
            Log.Warn($"Failed to read the files of {commit.Sid}: {result.Error.AllMessages()}");
            return;
        }

        if (filesByCommit.Count >= MaxCachedCommits)
            filesByCommit.Clear();
        filesByCommit[key] = files;

        if (shown == key)
            ShowRows(CommitDetailsRows.Of(repo, commit, branch, branchColorService, files), isMoveToTop: false);
    }

    void ShowRows(IReadOnlyList<Text> rows, bool isMoveToTop)
    {
        this.rows = rows;
        contentView.SetNeedsDraw();
        if (isMoveToTop)
            contentView.MoveToTop();
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width) =>
        (rows.Skip(firstIndex).Take(count), rows.Count);
}
