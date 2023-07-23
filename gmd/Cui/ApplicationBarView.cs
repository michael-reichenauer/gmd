using gmd.Common;
using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

interface IApplicationBarView
{
    View View { get; }

    void SetBranch(GraphBranch branch);
    void SetRepo(Server.Repo repo);
}

class ApplicationBarView : View, IApplicationBarView
{
    const int maxRepoLength = 30;
    readonly IBranchColorService branchColorService;
    readonly IState state;

    UILabel label;
    Text repoText = Common.Text.Empty;
    Text branchText = Common.Text.Empty;
    string currentBranchName = "";
    GraphBranch branch = null!;

    public View View => this;

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
        UpdateView();

        UI.AddTimeout(TimeSpan.FromSeconds(5), () => UpdateView());
    }

    public void SetRepo(Server.Repo repo)
    {
        var behindCount = repo.Commits.Where(c => c.IsBehind).Count();
        var aheadCount = repo.Commits.Where(c => c.IsAhead).Count();
        var path = repo.Path;

        if (path.Length > maxRepoLength) path = "┅" + path.Substring(path.Length - maxRepoLength);
        var text = Common.Text.Dark($"{path}, ");

        AddCurrentBranch(text, repo);
        if (!repo.Status.IsOk) text.Dark(", ").Yellow("* ").Dark($"{repo.Status.ChangesCount}");
        if (behindCount > 0) text.Dark(", ").BrightBlue("▼ ").Dark($"{behindCount}");
        if (aheadCount > 0) text.Dark(", ").Green("▲ ").Dark($"{aheadCount}");

        repoText = text;
        UpdateView();
    }


    public void SetBranch(GraphBranch branch)
    {
        if (this.branch == branch) return;
        this.branch = branch;

        branchText = branch != null && branch.B.NiceNameUnique != currentBranchName
            ? Common.Text.BrightMagenta(" | ").Color(branch.Color, branch.B.NiceNameUnique)
            : Common.Text.BrightMagenta(" | ").Dark("");

        UpdateView();
    }


    void UpdateView()
    {
        var updateText = state.Get().Releases.IsUpdateAvailable()
            ? Common.Text.BrightGreen("⇓ ") : Common.Text.Empty;
        label.Text = Common.Text.Add(updateText).BrightMagenta("Gmd ").Add(repoText).Add(branchText);
    }


    void AddCurrentBranch(TextBuilder text, Server.Repo repo)
    {
        var currentBranch = repo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {   // Current branch is shown
            var color = branchColorService.GetColor(repo, currentBranch);
            text.White("● ").Color(color, currentBranch.NiceNameUnique);
            currentBranchName = currentBranch.NiceNameUnique;
        }
        else
        {   // Current branch not shown, lets show the current branch name anyway (color might be wrong)
            var cb = repo.AugmentedRepo.Branches.Values.First(b => b.IsCurrent);
            var color = branchColorService.GetBranchNameColor(cb.PrimaryBaseName);
            text.White("● ").Color(color, cb.NiceNameUnique);
            currentBranchName = cb.NiceNameUnique;
        }
    }
}


