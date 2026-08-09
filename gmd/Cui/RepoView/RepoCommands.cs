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

    string OperationName();
    string OperationSummary();
    Task<bool> ConfirmConflictsResolvedAsync(string action);
    void AbortOperation();
    void ContinueOperation();
    void SkipOperationCommit();

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

    // The name of what git stopped part way through, e.g. "Rebase", for the menu items that act on
    // it. "Operation" when nothing is in progress, where those items are hidden anyway.
    public string OperationName() => OperationName(repo.Repo.Status.Operation);

    static string OperationName(GitOperation operation) =>
        operation switch
        {
            GitOperation.Merge => "Merge",
            GitOperation.CherryPick => "Cherry Pick",
            GitOperation.Revert => "Revert",
            GitOperation.Rebase => "Rebase",
            GitOperation.Am => "Apply Patches",
            _ => "Operation",
        };

    // What the operation is doing, for the separator that heads the menu section, e.g.
    // "Rebasing 'dev' (3 of 7)  ·  2 conflicts"
    public string OperationSummary()
    {
        var s = repo.Repo.Status;
        var text = OperationName(s.Operation);
        if (s.OperationBranchName != "")
            text += $" '{s.OperationBranchName}'";
        if (s.OperationTotal > 0)
            text += $" ({s.OperationStep} of {s.OperationTotal})";
        if (s.Conflicted > 0)
            text += $"  ·  {s.Conflicted} conflict{(s.Conflicted == 1 ? "" : "s")}";

        return text;
    }

    // Whether it is safe to make a commit, i.e. whether the conflicts of an operation in progress
    // really are resolved. Two separate things can be wrong, and only the first is git's own job:
    //
    //   - a path is still unmerged, which git would refuse anyway, but saying so here means the
    //     user is not asked for a commit message first and refused after;
    //   - a path was marked resolved with the conflict markers still in it. Marking resolved is
    //     just 'git add', which does not look at what it stages, so this commits '<<<<<<<' into
    //     history and neither git nor gmd's own staging guards catch it.
    //
    // 'action' names what is about to happen, for the button that overrides the second warning.
    public async Task<bool> ConfirmConflictsResolvedAsync(string action)
    {
        if (repo.Repo.Status.Operation == GitOperation.None)
            return true;

        var conflicted = repo.Repo.Status.ConflictsFiles;
        if (conflicted.Length > 0)
        {
            var text = UnresolvedText(conflicted, action);
            if (UI.InfoMessage("Unresolved Conflicts", text, 0, ["Show Diff", "Cancel"]) == 0)
                repo.CommitCmds.ShowUncommittedDiff();
            return false;
        }

        if (!Try(out var marked, out var e, await server.GetLeftoverMarkerPathsAsync(repo.Path)))
        {
            Log.Warn($"Failed to check for conflict markers: {e}");
            return true; // Never block a commit because the check itself broke
        }
        if (marked.Count == 0)
            return true;

        var choice = UI.InfoMessage(
            "Conflict Markers Left",
            MarkersText(marked),
            2,
            ["Show Diff", $"{action} Anyway", "Cancel"]
        );
        if (choice == 0)
            repo.CommitCmds.ShowUncommittedDiff();

        return choice == 1;
    }

    static string UnresolvedText(IReadOnlyList<string> paths, string action) =>
        $"{Count(paths.Count, "file")} still {(paths.Count == 1 ? "has" : "have")} unresolved conflicts:\n\n"
        + $"{FileList(paths)}\n\n"
        + $"Resolve each one and mark it resolved, then {action.ToLower()}.";

    static string MarkersText(IReadOnlyList<string> paths) =>
        $"{Count(paths.Count, "file")} marked as resolved still {(paths.Count == 1 ? "contains" : "contain")} "
        + $"conflict markers:\n\n"
        + $"{FileList(paths)}\n\n"
        + "Committing now would put the '<<<<<<<' markers into history.";

    static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    // At most a handful of names, so a long list does not push the buttons off the screen
    static string FileList(IReadOnlyList<string> paths) =>
        string.Join('\n', paths.Take(8).Select(p => $"  {p}"))
        + (paths.Count > 8 ? $"\n  ... and {paths.Count - 8} more" : "");

    // Throws away everything the operation did and puts the working folder back as it was. It is
    // the only way out of a conflicted rebase, so it is worth confirming rather than a stray key.
    public void AbortOperation() =>
        Do(async () =>
        {
            var name = OperationName();
            if (UI.InfoMessage($"Abort {name}", $"Do you want to abort the {name.ToLower()}?", 1, ["Yes", "No"]) != 0)
                return R.Ok;

            if (!Try(out var e, await server.AbortOperationAsync(repo.Path)))
                return R.Error($"Failed to abort {name.ToLower()}", e);

            Refresh();
            return R.Ok;
        });

    // Carries on with the commits the operation has left. Not offered for a merge, which is
    // finished by committing it instead.
    public void ContinueOperation() =>
        Do(async () =>
        {
            // Continuing is how a rebase makes its commit, so it needs the same gate as Commit
            if (!await ConfirmConflictsResolvedAsync("Continue"))
                return R.Ok;

            if (!Try(out var e, await server.ContinueOperationAsync(repo.Path)))
            {
                Refresh(); // It may have got further before stopping again, so show where it is now
                return R.Error($"Failed to continue {OperationName().ToLower()}", e);
            }

            Refresh();
            return R.Ok;
        });

    // Drops the commit the operation stopped on and carries on with the rest
    public void SkipOperationCommit() =>
        Do(async () =>
        {
            var name = OperationName();
            if (
                UI.InfoMessage(
                    $"Skip Commit",
                    $"Do you want to drop the commit the {name.ToLower()} stopped on\nand carry on with the rest?",
                    1,
                    ["Yes", "No"]
                ) != 0
            )
                return R.Ok;

            if (!Try(out var e, await server.SkipOperationAsync(repo.Path)))
            {
                Refresh();
                return R.Error($"Failed to skip commit", e);
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
