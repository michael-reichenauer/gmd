using gmd.Common;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Installation;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.Start;

interface IStartView
{
    View View { get; }

    // Shows the page, for gmd started in 'folder', and 'open' to open a repository with. Shown again
    // after a repository failed to open, which keeps what was selected. The key-hint line is drawn
    // after the page, so it is marked for drawing whenever the page is drawn.
    void Show(string folder, Action<string> open, View keyHintBar);
    void Hide();

    // The page's keys, for the key-hint line
    IReadOnlyList<KeyHint> Hints();

    // A click on the application bar while the page is shown
    void OnBarClicked(ApplicationBarItem item);
}

// The start page, which gmd shows in place of the log until a repository is opened: when started
// outside a repository, or with '-m'. See StartRows for what it shows; this is the view, the keys
// and what they do. A key acts at once: Enter opens the selected item, the most recent repository
// to begin with, the numbers open the listed repositories, a letter runs its action, and Esc quits.
//
// Not a modal dialog, as the menu it replaced was, so a dialog an action opens is drawn over the
// page, and closing it leaves the page there to go on from, rather than a blank screen.
class StartView : IStartView
{
    readonly Config config;
    readonly ICloneDlg cloneDlg;
    readonly IInitRepoDlg initRepoDlg;
    readonly IHelpPage helpPage;
    readonly IAboutDlg aboutDlg;
    readonly IServer server;
    readonly IProgress progress;
    readonly IUpdater updater;
    readonly IStatusLine status;
    readonly ContentView view;

    string folder = "";
    Action<string> open = _ => { };
    View? keyHintBar;
    StartRepos repos = StartRepos.Empty;
    StartPage? page;
    StartItem? selected;
    bool isOpening;
    bool isUpdateChecked;

