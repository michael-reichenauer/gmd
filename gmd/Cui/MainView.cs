using gmd.Common;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IMainView
{
    View View { get; }
}

partial class MainView : IMainView
{
    readonly IRepoView repoView;
    readonly IGit git;
    readonly IState states;
    readonly ICloneDlg cloneDlg;
    readonly IHelpDlg helpDlg;
    readonly IServer server;
    readonly IProgress progress;
    readonly IAboutDlg aboutDlg;
    readonly Lazy<View> toplevel;

    MainView(
        IRepoView repoView,
        IGit git,
        IState states,
        ICloneDlg cloneDlg,
        IHelpDlg helpDlg,
        IServer server,
        IProgress progress,
        IAboutDlg aboutDlg) : base()
    {
        this.repoView = repoView;
        this.git = git;
        this.states = states;
        this.cloneDlg = cloneDlg;
        this.helpDlg = helpDlg;
        this.server = server;
        this.progress = progress;
        this.aboutDlg = aboutDlg;
        toplevel = new Lazy<View>(CreateView);
    }

    public View View => toplevel.Value;

    View CreateView()
    {
        // Adjust some global color schemes
        Terminal.Gui.Colors.Dialog = ColorSchemes.Dialog;
        Terminal.Gui.Colors.Error = ColorSchemes.ErrorDialog;
        Terminal.Gui.Colors.Menu = ColorSchemes.Menu;

        var mainView = new MainViewWrapper(OnReady) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        mainView.ColorScheme = ColorSchemes.Window;

        mainView.Add(repoView.View, repoView.DetailsView);
        repoView.View.SetFocus();

        return mainView;
    }


    void OnReady()
    {
        Threading.SetUp();

        string path = GetWorkingFolder();
        // path = "/workspaces/Terminal.Gui";
        // path = "/workspaces/gt2";
        // path = "/workspaces/Dependitor";
        // path = "/workspaces/gmd/tmp/Dependitor";

        if (!Try(out var rootPath, out var e, git.RootPath(path)))
        {
            if (path != "")
            {
                // User specified an invalid folder on command line
                UI.ErrorMessage($"Not a valid working folder:\n'{path}':\n{e}");
            }

            ShowMainMenu();
            return;
        }

        ShowRepo(rootPath);
    }

    string GetWorkingFolder()
    {
        var path = "";
        var args = Environment.GetCommandLineArgs();
        int i = args.ToList().FindIndex(n => n == "-d");
        if (i != -1 && args.Length >= i + 2)
        {
            path = args[i + 1];
        }
        return path;
    }


    void ShowMainMenu()
    {
        List<MenuItem> items = new List<MenuItem>();
        items.Add(UI.MenuSeparator("Open Repo"));
        items.AddRange(GetRecentRepoItems());

        if (items.Any())
        {
            items.Add(UI.MenuSeparator());
        }

        items.Add(new MenuItem("Browse ...", "", ShowBrowseDialog));
        items.Add(new MenuItem("Clone ...", "", () => Clone()));
        items.Add(new MenuItem("Help ...", "", () => helpDlg.Show()));
        items.Add(new MenuItem("About ...", "", () => aboutDlg.Show()));
        items.Add(new MenuItem("Quit", "Esc ", () => Application.RequestStop()));

        var menu = new ContextMenu(4, 0, new MenuBarItem(items.ToArray()));
        menu.Show();
    }


    MenuItem[] GetRecentRepoItems() =>
        states.Get().RecentFolders
            .Where(Files.DirExists)
            .Select(path => new MenuItem(path, "", () => ShowRepo(path)))
            .ToArray();


    void ShowRepo(string path)
    {
        UI.RunInBackground(async () =>
        {
            if (!Try(out var e, await repoView.ShowInitialRepoAsync(path)))
            {
                UI.ErrorMessage($"Failed to load repo in:\n'{path}':\n{e}");
                ShowMainMenu();
                return;
            }
            repoView.View.SetFocus();
        });
    }

    async void Clone()
    {
        // Parent folders to recent work folders, usually other repos there as well
        var recentFolders = states.Get().RecentParentFolders.Where(Files.DirExists).ToList();
        if (!Try(out var r, out var e, cloneDlg.Show(recentFolders))) return;
        (var uri, var path) = r;

        using (progress.Show())
        {
            if (!Try(out e, await server.CloneAsync(uri, path, "")))
            {
                UI.ErrorMessage($"Failed to clone:\n{uri}:\n{e}");
                return;
            }
        }

        ShowRepo(path);
    }



    void ShowBrowseDialog()
    {
        // Parent folders to recent work folders, usually other repos there as well
        var recentFolders = states.Get().RecentParentFolders.Where(Files.DirExists).ToList();

        var browser = new FolderBrowseDlg();
        if (!Try(out var path, browser.Show(recentFolders)))
        {
            ShowMainMenu();
            return;
        }

        ShowRepo(path);
    }


    // A workaround to get notifications once view is ready
    class MainViewWrapper : Toplevel
    {
        Action ready;
        bool hasCalledReady;

        public MainViewWrapper(Action ready)
        {
            this.ready = ready;
        }

        public override void Redraw(Rect bounds)
        {
            base.Redraw(bounds);

            if (!hasCalledReady)
            {
                hasCalledReady = true;
                UI.Post(() => ready());
            }
        }
    }
}