using gmd.Common;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Installation;
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
    readonly IUpdater updater;
    readonly Lazy<View> toplevel;

    MainView(
        IRepoView repoView,
        IGit git,
        IState states,
        ICloneDlg cloneDlg,
        IHelpDlg helpDlg,
        IServer server,
        IProgress progress,
        IAboutDlg aboutDlg,
        IUpdater updater) : base()
    {
        this.repoView = repoView;
        this.git = git;
        this.states = states;
        this.cloneDlg = cloneDlg;
        this.helpDlg = helpDlg;
        this.server = server;
        this.progress = progress;
        this.aboutDlg = aboutDlg;
        this.updater = updater;
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

        mainView.Add(repoView.ApplicationBarView, repoView.View, repoView.DetailsView);
        repoView.View.SetFocus();

        return mainView;
    }


    void OnReady()
    {
        Threading.SetUp();

        string path = GetWorkingFolder();
        // Environment.CurrentDirectory = "/workspaces";
        // path = "/NoExistFolder";
        // path = "/workspaces/Terminal.Gui";
        // path = "/workspaces/gt2";
        // path = "/workspaces/Dependitor";
        // path = "/workspaces/gmd/tmp/Dependitor";
        // path = "/workspaces/GitMind";
        // path = "/workspaces/kal kl/gmd-3";
        // path = "/workspaces/gmd-1";  
        // path = "/workspaces/vscode";

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
        Log.Info("Show main menu");
        Menu menu = new Menu(4, 2, "Recent Repos", null, -1, () => OnCancelMenu());

        if (!states.Get().Releases.IsUpdateAvailable())
        {   // Check for update ...
            updater.CheckUpdateAvailableAsync().ContinueWith(t =>
            {
                UI.Post(async () =>
                {
                    if (!states.Get().Releases.IsUpdateAvailable()) return;  // No update available

                    // Update is available, show menu again, which now will show the update available menu item
                    await menu.CloseAsync();
                    UI.Post(() => ShowMainMenu());
                });
            });
        }

        menu.Show(Menu.Items
            .Items(GetNewReleaseItems())
            .Items(GetRecentRepoItems())
            .Separator()
            .Item("Browse ...", "", () => ShowBrowseDialog())
            .Item("Help ...", "", () => ShowHelp())
            .Item("About ...", "", () => ShowAbout())
            .Item("Quit", "Esc ", () => Application.RequestStop()));
    }


    IEnumerable<MenuItem> GetNewReleaseItems()
    {
        if (!states.Get().Releases.IsUpdateAvailable()) return Menu.Items;

        return Menu.Items
           .Separator("New Release Available !!!")
           .Item("Update to Latest Version ...", "", () => UpdateRelease().RunInBackground())
           .Separator();
    }


    public async Task UpdateRelease()
    {
        var releases = states.Get().Releases;
        var typeText = releases.IsPreview ? "(preview)" : "";
        string msg = $"A new release is available:\n" +
            $"New Version:     {releases.LatestVersion} {typeText}\n" +
            $"\nCurrent Version: {Build.Version()}\n\n" +
            "Do you want to update?";

        var button = UI.InfoMessage("New Release", msg, new[] { "Yes", "No" });
        if (button != 0)
        {
            Log.Info($"Skip update");
            ShowMainMenu();
            return;
        }

        Log.Info($"Updating release ...");
        using (progress.Show())
        {
            if (!Try(out var _, out var e, await updater.UpdateAsync()))
            {
                UI.ErrorMessage($"Failed to update:\n{e}");
                ShowMainMenu();
                return;
            }
        }

        UI.InfoMessage("Restart Required", "A program restart is required,\nplease start Gmd again.");
        Application.RequestStop();
    }


    void OnCancelMenu()
    {
        Log.Info("Cancel menu");
        Application.RequestStop();
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