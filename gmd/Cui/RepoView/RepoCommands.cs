using gmd.Common;
using gmd.Cui.Common;
using gmd.Installation;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IRepoCommands
{
    void ShowAbout();
    void ShowHelp();
    void SearchFilterRepo();
    void ShowBrowseRepoDialog();
    void UpdateRelease();
    void Clone();
    void InitRepo();

    void ShowRepo(string path);
    void Refresh(string addName = "", string commitId = "");
    void RefreshAndFetch(string addName = "", string commitId = "");

    void UndoAllUncommittedChanged();

    void CleanWorkingFolder();

    void CopyCommitId();
    void CopyCommitMessage();
}

class RepoCommands : IRepoCommands
{
    readonly IViewRepo repo;

    readonly IRepoView repoView;
    readonly IServer server;
    readonly IProgress progress;
    readonly ICloneDlg cloneDlg;
    readonly IInitRepoDlg initRepoDlg;
    readonly IAboutDlg aboutDlg;
    readonly IHelpDlg helpDlg;
    readonly Config config;
    readonly IUpdater updater;
    readonly IClipboardService clipboard;

    internal RepoCommands(
        IViewRepo repo,
        IRepoView repoView,
        IServer server,
        IProgress progress,
        ICloneDlg cloneDlg,
        IInitRepoDlg initRepoDlg,
        IAboutDlg aboutDlg,
        IHelpDlg helpDlg,
        Config config,
        IUpdater updater,
        IClipboardService clipboard
    )
    {
        this.repo = repo;
        this.repoView = repoView;
        this.server = server;
        this.progress = progress;
        this.cloneDlg = cloneDlg;
        this.initRepoDlg = initRepoDlg;
        this.aboutDlg = aboutDlg;
        this.helpDlg = helpDlg;
        this.config = config;
        this.updater = updater;
        this.clipboard = clipboard;
    }

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    public void RefreshAndCommit(
        string addName = "",
        string commitId = "",
        IReadOnlyList<Server.Commit>? commits = null
    ) => repoView.RefreshAndCommit(addName, commitId, commits);

    public void RefreshAndFetch(string addName = "", string commitId = "") =>
        repoView.RefreshAndFetch(addName, commitId);

    public void ShowRepo(string path) =>
        Do(async () =>
        {
            if (!Try(out var e, await repoView.ShowRepoAsync(path)))
            {
                return R.Error($"Failed to open repo at {path}", e);
            }
            return R.Ok;
        });

    public void ShowBrowseRepoDialog() =>
        Do(async () =>
        {
            var browser = new FolderBrowseDlg();
            if (!Try(out var path, browser.Show(config.ResentParentFolders())))
                return R.Ok;

            if (!Try(out var e, await repoView.ShowRepoAsync(path)))
            {
                return R.Error($"Failed to open repo at {path}", e);
            }
            return R.Ok;
        });

    public void ShowAbout() => aboutDlg.Show();

    public void ShowHelp() => helpDlg.Show();

    public void Clone() =>
        Do(async () =>
        {
            if (!Try(out var r, out var e, cloneDlg.Show(config.ResentParentFolders())))
                return R.Ok;
            (var uri, var path) = r;

            if (!Try(out e, await server.CloneAsync(uri, path, repo.Path)))
            {
                return R.Error($"Failed to clone", e);
            }

            if (!Try(out e, await repoView.ShowRepoAsync(path)))
            {
                return R.Error($"Failed to open repo at {path}", e);
            }
            return R.Ok;
        });

    public void InitRepo() =>
        Do(async () =>
        {
            if (!Try(out var path, out var e, initRepoDlg.Show(config.ResentParentFolders())))
                return R.Ok;

            if (!Try(out e, await server.InitRepoAsync(path, repo.Path)))
            {
                return R.Error($"Failed to init repo", e);
            }

            if (!Try(out e, await repoView.ShowRepoAsync(path)))
            {
                return R.Error($"Failed to open repo at {path}", e);
            }
            return R.Ok;
        });

    public void SearchFilterRepo() =>
        Do(async () =>
        {
            await Task.CompletedTask;
            repoView.ShowFilter();
            return R.Ok;
        });

    public void UndoAllUncommittedChanged() =>
        Do(async () =>
        {
            if (!Try(out var e, await server.UndoAllUncommittedChangesAsync(repo.Path)))
            {
                return R.Error($"Failed to undo all changes", e);
            }

            Refresh();
            return R.Ok;
        });

    public void CleanWorkingFolder() =>
        Do(async () =>
        {
            if (
                UI.InfoMessage(
                    "Clean Working Folder",
                    "Do you want to reset folder\nand delete all untracked files and folders?",
                    1,
                    ["Yes", "No"]
                ) != 0
            )
            {
                return R.Ok;
            }

            if (!Try(out var e, await server.CleanWorkingFolderAsync(repo.Path)))
            {
                return R.Error($"Failed to clean working folder", e);
            }

            Refresh();
            return R.Ok;
        });

    public void UpdateRelease() =>
        Do(async () =>
        {
            await Task.Yield();

            var releases = config.Releases;
            var latest = Version.Parse(releases.LatestVersion);
            var typeText = releases.IsPreview ? "(preview)" : "";
            string msg =
                $"A new release is available.\n\n"
                + $"Running Version: {Build.Version().Txt()}\n"
                + $"Built:           {Build.Time().Iso()}\n\n"
                + $"New Version:     {latest.Txt()} {typeText}\n"
                + $"Built:           {Build.GetBuildTime(releases.LatestVersion).Iso()}\n\n"
                + "Do you want to update?";

            var button = UI.InfoMessage("New Release", msg, ["Yes", "No"]);
            if (button != 0)
            {
                Log.Info($"Skip update");
                return R.Ok;
            }
            Log.Info($"Updating release ...");
            var updateTask = updater.UpdateAsync();
            UI.ShowMessageWhile(
                "Updating",
                $"Updating to version {latest.Txt()},\nthis might take a while ...",
                updateTask
            );
            if (!Try(out var _, out var e, await updateTask))
                return e;

            UI.InfoMessage("Restart Required", "A program restart is required after update,\nplease start gmd again.");
            UI.Shutdown();
            return R.Ok;
        });

    void Do(Func<Task<R>> action) => CommandRunner.Do(progress, action);

    public void CopyCommitId() =>
        Do(async () =>
        {
            await Task.Yield();
            var commit = repo.RowCommit;
            if (!Try(out var e, clipboard.Set(commit.Id)))
                return R.Error("Failed to copy the commit id", e);

            return R.Ok;
        });

    public void CopyCommitMessage() =>
        Do(async () =>
        {
            await Task.Yield();
            var commit = repo.RowCommit;
            if (!Try(out var e, clipboard.Set(commit.Message.TrimEnd())))
                return R.Error("Failed to copy the commit message", e);

            return R.Ok;
        });
}
