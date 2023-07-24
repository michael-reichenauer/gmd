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
    Divider,
    BranchName,
    Space,
    Search,
    Help
}


interface IApplicationBarView
{
    View View { get; }
    event Action<int, int, ApplicationBarItem> ItemClicked;
    void SetBranch(GraphBranch branch);
    void SetRepo(Server.Repo repo);
}


class ApplicationBarView : View, IApplicationBarView
{
    const int maxRepoLength = 30;
    readonly IBranchColorService branchColorService;
    readonly IState state;

    UILabel label;
    string currentBranchName = "";
    GraphBranch branch = null!;
    Rect bounds = Rect.Empty;
    List<Text> texts = new List<Text>();

    public View View => this;

    public event Action<int, int, ApplicationBarItem>? ItemClicked = null;

    public ApplicationBarView(IBranchColorService branchColorService, IState state)
    {
        this.branchColorService = branchColorService;
        this.state = state;
        X = 0;
        Y = 0;
        Height = 2;
        Width = Dim.Fill();

        label = new UILabel(0, 0);
        var border = new Label(0, 1, new string('─', 200)) { ColorScheme = ColorSchemes.Border };

        Add(label, border);

        label.MouseClick += OnMouseClicked;
        bounds = Frame;

        // Initialize some text values 
        Enumerable.Range(0, Enum.GetNames(typeof(ApplicationBarItem)).Count())
            .ForEach(i => texts.Add(Common.Text.Empty));
        texts[(int)ApplicationBarItem.Gmd] = Common.Text.BrightMagenta("Gmd ");
        texts[(int)ApplicationBarItem.Divider] = Common.Text.BrightMagenta(" | ");
        texts[(int)ApplicationBarItem.Space] = Common.Text.Empty;
        texts[(int)ApplicationBarItem.Search] = Common.Text.Dark("|Ϙ Search|");
        texts[(int)ApplicationBarItem.Help] = Common.Text.BrightCyan(" ? ");

        UpdateView();

        UI.AddTimeout(TimeSpan.FromSeconds(5), () => UpdateView());
    }

    void OnMouseClicked(MouseEventArgs e)
    {
        if (e.MouseEvent.Flags == MouseFlags.Button1Clicked) OnClicked(e.MouseEvent.X, e.MouseEvent.Y);
        e.Handled = false;
    }

    public override bool MouseEvent(MouseEvent e)
    {
        if (e.Flags == MouseFlags.Button1Clicked) OnClicked(e.X, e.Y);
        return false;
    }

    // Search for the item that contains the clicked position
    void OnClicked(int x, int y)
    {
        int s = 0;
        for (int i = 0; i < texts.Count; i++)
        {
            var e = s + texts[i].Length;
            if (e > s && x >= s && x <= e)  // Skipping empty texts and check if the click is within the text bounds
            {
                ItemClicked?.Invoke(x, y, (ApplicationBarItem)i);
                break;
            }
            s = e;
        }
    }

    public override void Redraw(Rect bounds)
    {
        if (bounds != this.bounds)
        {
            Log.Info($"Redraw: {bounds}");
            this.bounds = bounds;
            UpdateView();
        }

        base.Redraw(bounds);
    }


    public void SetRepo(Server.Repo repo)
    {
        var behindCount = repo.Commits.Where(c => c.IsBehind).Count();
        var aheadCount = repo.Commits.Where(c => c.IsAhead).Count();

        texts[(int)ApplicationBarItem.Repo] = GetRepoPath(repo);
        SetCurrentBranch(repo);
        texts[(int)ApplicationBarItem.Status] = !repo.Status.IsOk ? Common.Text.Dark(", ").Yellow("© ").Dark($"{repo.Status.ChangesCount}") : Common.Text.Empty;
        texts[(int)ApplicationBarItem.Behind] = behindCount > 0 ? Common.Text.Dark(", ").BrightBlue("▼ ").Dark($"{behindCount}") : Common.Text.Empty;
        texts[(int)ApplicationBarItem.Ahead] = aheadCount > 0 ? Common.Text.Dark(", ").Green("▲ ").Dark($"{aheadCount}") : Common.Text.Empty;
        UpdateView();
    }


    public void SetBranch(GraphBranch branch)
    {
        if (this.branch == branch) return;
        this.branch = branch;

        texts[(int)ApplicationBarItem.BranchName] = branch != null
            ? Common.Text.Color(branch.Color, $"({branch.B.NiceNameUnique})")
            : Common.Text.Empty;

        UpdateView();
    }


    void UpdateView()
    {
        texts[(int)ApplicationBarItem.Update] = GetUpdateText();
        texts[(int)ApplicationBarItem.Space] = GetSpace();

        label.Text = Common.Text.Add(texts);
    }

    Text GetSpace()
    {
        texts[(int)ApplicationBarItem.Space] = Common.Text.Empty;
        var count = texts.Sum(t => t.Length);
        var space = new string(' ', Math.Max(0, bounds.Width - count));
        return Common.Text.White(space);
    }

    Text GetRepoPath(Server.Repo repo)
    {
        var path = repo.Path.Length <= maxRepoLength ? repo.Path
            : "┅" + repo.Path.Substring(repo.Path.Length - maxRepoLength);
        return Common.Text.Dark($"{path}, ");
    }

    Text GetUpdateText() => state.Get().Releases.IsUpdateAvailable()
        ? Common.Text.BrightGreen("⇓ ") : Common.Text.Empty;


    void SetCurrentBranch(Server.Repo repo)
    {
        var currentBranch = repo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {   // Current branch is shown
            var color = branchColorService.GetColor(repo, currentBranch);
            texts[(int)ApplicationBarItem.CurrentBranch] = Common.Text.White("● ").Color(color, currentBranch.NiceNameUnique);
            currentBranchName = currentBranch.NiceNameUnique;
        }
        else
        {   // Current branch not shown, lets show the current branch name anyway (color might be wrong)
            var cb = repo.AugmentedRepo.Branches.Values.First(b => b.IsCurrent);
            var color = branchColorService.GetBranchNameColor(cb.PrimaryBaseName);
            texts[(int)ApplicationBarItem.CurrentBranch] = Common.Text.White("● ").Color(color, cb.NiceNameUnique);
            currentBranchName = cb.NiceNameUnique;
        }
    }
}


