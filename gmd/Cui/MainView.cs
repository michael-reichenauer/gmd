using gmd.Common;
using gmd.Git;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace gmd.Cui;

interface IMainView
{
    View View { get; }
}

partial class MainView : IMainView
{
    static readonly int MaxRecentFolders = 10;
    static readonly int MaxRecentParentFolders = 5;

    readonly IRepoView repoView;
    readonly IGit git;
    private readonly IState state;
    readonly Lazy<View> toplevel;

    MainView(IRepoView repoView, IGit git, IState state) : base()
    {
        this.repoView = repoView;
        this.git = git;
        this.state = state;
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

        mainView.Add(repoView.View);

        //Application.Current.Added += (v) => Log.Info($"View added {v}");
        //     Application.Current.Removed += (v) =>
        //    {
        //        Log.Info($"View removed {v}");
        //    };

        return mainView;
    }


    void OnReady()
    {
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

        Log.Info($"Show Path {rootPath}");
        ShowRepo(rootPath);
    }


    void ShowMainMenu()
    {
        List<MenuItem> items = new List<MenuItem>();
        items.Add(UI.MenuSeparator("Open Repo"));
        items.AddRange(GetRecentRepoItems());

        items.Add(new MenuItem("Browse ...", "", ShowBrowseDialog));
        items.Add(new MenuItem("Clone ...", "", () => { }, () => false));
        items.Add(new MenuItem("Quit", "", () => Application.RequestStop()));

        var menu = new ContextMenu(4, 0, new MenuBarItem(items.ToArray()));
        menu.Show();
    }


    MenuItem[] GetRecentRepoItems() =>
        state.Get().RecentFolders
            .Select(f => new MenuItem(f, "", () => ShowRepo(f)))
            .ToArray();


    void ShowRepo(string path)
    {
        UI.RunInBackground(async () =>
        {
            if (!Try(out var e, await repoView.ShowRepoAsync(path)))
            {
                UI.ErrorMessage($"Failed to load repo in:\n'{path}':\n{e}");
                ShowMainMenu();
                return;
            }
            state.Set(s => s.RecentFolders = s.RecentFolders
                .Prepend(path).Distinct().Take(MaxRecentFolders).ToList());
            var parent = Path.GetDirectoryName(path);
            if (parent != null)
            {
                state.Set(s => s.RecentParentFolders = s.RecentParentFolders
                   .Prepend(parent).Distinct().Take(MaxRecentParentFolders).ToList());
            }
        });
    }


    void ShowBrowseDialog()
    {
        // Parent folders to recent work folders, usually other repos there as well
        var recentFolders = state.Get().RecentParentFolders;

        var browser = new FolderBrowseDlg();
        if (!Try(out var path, out var e, browser.Show(recentFolders)))
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