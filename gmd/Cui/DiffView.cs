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

    Content? diffRows = null;

    int TotalRows => diffRows?.RowCount ?? 0;

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

    private void onDrawRepoContent(Rect bounds, int firstIndex, int currentIndex)
    {
        if (diffRows == null)
        {
            return;
        }

        int firstRow = Math.Min(firstIndex, TotalRows);
        int rowCount = Math.Min(bounds.Height, TotalRows - firstRow);

        Rect contentRect = new Rect(10, firstRow, 10, rowCount);

        diffRows.Draw(contentView, bounds, contentRect);
    }


    public Toplevel View => diffView;
}
