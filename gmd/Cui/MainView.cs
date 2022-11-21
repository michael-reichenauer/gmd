using gmd.Common;
using gmd.Cui.Common;
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
    readonly IGit git;
    private readonly IStates states;
    readonly Lazy<View> toplevel;

    MainView(IRepoView repoView, IGit git, IStates states) : base()
    {
        this.repoView = repoView;
        this.git = git;
        this.states = states;
        toplevel = new Lazy<View>(CreateView);
    }

    public View View => toplevel.Value;

    View CreateView()
    {
        // Adjust some global color schemes
        Terminal.Gui.Colors.Dialog = ColorSchemes.DialogColorScheme;
        Terminal.Gui.Colors.Error = ColorSchemes.ErrorDialogColorScheme;
        Terminal.Gui.Colors.Menu = ColorSchemes.MenuColorScheme;

        var mainView = new MainViewWrapper(OnReady) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        mainView.ColorScheme = ColorSchemes.WindowColorScheme;

        mainView.Add(repoView.View, repoView.DetailsView);
        repoView.View.SetFocus();


        //Application.Current.Added += (v) => Log.Info($"View added {v}");
        //     Application.Current.Removed += (v) =>
        //    {
        //        Log.Info($"View removed {v}");
        //    };

        return mainView;
    }


    void OnReady()
    {
        Threading.SetUp();
        // UI.AddTimeout(TimeSpan.FromMilliseconds(1000), (f) =>
        // {
        //     Log.Info("Ui callback");
        //     return true;
        // });

        string path = "";
        if (!Try(out var rootPath, out var e, git.RootPath(path)))
        {
            if (path != "")
            {
                // User specified an invalid folder
                UI.ErrorMessage($"Not a valid working folder:\n'{path}':\n{e}");
            }

            ShowMainMenu();
            return;
        }

        ShowRepo(rootPath);
    }


    void ShowMainMenu()
    {
        List<MenuItem> items = new List<MenuItem>();
        items.Add(UI.MenuSeparator("Open Repo"));
        items.AddRange(GetRecentRepoItems());

        items.Add(new MenuItem("Browse ...", "", ShowBrowseDialog));
        items.Add(new MenuItem("Clone ...", "", () => { }, () => false));
        items.Add(new MenuItem("Quit", "Esc ", () => Application.RequestStop()));

        var menu = new ContextMenu(4, 0, new MenuBarItem(items.ToArray()));
        menu.Show();
    }


    MenuItem[] GetRecentRepoItems() =>
        states.Get().RecentFolders
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


    void ShowBrowseDialog()
    {
        // Parent folders to recent work folders, usually other repos there as well
        var recentFolders = states.Get().RecentParentFolders;

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