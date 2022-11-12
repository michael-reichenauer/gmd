using gmd.Git;
using Terminal.Gui;


namespace gmd.Cui;

interface IMainView
{
    View View { get; }
}

class MainView : IMainView
{
    readonly IRepoView repoView;
    readonly IGit git;
    readonly Lazy<Toplevel> toplevel;

    MainView(IRepoView repoView, IGit git) : base()
    {
        this.repoView = repoView;
        this.git = git;

        toplevel = new Lazy<Toplevel>(CreateView);
    }

    public View View => toplevel.Value;

    Toplevel CreateView()
    {
        Toplevel toplevel = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        toplevel.Add(repoView.View);

        Terminal.Gui.Colors.Dialog = Colors.DialogColorScheme;
        Terminal.Gui.Colors.Error = Colors.ErrorDialogColorScheme;

        toplevel.Loaded += () => OnLoaded().RunInBackground();
        return toplevel;
    }



    async Task OnLoaded()
    {
        // UI.AddTimeout(TimeSpan.FromMilliseconds(1000), (m) =>
        // {
        //     return true;
        // });
        // UI.ErrorMessage("Title", "Text");
        // return;

        string path = "";  // !!!!!!!!!!!! make selectable 

        if (!Try(out var e, await repoView.ShowRepoAsync(path)))
        {
            UI.ErrorMessage($"Failed to load repo in:\n'{path}':\n{e}");
            UI.Shutdown();
            return;
        }
    }

    public void ShowMainMenu()
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(new MenuItem("Commit ...", "", () => { }));


        var menu = new ContextMenu(View.Frame.Width / 2 - 10, 0, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

}