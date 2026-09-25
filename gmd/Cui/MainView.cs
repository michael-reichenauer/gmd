using gmd.Common;
using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
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
    readonly Config config;
    readonly ICloneDlg cloneDlg;
    readonly IInitRepoDlg initRepoDlg;
    readonly IHelpDlg helpDlg;
    readonly IServer server;
    readonly IProgress progress;
    readonly IAboutDlg aboutDlg;
    readonly IUpdater updater;
    readonly ISpellChecker spellChecker;
    readonly Lazy<View> toplevel;

    public MainView(
        IRepoView repoView,
        IGit git,
        Config config,
        ICloneDlg cloneDlg,
        IInitRepoDlg initRepoDlg,
        IHelpDlg helpDlg,
        IServer server,
        IProgress progress,
        IAboutDlg aboutDlg,
        IUpdater updater,
        ISpellChecker spellChecker,
        IStatusLine statusLine
    )
        : base()
    {
        Menu.StatusLine = statusLine;
        this.repoView = repoView;
        this.git = git;
        this.config = config;
        this.cloneDlg = cloneDlg;
        this.initRepoDlg = initRepoDlg;
        this.helpDlg = helpDlg;
        this.server = server;
        this.progress = progress;
        this.aboutDlg = aboutDlg;
        this.updater = updater;
        this.spellChecker = spellChecker;
        toplevel = new Lazy<View>(CreateView);
    }

    public View View => toplevel.Value;

    View CreateView()
    {
        // Adjust some global color schemes
        Colors.Dialog = ColorSchemes.Dialog;
        Colors.Error = ColorSchemes.ErrorDialog;
        Colors.Menu = ColorSchemes.Menu;

        var mainView = new MainViewWrapper(OnReady)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = ColorSchemes.Window,
        };

        // The key hints after the log view, so they are drawn after it in the same pass, which is
        // when the log view marks them for drawing
        mainView.Add(repoView.ApplicationBarView, repoView.View, repoView.DetailsView, repoView.KeyHintView);

        // Hidden until a repo is shown, which is when RepoView.UpdateLayout makes the layout from the
        // config: before that the start menu is all there is, and no key hinted would work there.
        // Here rather than in RepoView, whose constructor runs before there is a driver to hide it.
        repoView.KeyHintView.Visible = false;
        repoView.View.SetFocus();

        return mainView;
    }

    void OnReady()
    {
        Threading.SetUp();
        config.Init();
        spellChecker.WarmUp(); // Loads the dictionary, with the user's words from the config just read

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
        // path = "/workspaces/Dependinator-1";
        // path = "/workspaces/empty";
        // path = "/workspaces/empty2";

        var rootPathResult = git.RootPath(path);
        if (rootPathResult is not string rootPath || IsShowMainMenu)
        {
            if (path != "" && rootPathResult is Error e)
            { // User specified an invalid folder on command line
                UI.ErrorMessage($"Not a valid working folder:\n'{path}':\n{e.AllMessages()}");
            }

            ShowMainMenu();
            return;
        }

        ShowRepo(rootPath);
    }

    bool IsShowMainMenu => Environment.GetCommandLineArgs().Contains("-m");

    static string GetWorkingFolder()
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
        // Closing it quits, since it is all there is on screen, so only Esc and 'Quit' close it and a
        // click beside it is ignored
        // Titled with what it is for, since it is what gmd shows when it was started outside a
        // repository, which nothing else on the screen says
        Menu menu = new Menu(4, 2, "Open a Repository", null, -1, () => OnCancelMenu())
        {
            IsClosedOnClickOutside = false,
        };

        if (!config.Releases.IsUpdateAvailable())
        { // Check for update ...
            updater
                .CheckUpdateAvailableAsync()
                .ContinueWith(t =>
                {
                    UI.Post(async () =>
                    {
                        if (!config.Releases.IsUpdateAvailable())
                            return; // No update available

                        // Update is available, show menu again, which now will show the update available menu item
                        await menu.CloseAsync();
                        UI.Post(() => ShowMainMenu());
                    });
                });
        }

        menu.Show(
            Menu.Items.Items(GetNewReleaseItems())
                .Items(GetRecentRepoItems())
                .Separator()
                .Item("Browse ...", "", () => ShowBrowseDialog())
                .Item("Clone ...", "", () => ShowCloneDlg())
                .Item("Init ...", "", () => ShowInitRepoDlg())
                .Item("Help", "", () => ShowHelp())
                .Item("About", "", () => ShowAbout())
                .Item("Quit", "Esc ", () => Application.RequestStop())
        );
    }

    IEnumerable<MenuItem> GetNewReleaseItems()
    {
        if (!config.Releases.IsUpdateAvailable())
            return Menu.Items;

        return Menu
            .Items.Separator("New Release Available !!!")
            .Item("Update to Latest Version", "", () => UpdateRelease().RunInBackground())
            .Separator();
    }

    public async Task UpdateRelease()
    {
        var releases = config.Releases;
        var latest = Version.Parse(releases.LatestVersion);
        var typeText = releases.IsPreview ? "(preview)" : "";
        string msg =
            $"A new release is available.\n\n"
            + $"Current Version: {Build.Version().Txt()}\n"
            + $"Built:           {Build.Time().Iso()}\n\n"
            + $"New Version:     {latest.Txt()} {typeText}\n"
            + $"Built:           {Build.GetBuildTime(releases.LatestVersion).Iso()}\n\n"
            + "Do you want to update?";

        var button = UI.InfoMessage("New Release", msg, ["Yes", "No"]);
        if (button != 0)
        {
            Log.Info($"Skip update");
            ShowMainMenu();
            return;
        }

        Log.Info($"Updating release ...");
        using (progress.Show())
        {
            var updateTask = updater.UpdateAsync();
            UI.ShowMessageWhile(
                "Updating",
                $"Updating to version {latest.Txt()},\nthis might take a while ...",
                updateTask
            );
            if (await updateTask is Error e)
            {
                UI.ErrorMessage($"Failed to update:\n{e.AllMessages()}");
                ShowMainMenu();
                return;
            }
        }

        UI.InfoMessage("Restart Required", "A program restart is required,\nplease start Gmd again.");
        Application.RequestStop();
    }

    static void OnCancelMenu()
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

    // The repositories opened last, or a greyed out line saying there are none yet, rather than a
    // menu that starts with a bare separator
    IEnumerable<MenuItem> GetRecentRepoItems()
    {
        var items = config
            .RecentFolders.Where(Directory.Exists)
            .Select(path => new MenuItem(path, "", () => ShowRepo(path)))
            .Take(Config.MaxRecentFolders)
            .ToList();

        return items.Any() ? items : [Menu.Item("No recent repositories", "", () => { }, () => false)];
    }

    void ShowRepo(string path)
    {
        UI.RunInBackground(async () =>
        {
            if (await repoView.ShowInitialRepoAsync(path) is Error e)
            {
                UI.ErrorMessage($"Failed to load repo in:\n'{path}':\n{e.AllMessages()}");
                ShowMainMenu();
                return;
            }
            repoView.View.SetFocus();
        });
    }

    async void ShowCloneDlg()
    {
        if (cloneDlg.Show(config.ResentParentFolders()) is not CloneInfo clone)
        {
            ShowMainMenu();
            return;
        }

        Result cloned;
        using (progress.Show())
        {
            cloned = await server.CloneAsync(clone.Uri, clone.Path, "");
        }

        // Back to the start menu on a failure, the one way on from here, as for a repo that fails to
        // load. The progress is over by then, since the menu is modal and would keep it running.
        if (cloned is Error e)
        {
            UI.ErrorMessage($"Failed to clone:\n{clone.Uri}:\n{e.AllMessages()}");
            ShowMainMenu();
            return;
        }

        ShowRepo(clone.Path);
    }

    async void ShowInitRepoDlg()
    {
        var pathResult = initRepoDlg.Show(config.ResentParentFolders());
        if (pathResult is not string path)
        {
            ShowMainMenu();
            return;
        }

        Result initiated;
        using (progress.Show())
        {
            initiated = await server.InitRepoAsync(path, "");
        }

        // Back to the start menu on a failure, as for a failed clone above
        if (initiated is Error e)
        {
            UI.ErrorMessage($"Failed to init:\n{path}:\n{e.AllMessages()}");
            ShowMainMenu();
            return;
        }

        ShowRepo(path);
    }

    void ShowBrowseDialog()
    {
        var browser = new FolderBrowseDlg();
        if (browser.Show(config.ResentParentFolders()) is not string path)
        {
            ShowMainMenu();
            return;
        }

        ShowRepo(path);
    }

    // A workaround to get notifications once view is ready
    class MainViewWrapper : Toplevel
    {
        readonly Action ready;
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
