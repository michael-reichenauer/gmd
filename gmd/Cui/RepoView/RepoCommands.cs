using gmd.Common;
using gmd.Cui.App;
using gmd.Cui.Common;
using gmd.Cui.Conflict;
using gmd.Cui.Start;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IRepoCommands
{
    void ShowAbout();
    void ShowHelp();
    void ShowWhatsNew();
    void ReportBug();
    void SuggestImprovement();
    void SearchFilterRepo();
    void ShowBrowseRepoDialog();
    void UpdateRelease();
    void Clone();
    void InitRepo();

    void ShowRepo(string path);
    void RefreshAndFetch(string addName = "", string commitId = "");

    void DiscardAllChanges();

    string OperationName();
    string OperationSummary();
    Task<bool> ConfirmConflictsResolvedAsync(string action);
    void AbortOperation();
    void ShowConflicts();
    void ResolveConflicts();
    void ResolveOrDiff();
    void ContinueOperation();
    void SkipOperationCommit();

    void Copy(CopyKind kind);
    void CopyCommitLink(string commitId);

    void OpenRepoInBrowser();
    void OpenBranchInBrowser(string name);
    void OpenCommitInBrowser(string commitId);
    void OpenNewPullRequest(string name);
}

class RepoCommands : IRepoCommands
{
    readonly CommandContext context;

    readonly IRepoView repoView;
    readonly IServer server;
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly ICloneDlg cloneDlg;
    readonly IInitRepoDlg initRepoDlg;
    readonly IAboutDlg aboutDlg;
    readonly IHelpPage helpPage;
    readonly IWebPage webPage;
    readonly IIssueReport issueReport;
    readonly Config config;
    readonly IUpdateDlg updateDlg;
    readonly IClipboardService clipboard;
    readonly IWebCommands webCmds;
    readonly IConflictWorkspace conflictWorkspace;

    internal RepoCommands(
        CommandContext context,
        ICloneDlg cloneDlg,
        IInitRepoDlg initRepoDlg,
        IAboutDlg aboutDlg,
        IHelpPage helpPage,
        IWebPage webPage,
        IIssueReport issueReport,
        Config config,
        IUpdateDlg updateDlg,
        IClipboardService clipboard,
        Func<CommandContext, IWebCommands> newWebCommands,
        IConflictWorkspace conflictWorkspace
    )
    {
        this.conflictWorkspace = conflictWorkspace;
        this.context = context;
        this.repoView = context.View;
        this.server = context.Server;
        this.progress = context.Progress;
        this.status = context.Status;
        this.cloneDlg = cloneDlg;
        this.initRepoDlg = initRepoDlg;
        this.aboutDlg = aboutDlg;
        this.helpPage = helpPage;
        this.webPage = webPage;
        this.issueReport = issueReport;
        this.config = config;
        this.updateDlg = updateDlg;
        this.clipboard = clipboard;
        this.webCmds = newWebCommands(context);
    }

    public void OpenRepoInBrowser() => webCmds.OpenRepo();

    public void OpenBranchInBrowser(string name) => webCmds.OpenBranch(name);

    public void OpenCommitInBrowser(string commitId) => webCmds.OpenCommit(commitId);

    public void CopyCommitLink(string commitId) => webCmds.CopyCommitLink(commitId);

    public void OpenNewPullRequest(string name) => webCmds.OpenNewPullRequest(name);

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    public void RefreshAndFetch(string addName = "", string commitId = "") =>
        repoView.RefreshAndFetch(addName, commitId);

    public void ShowRepo(string path) =>
        Do(async repo =>
        {
            if (await repoView.ShowRepoAsync(path) is Error e)
            {
                return new Error($"Failed to open repo at {path}", e);
            }
            return Result.Ok;
        });

    public void ShowBrowseRepoDialog() =>
        Do(async repo =>
        {
            var browser = new FolderBrowseDlg();
            if (browser.Show("Open Repo", "open", config.RecentParentFolders()) is not string path)
                return Result.Ok;

            if (await repoView.ShowRepoAsync(path) is Error e)
            {
                return new Error($"Failed to open repo at {path}", e);
            }
            return Result.Ok;
        });

    public void ShowAbout() => aboutDlg.Show();

    public void ShowHelp() => helpPage.ShowAsync().RunInBackground();

    public void ShowWhatsNew() =>
        webPage.OpenAsync(Project.ChangeLogUrlOfBuild(), "the change log", "What's New").RunInBackground();

    public void ReportBug() => issueReport.OpenAsync(IssueKind.Bug).RunInBackground();

    public void SuggestImprovement() => issueReport.OpenAsync(IssueKind.Suggestion).RunInBackground();

    public void Clone() =>
        Do(async repo =>
        {
            if (cloneDlg.Show(config.RecentParentFolders()) is not CloneInfo clone)
                return Result.Ok;

            if (await server.CloneAsync(clone.Uri, clone.Path, repo.Path) is Error cloneError)
            {
                return new Error($"Failed to clone", cloneError);
            }

            if (await repoView.ShowRepoAsync(clone.Path) is Error showError)
            {
                return new Error($"Failed to open repo at {clone.Path}", showError);
            }
            return Result.Ok;
        });

