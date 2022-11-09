using gmd.ViewRepos;
using Terminal.Gui;

namespace gmd.Cui;

interface IDiffView
{
    Toplevel View { get; }

    void Show(Repo repo, string commitId);
}


class DiffView : IDiffView
{
    private readonly IViewRepoService viewRepoService;
    private readonly IDiffService diffService;

    public Toplevel diffView { get; }

    readonly ContentView contentView;
    readonly DiffWriter diffWriter;

    DiffRows? diffRows = null;

    int TotalRows => diffRows?.Rows.Count ?? 0;

    public DiffView(IViewRepoService viewRepoService, IDiffService diffService)
    {
        diffView = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        diffView.Add(contentView);
        diffWriter = new DiffWriter(contentView, contentView.ContentX);
        this.viewRepoService = viewRepoService;
        this.diffService = diffService;
    }

    public void Show(Repo repo, string commitId)
    {
        Log.Info($"Show '{commitId}'");
        ShowAsync(repo, commitId).RunInBackground();
        Application.Run(diffView);
    }

    async Task ShowAsync(Repo repo, string commitId)
    {
        Log.Info($"Show '{commitId}'");
        if (commitId == Repo.UncommittedId)
        {
            Log.Info($"Show diff for '{commitId}'");
            var diff = await viewRepoService.GetUncommittedDiff(repo);
            if (diff.IsError)
            {
                UI.ErrorMessage($"Failed to get uncommitted diff:\n{diff.Error.Message}");
                return;
            }

            diffRows = diffService.CreateRows(diff.Value);
            View.SetNeedsDisplay();
        }
    }

    private void onDrawRepoContent(int width, int Height, int firstIndex, int currentIndex)
    {
        if (diffRows == null)
        {
            return;
        }

        int firstCommit = Math.Min(firstIndex, TotalRows);
        int commitCount = Math.Min(Height, TotalRows - firstCommit);

        diffWriter.WriteDiffPage(diffRows, width, firstIndex, Height, currentIndex);
    }


    public Toplevel View => diffView;
}
