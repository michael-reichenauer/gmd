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
        };

        toplevel.Add(repoView.View);

        toplevel.Loaded += () => OnLoaded().RunInBackground();
    }


    async Task OnLoaded()
    {
        UI.AddTimeout(TimeSpan.FromMilliseconds(1000), (m) =>
        {
            return true;
        });

        string path = "";  // !!!!!!!!!!!! make selectable 

        if (!Try(out var e, await repoView.ShowRepoAsync(path)))
        {
            UI.ErrorMessage($"Failed to load repo in:\n'{path}':\n{e}");
            UI.Shutdown();
            return;
        }
    }
}