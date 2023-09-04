using gmd.Common;
using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Installation;
using gmd.Server;


namespace gmd.Cui.RepoView;




interface IRepoCommands
{
    void ShowAbout();
    void ShowHelp();
    void ShowFileHistory();
    void Filter();
    void ShowBrowseDialog();
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
    readonly IServer server;
    readonly IProgress progress;
    readonly ICommitDlg commitDlg;
    readonly ICloneDlg cloneDlg;
    readonly IInitRepoDlg initRepoDlg;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IDeleteBranchDlg deleteBranchDlg;
    readonly IAddTagDlg addTagDlg;
    readonly ISetBranchDlg setBranchDlg;
    readonly IAboutDlg aboutDlg;
    readonly IHelpDlg helpDlg;
    readonly IDiffView diffView;
    readonly Config config;
    readonly IUpdater updater;
    readonly IRepoConfig repoConfig;
    readonly IBranchColorService branchColorService;
    readonly IViewRepo repo;
    readonly Repo serverRepo;
    readonly IRepoView repoView;

    string RepoPath => serverRepo.Path;
    Server.Status Status => serverRepo.Status;

    internal RepoCommands(
        IViewRepo repo,
        IRepoView repoView,
        IServer server,
        IProgress progress,
        ICommitDlg commitDlg,
        ICloneDlg cloneDlg,
        IInitRepoDlg initRepoDlg,
        ICreateBranchDlg createBranchDlg,
        IDeleteBranchDlg deleteBranchDlg,
        IAddTagDlg addTagDlg,
        ISetBranchDlg setBranchDlg,
        IAboutDlg aboutDlg,
        IHelpDlg helpDlg,
        IDiffView diffView,
        Config config,
        IUpdater updater,
        IRepoConfig repoConfig,
        IBranchColorService branchColorService)
    {
        this.repo = repo;
        this.serverRepo = repo.Repo;
        this.repoView = repoView;
        this.server = server;
        this.progress = progress;
        this.commitDlg = commitDlg;
        this.cloneDlg = cloneDlg;
        this.initRepoDlg = initRepoDlg;
        this.createBranchDlg = createBranchDlg;
        this.deleteBranchDlg = deleteBranchDlg;
        this.addTagDlg = addTagDlg;
        this.setBranchDlg = setBranchDlg;
        this.aboutDlg = aboutDlg;
        this.helpDlg = helpDlg;
        this.diffView = diffView;
        this.config = config;
        this.updater = updater;
        this.repoConfig = repoConfig;
        this.branchColorService = branchColorService;
    }

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);
    public void RefreshAndCommit(string addName = "", string commitId = "", IReadOnlyList<Server.Commit>? commits = null) =>
        repoView.RefreshAndCommit(addName, commitId, commits);

    public void RefreshAndFetch(string addName = "", string commitId = "") => repoView.RefreshAndFetch(addName, commitId);



    public void ShowRepo(string path) => Do(async () =>
     {
         if (!Try(out var e, await repoView.ShowRepoAsync(path)))
         {
             return R.Error($"Failed to open repo at {path}", e);
         }
         return R.Ok;
     });


    public void ShowBrowseDialog() => Do(async () =>
   {
       var browser = new FolderBrowseDlg();
       if (!Try(out var path, browser.Show(config.ResentParentFolders()))) return R.Ok;

       if (!Try(out var e, await repoView.ShowRepoAsync(path)))
       {
           return R.Error($"Failed to open repo at {path}", e);
       }
       return R.Ok;
   });

    public void ShowAbout() => aboutDlg.Show();
    public void ShowHelp() => helpDlg.Show();


    public void Clone() => Do(async () =>
    {
        if (!Try(out var r, out var e, cloneDlg.Show(config.ResentParentFolders()))) return R.Ok;
        (var uri, var path) = r;

        if (!Try(out e, await server.CloneAsync(uri, path, RepoPath)))
        {
            return R.Error($"Failed to clone", e);
        }

        if (!Try(out e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open repo at {path}", e);
        }
        return R.Ok;
    });

    public void InitRepo() => Do(async () =>
    {
        if (!Try(out var path, out var e, initRepoDlg.Show(config.ResentParentFolders()))) return R.Ok;

        if (!Try(out e, await server.InitRepoAsync(path, RepoPath)))
        {
            return R.Error($"Failed to init repo", e);
        }

        if (!Try(out e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open repo at {path}", e);
        }
        return R.Ok;
    });



    public void Filter() => Do(async () =>
     {
         await Task.CompletedTask;
         repoView.ShowFilter();
         return R.Ok;
     });


    public bool CanUndoUncommitted() => !serverRepo.Status.IsOk;


    public void UndoAllUncommittedChanged() => Do(async () =>
    {
        if (!Try(out var e, await server.UndoAllUncommittedChangesAsync(RepoPath)))
        {
            return R.Error($"Failed to undo all changes", e);
        }

        Refresh();
        return R.Ok;
    });

    public void CleanWorkingFolder() => Do(async () =>
    {
        if (UI.InfoMessage("Clean Working Folder",
            "Do you want to reset folder\nand delete all untracked files and folders?", 1, new[] { "Yes", "No" })
            != 0)
        {
            return R.Ok;
        }

        if (!Try(out var e, await server.CleanWorkingFolderAsync(RepoPath)))
        {
            return R.Error($"Failed to clean working folder", e);
        }

        Refresh();
        return R.Ok;
    });


    public void ShowFileHistory() => Do(async () =>
    {
        if (!Try(out var files, out var e, await GetFilesAsync()))
        {
            return R.Error($"Failed to get files", e);
        }

        var browser = new FileBrowseDlg();
        if (!Try(out var path, browser.Show(files))) return R.Ok;

        if (!Try(out var diffs, out e, await server.GetFileDiffAsync(path, RepoPath)))
        {
            return R.Error($"Failed to show file history", e);
        }

        diffView.Show(diffs);
        return R.Ok;
    });


    public void UpdateRelease() => Do(async () =>
    {
        await Task.Yield();

        var releases = config.Releases;
        var typeText = releases.IsPreview ? "(preview)" : "";
        string msg = $"A new release is available.\n\n" +
            $"Current Version: {Build.Version()}\n" +
            $"Built:           {Build.Time().Iso()}\n\n" +
            $"New Version:     {releases.LatestVersion} {typeText}\n" +
            $"Built:           {Build.GetBuildTime(releases.LatestVersion).Iso()}\n\n" +
            "Do you want to update?";

        var button = UI.InfoMessage("New Release", msg, new[] { "Yes", "No" });
        if (button != 0)
        {
            Log.Info($"Skip update");
            return R.Ok;
        }
        Log.Info($"Updating release ...");
        if (!Try(out var _, out var e, await updater.UpdateAsync())) return e;

        UI.InfoMessage("Restart Required", "A program restart is required,\nplease start gmd again.");
        UI.Shutdown();

        return R.Ok;
    });


    void Do(Func<Task<R>> action)
    {
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                if (!Try(out var e, await action()))
                {
                    UI.ErrorMessage($"{e.AllErrorMessages()}");
                }
            }
        });
    }


    public void CopyCommitId() => Do(async () =>
    {
        await Task.Yield();
        var commit = repo.RowCommit;
        if (!Try(out var e, Clipboard.Set(commit.Id)))
            return R.Error($"Clipboard copy not supported on this platform", e);

        return R.Ok;
    });

    public void CopyCommitMessage() => Do(async () =>
    {
        await Task.Yield();
        var commit = repo.RowCommit;
        if (!Try(out var e, Clipboard.Set(commit.Message.TrimEnd())))
            return R.Error($"Clipboard copy not supported on this platform", e);

        return R.Ok;
    });


    async Task<R<IReadOnlyList<string>>> GetFilesAsync()
    {
        var commit = repo.RowCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;
        return await server.GetFileAsync(reference, repo.Repo.Path);
    }
}
