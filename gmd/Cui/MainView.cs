using gmd.Common;
using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Cui.Start;
using gmd.Git;
using Terminal.Gui;

namespace gmd.Cui;

interface IMainView
{
    View View { get; }
}

partial class MainView : IMainView
{
    readonly IRepoView repoView;
    readonly IStartView startView;
    readonly IGit git;
    readonly ISpellChecker spellChecker;
    readonly IAskpassServer askpassServer;
    readonly ILoginDlg loginDlg;
    readonly Lazy<View> toplevel;

    // The folder gmd was started in, which the start page names when it is not in a repository
    string startFolder = "";

    public MainView(
        IRepoView repoView,
        IStartView startView,
        IGit git,
        ISpellChecker spellChecker,
        IStatusLine statusLine,
        IAskpassServer askpassServer,
        ILoginDlg loginDlg
    )
        : base()
    {
        Menu.StatusLine = statusLine;
        this.repoView = repoView;
        this.startView = startView;
        this.git = git;
        this.spellChecker = spellChecker;
        this.askpassServer = askpassServer;
        this.loginDlg = loginDlg;
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

        // The start page in the place of the log, shown instead of it until a repo is. The key hints
        // after both, so they are drawn after them in the same pass, which is when either marks them
        // for drawing.
        mainView.Add(
            repoView.ApplicationBarView,
            repoView.View,
            startView.View,
            repoView.DetailsView,
            repoView.KeyHintView
        );

        // Hidden until a repo or the start page is shown, which is when RepoView.UpdateLayout makes the
        // layout from the config. Here rather than in the views, whose constructors run before there
        // is a driver to hide them with.
        repoView.KeyHintView.Visible = false;
        startView.View.Visible = false;
        repoView.View.SetFocus();

        // Until a repo is shown, the start page has the bar's clicks, after that the log view has
        repoView.ApplicationBar.ItemClicked += (_, _, item) =>
        {
            if (startView.View.Visible)
                startView.OnBarClicked(item);
        };

        return mainView;
    }

    void OnReady()
    {
        Threading.SetUp();
        spellChecker.WarmUp(); // Loads the dictionary, with the user's words from the config

        // From now on what git asks is asked in a dialog, before the first fetch of a repo is started
        askpassServer.Start(loginDlg.AskAsync);

        startFolder = GetStartFolder();
        repoView.SetStartPage(startView.View, startView.Hints);

        if (git.RootPath(startFolder) is string rootPath && !IsShowStartPage)
        {
            ShowRepo(rootPath);
            return;
        }

        ShowStartPage();
    }

    bool IsShowStartPage => Environment.GetCommandLineArgs().Contains("-m");

    // The folder given with '-d', or the current one. A folder that is not there is said, and the
    // current one is used instead, while one that is there but is no repository needs no box: the
    // start page says so. Read as the other options are, so '-d -m' is no folder named '-m'.
    static string GetStartFolder()
    {
        var folder = ProgramCommands.OptionValue(Environment.GetCommandLineArgs(), "-d");
        if (string.IsNullOrEmpty(folder))
            return Environment.CurrentDirectory;

        var path = Result.Catch(() => Path.GetFullPath(folder)) is string full ? full : folder;
        if (!Directory.Exists(path))
        {
            UI.ErrorMessage($"Not a valid working folder:\n'{folder}'");
            return Environment.CurrentDirectory;
        }

        return path;
    }

    // In the place of the log, which is hidden rather than covered: a hidden view is not drawn, while
    // a covered one would clear what is drawn over it whenever it is drawn itself
    void ShowStartPage()
    {
        Log.Info($"Show start page in '{startFolder}'");
        repoView.View.Visible = false;
        startView.Show(startFolder, ShowRepo, repoView.KeyHintView);
        repoView.UpdateLayout();
    }

    void ShowRepo(string path)
    {
        UI.RunInBackground(async () =>
        {
            if (await repoView.ShowInitialRepoAsync(path) is Error e)
            {
                UI.ErrorMessage($"Failed to load repo in:\n'{path}':\n{e.AllMessages()}");
                ShowStartPage();
                return;
            }

            // The log visible before it is focused, which a hidden view cannot be, and the start
            // page hidden after, so the focus is never on a hidden view
            repoView.View.Visible = true;
            repoView.View.SetFocus();
            startView.Hide();
            repoView.UpdateLayout();
        });
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