    public StartView(
        Config config,
        ICloneDlg cloneDlg,
        IInitRepoDlg initRepoDlg,
        IHelpPage helpPage,
        IAboutDlg aboutDlg,
        IServer server,
        IProgress progress,
        IUpdater updater,
        IStatusLine status
    )
    {
        this.config = config;
        this.cloneDlg = cloneDlg;
        this.initRepoDlg = initRepoDlg;
        this.helpPage = helpPage;
        this.aboutDlg = aboutDlg;
        this.server = server;
        this.progress = progress;
        this.updater = updater;
        this.status = status;

        view = new ContentView(OnGetContent)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            IsShowCursor = false,
            IsHighlightCurrentIndex = false,
            IsCustomShowSelection = true,
        };
        RegisterKeys();
    }

    public View View => view;

    public void Show(string folder, Action<string> open, View keyHintBar)
    {
        this.folder = folder;
        this.open = open;
        this.keyHintBar = keyHintBar;
        isOpening = false;

        view.Height = Dim.Fill(config.ShowKeyHints ? 1 : 0); // Known once the config is read
        view.Visible = true;
        Refresh();
        view.SetFocus();
        CheckForUpdate();
    }

    public void Hide() => view.Visible = false;

    public IReadOnlyList<KeyHint> Hints()
    {
        var page = Page();
        var numbered = page.Items.Count(i => i.Number > 0);
        return KeyHints.ForStart(numbered, Selected(page).Repo?.IsRecent == true);
    }

    public void OnBarClicked(ApplicationBarItem item)
    {
        switch (item)
        {
            case ApplicationBarItem.Update:
                UpdateAsync().RunInBackground();
                break;
            case ApplicationBarItem.Help:
                HelpAsync().RunInBackground();
                break;
            case ApplicationBarItem.Close:
                UI.Shutdown();
                break;
        }
    }

    void RegisterKeys()
    {
        view.RegisterKeyHandler(Key.CursorUp, () => Move(-1));
        view.RegisterKeyHandler(Key.CursorDown, () => Move(1));
        view.RegisterKeyHandler(Key.CursorUp | Key.ShiftMask, () => Move(-1));
        view.RegisterKeyHandler(Key.CursorDown | Key.ShiftMask, () => Move(1));
        view.RegisterKeyHandler(Key.Home, () => Move(-1000));
        view.RegisterKeyHandler(Key.PageUp, () => Move(-1000));
        view.RegisterKeyHandler(Key.End, () => Move(1000));
        view.RegisterKeyHandler(Key.PageDown, () => Move(1000));
        view.RegisterKeyHandler(Key.Space, () => { }); // Would scroll a page, which there is no need for here
        view.RegisterKeyHandler(Key.Enter, () => Act(Selected(Page())));
        view.RegisterKeyHandler(Key.Esc, UI.Shutdown);

        for (int number = 1; number <= 9; number++)
        {
            var n = number;
            view.RegisterKeyHandler((Key)('0' + n), () => ActOnNumber(n));
        }

        view.RegisterLetterHandler(Key.o, () => Act(StartAction.OpenFolder));
        view.RegisterLetterHandler(Key.c, () => Act(StartAction.Clone));
        view.RegisterLetterHandler(Key.i, () => Act(StartAction.Init));
        view.RegisterLetterHandler(Key.a, () => Act(StartAction.About));
        view.RegisterLetterHandler(Key.q, () => Act(StartAction.Quit));
        view.RegisterLetterHandler(Key.u, OnKeyU);
        view.RegisterKeyHandler((Key)'?', () => Act(StartAction.Help));
        view.RegisterKeyHandler(Key.F1, () => Act(StartAction.Help));

        // Delete, and Backspace, which is what the delete key of a Mac sends. Nothing is lost by a
        // slip: the folder is not touched, and opening the repository lists it again.
        view.RegisterKeyHandler(Key.DeleteChar, RemoveSelected);
        view.RegisterKeyHandler(Key.Backspace, RemoveSelected);

        view.RegisterMouseHandler(MouseFlags.Button1Clicked, OnClick);
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        var page = Page();
        keyHintBar?.SetNeedsDisplay(); // Drawn after the page, and says what the selected item allows

        var selectedRow = page.RowOf(page.IndexOf(Selected(page).IsSame));
        var shown = Math.Max(0, Math.Min(count, page.Count - firstIndex));
        var rows = Enumerable.Range(firstIndex, shown).Select(i => page.Row(i, i == selectedRow)).ToList();
        return (rows, page.Count);
    }

    // The page laid out for the size of the view, again whenever it changes size or what it lists
    StartPage Page()
    {
        if (page == null || pageSize != (view.ContentWidth, view.ContentHeight))
        {
            pageSize = (view.ContentWidth, view.ContentHeight);
            page = StartRows.Of(repos, NewVersion(), pageSize.Width, pageSize.Height);
        }
        return page;
    }

    (int Width, int Height) pageSize;

    // The version there is to update to, or "" when there is none
    string NewVersion()
    {
        var releases = config.Releases;
        return releases.IsUpdateAvailable() && Version.TryParse(releases.LatestVersion, out var latest)
            ? latest.Txt()
            : "";
    }

    // The selected item, found again by what it is when the page was laid out anew, or the first
    StartItem Selected(StartPage page)
    {
        var index = selected != null ? page.IndexOf(selected.IsSame) : -1;
        return page.Items[index >= 0 ? index : page.FirstItem];
    }

    // Reads again what to list, keeping the selected item, e.g. after a repository failed to open
    void Refresh()
    {
        repos = StartRepos.Of(config.RecentFolders, folder, Files.HomePath);
        page = null;
        view.SetNeedsDisplay();
    }

    void Move(int step)
    {
        var page = Page();
        Select(page.Step(page.IndexOf(Selected(page).IsSame), step));
    }

    // Selects an item and scrolls it into view, and for the first item, the top of the page with it
    void Select(int index)
    {
        var page = Page();
        selected = page.Items[index];
        if (index == 0)
            view.MoveToTop();
        view.Move(page.RowOf(index) - view.CurrentIndex);
        view.SetNeedsDisplay();
    }

    void OnClick(int x, int y)
    {
        var page = Page();
        var index = page.ItemAt(y + view.FirstIndex);
        if (index < 0)
            return;

        Select(index);
        Act(page.Items[index]);
    }

    void ActOnNumber(int number)
    {
        var page = Page();
        var index = page.IndexOf(i => i.Number == number);
        if (index < 0)
        {
            status.Notice($"There is no repository {number} on the list");
            return;
        }

        Select(index);
        Act(page.Items[index]);
    }

    void OnKeyU()
    {
        if (!config.Releases.IsUpdateAvailable())
        {
            status.Notice("There is no new version of gmd to update to");
            return;
        }

        Act(StartAction.Update);
    }

    void Act(StartAction action) => Act(new StartItem(action));

    void Act(StartItem item)
    {
        switch (item.Action)
        {
            case StartAction.OpenRepo:
                Open(item.Repo!.Path);
                break;
            case StartAction.OpenFolder:
                OpenFolder();
                break;
            case StartAction.Clone:
                CloneAsync().RunInBackground();
                break;
            case StartAction.Init:
                InitAsync().RunInBackground();
                break;
            case StartAction.Update:
                UpdateAsync().RunInBackground();
                break;
            case StartAction.Help:
                HelpAsync().RunInBackground();
                break;
            case StartAction.About:
                aboutDlg.Show();
                view.SetFocus();
                break;
            case StartAction.Quit:
                UI.Shutdown();
                break;
        }
    }

    // Once at a time: a click is not dropped while a repository is read, as a key is, so a second one
    // would start reading another
    void Open(string path)
    {
        if (isOpening)
            return;
        isOpening = true;
        open(path);
    }

    void RemoveSelected()
    {
        var page = Page();
        var item = Selected(page);
        if (item.Repo is not { IsRecent: true } repo)
        {
            status.Notice("Only a recent repository can be removed from the list");
            return;
        }

        var index = page.IndexOf(item.IsSame);
        config.Set(c => c.RecentFolders = c.RecentFolders.Where(f => !Files.IsSamePath(f, repo.Path)).ToList());
        status.Info($"Removed {repo.ShownPath} from the recent list, the folder is untouched");

        // The item that took its place is selected, so Delete again removes the next one
        Refresh();
        Select(Math.Min(index, Page().Items.Count - 1));
    }

    void OpenFolder()
    {
        var browser = new FolderBrowseDlg();
        var result = browser.Show(Folders());
        view.SetFocus();
        if (result is string path)
            Open(path);
    }

    // Where to browse, clone and init: the folder gmd was started in, since that is where it was
    // asked to be, then the ones the recent repositories are in. Not the start folder when it is
    // inside a repository, which only '-m' shows the page for, since nesting one is seldom meant.
    IReadOnlyList<string> Folders()
    {
        var parents = config.ResentParentFolders();
        return repos.IsFolderInRepo || folder == "" ? parents : [folder, .. parents];
    }

    async Task CloneAsync()
    {
        var cloneResult = cloneDlg.Show(Folders());
        view.SetFocus();
        if (cloneResult is not CloneInfo clone)
            return;

        Result cloned;
        using (progress.Show())
        {
            cloned = await server.CloneAsync(clone.Uri, clone.Path, "");
        }

        if (cloned is Error e)
        {
            UI.ErrorMessage($"Failed to clone:\n{clone.Uri}:\n{Git.LoginError.Text(e)}");
            view.SetFocus();
            return;
        }

        Open(clone.Path);
    }

    async Task InitAsync()
    {
        var folders = Folders();
        var defaultPath = repos.IsFolderInRepo
            ? (folders.Any() ? folders[0] + Path.DirectorySeparatorChar : "")
            : folder;
        var pathResult = initRepoDlg.Show(defaultPath, folders);
        view.SetFocus();
        if (pathResult is not string path)
            return;

        Result initiated;
        using (progress.Show())
        {
            initiated = await server.InitRepoAsync(path, "");
        }

        if (initiated is Error e)
        {
            UI.ErrorMessage($"Failed to init:\n{path}:\n{e.AllMessages()}");
            view.SetFocus();
            return;
        }

        Open(path);
    }

    // The help opens in the browser, or its link is copied in a box that says so
    async Task HelpAsync()
    {
        await helpPage.ShowAsync();
        view.SetFocus();
    }

    // Once, when the page is first shown, and the page is laid out again when it is done, with the
    // update offered if there is one
    void CheckForUpdate()
    {
        if (isUpdateChecked || config.Releases.IsUpdateAvailable())
            return;
        isUpdateChecked = true;

        updater
            .CheckUpdateAvailableAsync()
            .ContinueWith(_ =>
                UI.Post(() =>
                {
                    if (!view.Visible || !config.Releases.IsUpdateAvailable())
                        return;
                    page = null;
                    view.SetNeedsDisplay();
                })
            );
    }

    async Task UpdateAsync()
    {
        var releases = config.Releases;
        if (!Version.TryParse(releases.LatestVersion, out var latest))
            return;
        var typeText = releases.IsPreview ? "(preview)" : "";
        string msg =
            $"A new release is available.\n\n"
            + $"Current Version: {Build.Version().Txt()}\n"
            + $"Built:           {Build.Time().ToLocalTime().Iso()}\n\n"
            + $"New Version:     {latest.Txt()} {typeText}\n"
            + $"Built:           {Build.GetBuildTime(releases.LatestVersion).ToLocalTime().Iso()}\n\n"
            + "Do you want to update?";

        if (UI.InfoMessage("New Release", msg, ["Yes", "No"]) != 0)
        {
            Log.Info($"Skip update");
            view.SetFocus();
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
                view.SetFocus();
                return;
            }
        }

        UI.InfoMessage("Restart Required", "A program restart is required,\nplease start Gmd again.");
        UI.Shutdown();
    }
}
