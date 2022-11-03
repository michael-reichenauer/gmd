using gmd.Utils.Git;
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

    public View View => toplevel;

    MainView(IRepoView repoView) : base()
    {
        this.repoView = repoView;

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

        var result = await repoView.ShowRepoAsync(path);
        if (result.IsError)
        {
            UI.ErrorMessage("Error", $"Failed to load repo in:\n'{path}'");
            UI.Shutdown();
            return;
        }
    }
}