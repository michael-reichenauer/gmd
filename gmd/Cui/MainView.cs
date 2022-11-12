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

        // Terminal.Gui.Colors.Error.Normal = Colors.Red;
        // Terminal.Gui.Colors.Error.Focus = Colors.White;
        // Terminal.Gui.Colors.Error.HotNormal = Colors.Green;
        // Terminal.Gui.Colors.Error.HotFocus = Colors.White;
        // Terminal.Gui.Colors.Error.Disabled = Colors.Dark;

        // Terminal.Gui.Colors.Dialog.Normal = View.Driver.MakeAttribute(Color.Black, Color.Gray);
        // Terminal.Gui.Colors.Dialog.Focus = View.Driver.MakeAttribute(Color.White, Color.DarkGray);
        // Terminal.Gui.Colors.Dialog.HotNormal = View.Driver.MakeAttribute(Color.Blue, Color.Gray);
        // Terminal.Gui.Colors.Dialog.HotFocus = View.Driver.MakeAttribute(Color.BrightYellow, Color.DarkGray);
        // Terminal.Gui.Colors.Dialog.Disabled = View.Driver.MakeAttribute(Color.Gray, Color.DarkGray);


        // Terminal.Gui.Colors.Dialog.Normal = Colors.Blue;
        // Terminal.Gui.Colors.Dialog.Focus = Colors.Green;
        // Terminal.Gui.Colors.Dialog.HotNormal = Colors.Green;
        // Terminal.Gui.Colors.Dialog.HotFocus = Colors.Yellow;
        // Terminal.Gui.Colors.Dialog.Disabled = Colors.Dark;

        toplevel.Loaded += () => OnLoaded().RunInBackground();
        return toplevel;
    }

    // public void CreateColors (bool hasColors = true)
    // 	{
    // 		Colors.TopLevel = new ColorScheme ();
    // 		Colors.Base = new ColorScheme ();
    // 		Colors.Dialog = new ColorScheme ();
    // 		Colors.Menu = new ColorScheme ();
    // 		Colors.Error = new ColorScheme ();

    // 		if (!hasColors) {
    // 			return;
    // 		}

    // 		Colors.TopLevel.Normal = MakeColor (Color.BrightGreen, Color.Black);
    // 		Colors.TopLevel.Focus = MakeColor (Color.White, Color.Cyan);
    // 		Colors.TopLevel.HotNormal = MakeColor (Color.Brown, Color.Black);
    // 		Colors.TopLevel.HotFocus = MakeColor (Color.Blue, Color.Cyan);
    // 		Colors.TopLevel.Disabled = MakeColor (Color.DarkGray, Color.Black);

    // 		Colors.Base.Normal = MakeColor (Color.White, Color.Blue);
    // 		Colors.Base.Focus = MakeColor (Color.Black, Color.Gray);
    // 		Colors.Base.HotNormal = MakeColor (Color.BrightCyan, Color.Blue);
    // 		Colors.Base.HotFocus = MakeColor (Color.BrightBlue, Color.Gray);
    // 		Colors.Base.Disabled = MakeColor (Color.DarkGray, Color.Blue);

    // 		Colors.Dialog.Normal = MakeColor (Color.Black, Color.Gray);
    // 		Colors.Dialog.Focus = MakeColor (Color.White, Color.DarkGray);
    // 		Colors.Dialog.HotNormal = MakeColor (Color.Blue, Color.Gray);
    // 		Colors.Dialog.HotFocus = MakeColor (Color.BrightYellow, Color.DarkGray);
    // 		Colors.Dialog.Disabled = MakeColor (Color.Gray, Color.DarkGray);

    // 		Colors.Menu.Normal = MakeColor (Color.White, Color.DarkGray);
    // 		Colors.Menu.Focus = MakeColor (Color.White, Color.Black);
    // 		Colors.Menu.HotNormal = MakeColor (Color.BrightYellow, Color.DarkGray);
    // 		Colors.Menu.HotFocus = MakeColor (Color.BrightYellow, Color.Black);
    // 		Colors.Menu.Disabled = MakeColor (Color.Gray, Color.DarkGray);

    // 		Colors.Error.Normal = MakeColor (Color.Red, Color.White);
    // 		Colors.Error.Focus = MakeColor (Color.Black, Color.BrightRed);
    // 		Colors.Error.HotNormal = MakeColor (Color.Black, Color.White);
    // 		Colors.Error.HotFocus = MakeColor (Color.BrightRed, Color.Gray);
    // 		Colors.Error.Disabled = MakeColor (Color.DarkGray, Color.White);
    // 	}

    async Task OnLoaded()
    {
        // UI.AddTimeout(TimeSpan.FromMilliseconds(1000), (m) =>
        // {
        //     return true;
        // });
        //UI.InfoMessage("Title", "Text");
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