    public void InitRepo() =>
        Do(async repo =>
        {
            var parents = config.RecentParentFolders();
            var pathResult = initRepoDlg.Show(parents.Any() ? InitPath.InFolder(parents[0]) : "", parents);
            if (pathResult is not string path)
                return Result.Ok;

            if (await server.InitRepoAsync(path, repo.Path) is Error initError)
            {
                return new Error($"Failed to init repo", initError);
            }

            if (await repoView.ShowRepoAsync(path) is Error showError)
            {
                return new Error($"Failed to open repo at {path}", showError);
            }
            return Result.Ok;
        });

    public void SearchFilterRepo() =>
        Do(async repo =>
        {
            await Task.CompletedTask;
            repoView.ShowFilter();
            return Result.Ok;
        });

    // Discard All Changes, in the commit menu's Undo: the uncommitted changes, and the files git
    // ignores too when the question is answered so, which it offers when there are any (see
    // Confirm.DiscardAll). Enabled whatever the status, since the ignored files are read only here.
    public void DiscardAllChanges() =>
        Do(async repo =>
        {
            var ignoredResult = await server.GetIgnoredFilesAsync(repo.Path);
            if (ignoredResult is not IReadOnlyList<string> ignored)
                return new Error("Failed to list the ignored files", ignoredResult.Error);
            var isChanges = !repo.Repo.Status.IsOk;
            if (!isChanges && ignored.Count == 0)
                return new Notice("There are no uncommitted changes, nor files git ignores, to discard");

            var choice = Confirm.DiscardAll(isChanges, ignored);
            if (choice == DiscardChoice.Cancel)
                return Result.Ok;

            if (choice == DiscardChoice.Changes)
            {
                if (await server.UndoAllUncommittedChangesAsync(repo.Path) is Error undoError)
                    return new Error("Failed to undo all changes", undoError);

                Refresh();
                status.Info(Confirm.Discarded("all changes"));
                return Result.Ok;
            }

            if (await server.CleanWorkingFolderAsync(repo.Path) is Error cleanError)
                return new Error("Failed to clean the working folder", cleanError);

            Refresh();
            status.Info(
                isChanges
                    ? "Discarded all changes and deleted the ignored files: Restore Discarded Changes in the Undo menu brings back the changes"
                    : "Deleted the files git ignores, as a fresh clone has the folder"
            );
            return Result.Ok;
        });

    // The name of what git stopped part way through, e.g. "Rebase", for the menu items that act on
    // it. "Operation" when nothing is in progress, where those items are hidden anyway.
    public string OperationName() => OperationText.Name(context.ViewRepo.Repo.Status.Operation);

