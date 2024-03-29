using gmd.Common;
using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

enum ApplicationBarItem
{
    Update,
    Gmd,
    Repo,
    CurrentBranch,
    Status,
    Behind,
    Ahead,
    Stash,
    Space,
    BranchName,
    Search,
    Help,
    Close,
}


interface IApplicationBar
{
    View View { get; }
    event Action<int, int, ApplicationBarItem> ItemClicked;
    void SetBranch(GraphBranch branch);
    void SetRepo(Server.Repo repo);
}


class ApplicationBar : View, IApplicationBar
{
    const int maxRepoLength = 30;
    readonly IBranchColorService branchColorService;
    readonly Config config;

    readonly UILabel label;
    readonly List<Text> items = new List<Text>();
    GraphBranch branch = null!;
    Rect bounds = Rect.Empty;

    public View View => this;

    public event Action<int, int, ApplicationBarItem>? ItemClicked = null;

    public ApplicationBar(IBranchColorService branchColorService, Config config)
    {
        this.branchColorService = branchColorService;
        this.config = config;
        X = 0;
        Y = 0;
        Height = 2;
        Width = Dim.Fill();

        label = new UILabel(0, 0);
        var border = new Label(0, 1, new string('─', 200)) { ColorScheme = ColorSchemes.Border };

        Add(label, border);

        label.MouseClick += OnLabelMouseClicked;
        bounds = Frame;

        // Initialize some text values 
        Enumerable.Range(0, Enum.GetNames(typeof(ApplicationBarItem)).Length)
            .ForEach(i => items.Add(Common.Text.Empty));
        items[(int)ApplicationBarItem.Gmd] = Common.Text.BrightMagenta(" Gmd ");
        items[(int)ApplicationBarItem.Space] = Common.Text.Empty;
        items[(int)ApplicationBarItem.Search] = Common.Text.Dark("[Ϙ Search]");
        items[(int)ApplicationBarItem.Help] = Common.Text.BrightCyan(" ? ");
        items[(int)ApplicationBarItem.Close] = Common.Text.White("X");

        UpdateView();

        UI.AddTimeout(TimeSpan.FromSeconds(5), () => UpdateView());
    }

    // Called when clicking on the label
    void OnLabelMouseClicked(MouseEventArgs e)
    {
        if (e.MouseEvent.Flags == MouseFlags.Button1Clicked) OnClicked(e.MouseEvent.X, e.MouseEvent.Y);
        e.Handled = false;
    }

    // Mouse events from the view not handled by the label mouse clicks
    public override bool MouseEvent(MouseEvent e)
    {
        if (e.Flags == MouseFlags.Button1Clicked) OnClicked(e.X, e.Y);
        return false;
    }

    // Search for the item that contains the clicked position
    void OnClicked(int x, int y)
    {
        int s = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var p = x + 1;
            var e = s + items[i].Length;
            if (e > s && p >= s && p <= e)  // Skipping empty texts and check if the click is within the text bounds
            {
                UI.Post(() => ItemClicked?.Invoke(x, y, (ApplicationBarItem)i));
                break;
            }
            s = e;
        }
    }

    // Monitors if the view has been resized
    public override void Redraw(Rect bounds)
    {
        if (bounds != this.bounds)
        {
            this.bounds = bounds;
            UpdateView();
        }

        base.Redraw(bounds);
    }


    public void SetRepo(Server.Repo repo)
    {
        var behindCount = repo.ViewCommits.Where(c => c.IsBehind).Count();
        var aheadCount = repo.ViewCommits.Where(c => c.IsAhead).Count();
        var stashCount = repo.Stashes.Count;

        items[(int)ApplicationBarItem.Repo] = GetRepoPath(repo);
        SetCurrentBranch(repo);
        items[(int)ApplicationBarItem.Status] = !repo.Status.IsOk ? Common.Text.Dark(", ").Yellow("©").Dark($"{repo.Status.ChangesCount}") : Common.Text.Empty;
        items[(int)ApplicationBarItem.Behind] = behindCount > 0 ? Common.Text.Dark(", ").BrightBlue("▼").Dark($"{behindCount}") : Common.Text.Empty;
        items[(int)ApplicationBarItem.Ahead] = aheadCount > 0 ? Common.Text.Dark(", ").Green("▲").Dark($"{aheadCount}") : Common.Text.Empty;
        items[(int)ApplicationBarItem.Stash] = stashCount > 0 ? Common.Text.Dark(", ").White("ß").Dark($"{stashCount}") : Common.Text.Empty;
        UpdateView();
    }


    public void SetBranch(GraphBranch branch)
    {
        if (this.branch == branch) return;
        this.branch = branch;

        items[(int)ApplicationBarItem.BranchName] = branch != null
            ? Common.Text.Color(branch.Color, $"({branch.B.NiceNameUnique}) ")
            : Common.Text.Empty;

        UpdateView();
    }


    void UpdateView()
    {
        items[(int)ApplicationBarItem.Update] = GetUpdateText();
        items[(int)ApplicationBarItem.Space] = GetSpace();

        label.Text = Common.Text.Add(items);
    }

    Text GetSpace()
    {
        items[(int)ApplicationBarItem.Space] = Common.Text.Empty;
        var count = items.Sum(t => t.Length);
        var space = new string(' ', Math.Max(0, bounds.Width - count - 1));
        return Common.Text.White(space);
    }

    static Text GetRepoPath(Server.Repo repo)
    {
        var path = repo.Path.Length <= maxRepoLength ? repo.Path
            : $"┅{repo.Path[^maxRepoLength..]}";
        return Common.Text.Dark($"{path}, ");
    }

    Text GetUpdateText() => config.Releases.IsUpdateAvailable()
        ? Common.Text.BrightGreen("⇓ ") : Common.Text.Empty;


    void SetCurrentBranch(Server.Repo repo)
    {
        var currentBranch = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {   // Current branch is shown
            var color = branchColorService.GetColor(repo, currentBranch);
            items[(int)ApplicationBarItem.CurrentBranch] = Common.Text.White("●").Color(color, currentBranch.NiceNameUnique);
        }
        else
        {   // No Current branch
            items[(int)ApplicationBarItem.CurrentBranch] = Common.Text.Empty;
        }
    }
}


