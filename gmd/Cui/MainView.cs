using gmd.Common;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Server;
using Terminal.Gui;
using MenuItem = gmd.Cui.Common.MenuItem;

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
        // string path = "/lkwjlkj";
        path = "/workspaces/Terminal.Gui";
        // path = "/workspaces/gt2";
        // path = "/workspaces/Dependitor";
        // path = "/workspaces/gmd/tmp/Dependitor";
        // path = "/workspaces/GitMind";
        // path = "/workspaces/kal kl/gmd-3";
        //  path = "/workspaces/gmd-1";           // ########### Disabled for now

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
        Menu.Show(4, 0, Menu.NewItems
            .AddSeparator("Recent Repos")
            .Add(GetRecentRepoItems())
            .AddSeparator()
            .AddItem("Browse ...", "", () => ShowBrowseDialog())
            .AddItem("Clone ...", "", () => Clone())
            .AddItem("Help ...", "", () => ShowHelp())
            .AddItem("About ...", "", () => ShowAbout())
            .AddItem("Quit", "Esc ", () => Application.RequestStop()));
    }


    private void ShowAbout()
    {
        aboutDlg.Show();
        ShowMainMenu();
    }

    private void ShowHelp()
    {
        helpDlg.Show();
        ShowMainMenu();
    }

    IEnumerable<MenuItem> GetRecentRepoItems() =>
        states.Get().RecentFolders
            .Where(Files.DirExists)
            .Select(path => new MenuItem(path, "", () => ShowRepo(path)))
            .Take(8);


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
        var recentParentFolders = states.Get().RecentParentFolders.Where(Files.DirExists).ToList();
        if (!Try(out var r, out var e, cloneDlg.Show(recentParentFolders)))
        {
            ShowMainMenu();
            return;
        }

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