    // What the operation is doing, for the separator that heads the menu section, e.g.
    // "Rebasing 'dev' (3 of 7)  ·  2 conflicts"
    public string OperationSummary()
    {
        var repo = context.ViewRepo;
        var s = repo.Repo.Status;
        var text = OperationText.Name(s.Operation);
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
    //
    // The conflicts are read from git, not from the repo shown, which lags what was just resolved
    // until the file monitor reports the writes: Continue chosen as the resolver saved the last
    // file was refused, with that file listed as unresolved.
    public async Task<bool> ConfirmConflictsResolvedAsync(string action)
    {
        var repo = context.ViewRepo;
        var (operation, conflicted) = await server.GetConflictStateAsync(repo.Path) is ConflictState state
            ? (state.Operation, state.Files.Select(f => f.Path).ToArray())
            : (repo.Repo.Status.Operation, repo.Repo.Status.ConflictsFiles);
        if (operation == GitOperation.None)
            return true;

        if (conflicted.Length > 0)
        {
            var text = UnresolvedText(conflicted, action);
            if (UI.InfoMessage("Unresolved Conflicts", text, 0, ["Show Diff", "Cancel"]) == 0)
                context.CommitCmds.ShowUncommittedDiff();
            return false;
        }

        var markedResult = await server.GetLeftoverMarkerPathsAsync(repo.Path);
        if (markedResult is not IReadOnlyList<string> marked)
        {
            Log.Warn($"Failed to check for conflict markers: {markedResult.Error}");
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
            context.CommitCmds.ShowUncommittedDiff();

        return choice == 1;
    }

    // After a command stopped on conflicts: what stopped, the files, and the way on, where there used
    // to be git's output in a red error box. Resolving is done in the resolver, file by file, which
    // then asks to commit or continue to finish the operation. Aborting throws it away; chosen here
    // it is not asked about again, since nothing has been resolved yet to lose.
    public void ShowConflicts()
    {
        var repo = context.ViewRepo;
        var s = repo.Repo.Status;
        if (!s.IsMerging || s.Conflicted == 0)
            return; // E.g. resolved by git itself, with rerere

        var name = OperationName();
        var what = name + (s.OperationBranchName != "" ? $" '{s.OperationBranchName}'" : "");
        var finish = s.IsFinishedByCommit
            ? $"commit (c) to finish the {name.ToLower()}"
            : $"continue (c) the {name.ToLower()}";
        var text =
            $"{what} stopped on conflicts in {Count(s.Conflicted, "file")}:\n\n"
            + $"{FileList(s.ConflictsFiles)}\n\n"
            + $"Resolve them one file after the other, then {finish}.\n"
            + "⇧m opens the repo menu, to resolve or to abort it later.";

        var choice = UI.InfoMessage(
            $"{name} Stopped on Conflicts",
            text,
            0,
            ["Resolve Conflicts", $"Abort {name}", "Later"]
        );
        if (choice == 0)
            ResolveConflicts();
        else if (choice == 1)
            Abort(isAsked: true);
    }

    // The conflicts of the operation stopped on them, resolved file by file in the resolver
    // (ConflictWorkspace), and once none is left, the commit or the continue that finishes it, as
    // the user chose there
    public async void ResolveConflicts()
    {
        var next = await conflictWorkspace.ResolveAsync(context.ViewRepo.Path);
        if (next == ResolveNext.Commit)
            repoView.RefreshAndCommit();
        else if (next == ResolveNext.Continue)
            ContinueOperation();
        else
            Refresh();
    }

    // 'd' on the uncommitted row with files in conflict: resolve them, the default, or show the diff
    // of the uncommitted changes. It showed the diff, where Enter on a file resolved it, while its
    // hint said 'd resolve'. Ctrl-D and the commit menu's Commit Diff show the diff without asking.
    public void ResolveOrDiff()
    {
        var s = context.ViewRepo.Repo.Status;
        var what = s.IsMerging
            ? $"The {OperationName().ToLower()} stopped on conflicts in:"
            : "There are conflicts in:";
        var choice = UI.InfoMessage(
            $"Conflicts in {Count(s.Conflicted, "File")}",
            $"{what}\n\n{FileList(s.ConflictsFiles)}",
            0,
            ["Resolve Conflicts", "Show Diff"]
        );
        // Esc (-1) is neither
        if (choice == 0)
            ResolveConflicts();
        else if (choice == 1)
            context.CommitCmds.ShowUncommittedDiff();
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
    public void AbortOperation() => Abort(isAsked: false);

    void Abort(bool isAsked) =>
        Do(async repo =>
        {
            var name = OperationName();
            var isConfirmed =
                isAsked
                || UI.InfoMessage($"Abort {name}", $"Do you want to abort the {name.ToLower()}?", 1, ["Yes", "No"])
                    == 0;
            if (!isConfirmed)
                return Result.Ok;

            if (await server.AbortOperationAsync(repo.Path) is Error e)
                return new Error($"Failed to abort {name.ToLower()}", e);

            Refresh();
            return Result.Ok;
        });

    // Carries on with the commits the operation has left. Not offered for a merge, which is
    // finished by committing it instead.
    public void ContinueOperation() =>
        Do(async repo =>
        {
            // Continuing is how a rebase makes its commit, so it needs the same gate as Commit
            if (!await ConfirmConflictsResolvedAsync("Continue"))
                return Result.Ok;

            if (await server.ContinueOperationAsync(repo.Path) is Error e)
            {
                // It may have got further before stopping again, so show where it is now. Stopped on
                // conflicts, CommandRunner refreshes, and a second refresh here ran beside it.
                if (!Git.ConflictError.IsIn(e))
                    Refresh();
                return new Error($"Failed to continue {OperationName().ToLower()}", e);
            }

            Refresh();
            return Result.Ok;
        });

    // Drops the commit the operation stopped on and carries on with the rest
    public void SkipOperationCommit() =>
        Do(async repo =>
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
                return Result.Ok;

            if (await server.SkipOperationAsync(repo.Path) is Error e)
            {
                if (!Git.ConflictError.IsIn(e)) // Then CommandRunner refreshes, see ContinueOperation
                    Refresh();
                return new Error($"Failed to skip commit", e);
            }

            Refresh();
            return Result.Ok;
        });

    public void UpdateRelease() => Do(repo => updateDlg.ShowAsync());

    void Do(Func<IViewRepo, Task<Result>> action) => context.Do(action);

    // Copies the ids, the messages, or another kind of text of the commit on the cursor's row or of
    // the commits selected (see CommitCopy): 'i', '⇧i' and the copy menu (Ctrl-C). The selection is
    // done with once copied, as it is once diffed.
    public void Copy(CopyKind kind) =>
        Do(async repo =>
        {
            await Task.Yield();
            var selection = repoView.Selection;
            if (CommitCopy.WhyNot(repo.Repo, selection, repo.CurrentIndex) is var whyNot && whyNot != "")
                return new Notice(whyNot);

            var commits = CommitCopy.Commits(repo.Repo, selection, repo.CurrentIndex);
            if (CommitCopy.Text(kind, commits) is not string text)
                return new Notice(CommitCopy.WhyNoRange(commits));
            if (clipboard.Set(text) is Error e)
                return new Error("Failed to copy", e);

            repoView.ClearSelection();
            status.Info(CommitCopy.Said(kind, commits));
            return Result.Ok;
        });
}
