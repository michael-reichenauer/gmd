using gmd.ViewRepos;
using Terminal.Gui;


namespace gmd.Cui;

interface IMainView
{
    View View { get; }
}

class MainView : IMainView
{
    Toplevel toplevel;
    readonly IRepoView repoView;
    private readonly IViewRepoService viewRepoService;

    public View View => toplevel;

    MainView(IRepoView repoView, IViewRepoService viewRepoService) : base()
    {
        this.repoView = repoView;
        this.viewRepoService = viewRepoService;
        toplevel = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WantMousePositionReports = false,
        };

        toplevel.Add(repoView.View);

        toplevel.Loaded += () => OnLoaded().RunInBackground();
    }


    async Task OnLoaded()
    {
        string path = "";
        var repo = await viewRepoService.GetRepoAsync(path);
        if (repo.IsError)
        {
            UI.ErrorMessage("Error", $"Failed to load repo in:\n'{path}'");
            UI.Shutdown();
            return;
        }

        repoView.SetRepo(repo.Value);
    }
}