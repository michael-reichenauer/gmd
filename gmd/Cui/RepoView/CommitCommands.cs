using gmd.Cui.Blame;
using gmd.Cui.Commits;
using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Cui.Tags;
using gmd.Server;

namespace gmd.Cui.RepoView;

// What CommitAsync did, for the commands that have more to do afterwards
enum CommitResult
{
    Committed,
    NothingToCommit, // The working folder was already clean, so nothing was staged to commit
    Cancelled, // The user backed out, so whatever was staged is still staged
}

interface ICommitCommands
{
    void Commit(bool isAmend, IReadOnlyList<Commit>? commits = null);
    Task<Result<CommitResult>> CommitAsync(bool isAmend, IReadOnlyList<Commit>? commits = null);

    void ShowUncommittedDiff(bool isFromCommit = false);
    void ShowCurrentRowDiff();
    void ShowDiff(string commitId, string commitId2, bool isFromCommit = false);
    void MarkForDiff(string commitId);
    void DiffWithMarked(string commitId);
    void ShowFileHistory();
    void BlameFile();

    void Stash();
    void StashPop(string name);
    void StashApply(string name);
    void StashDiff(string name);
    void StashDrop(string name);

    void UndoCommit(string id);
    void UncommitLastCommit();
    void UncommitUntilCommit(string id);
    void UndoUncommittedFile(string path);
    void UndoUncommittedFiles(IReadOnlyList<string> paths);
    void SquashCommits(string id1, string id2);
    void AmendOlderCommit(string id);
    void DropCommit(string id);
    void CherryPick();

    void AddTag();
    void DeleteTag(string name);
    void ShowTags();
    bool CanUncommitLastCommit();
    bool CanUndoUncommitted();
    void ToggleDetails();
}

class CommitCommands : ICommitCommands
{
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly CommandContext context;
    readonly IServer server;
    readonly ICommitDlg commitDlg;
    readonly ISquashDlg squashDlg;
    readonly IDiffView diffView;
    readonly IBlameView blameView;
    readonly IAddTagDlg addTagDlg;
    readonly IAddStashDlg addStashDlg;
    readonly IRepoView repoView;
    readonly ITagsDlg tagsDlg;
    readonly DiffMark diffMark;

    public CommitCommands(
        CommandContext context,
        ICommitDlg commitDlg,
        ISquashDlg squashDlg,
        IDiffView diffView,
        IBlameView blameView,
        IAddTagDlg addTagDlg,
        IAddStashDlg addStashDlg,
        ITagsDlg tagsDlg,
        DiffMark diffMark
    )
    {
        this.context = context;
        this.progress = context.Progress;
        this.status = context.Status;
        this.server = context.Server;
        this.commitDlg = commitDlg;
        this.squashDlg = squashDlg;
        this.diffView = diffView;
        this.blameView = blameView;
        this.addTagDlg = addTagDlg;
        this.addStashDlg = addStashDlg;
        this.repoView = context.View;
        this.tagsDlg = tagsDlg;
        this.diffMark = diffMark;
    }

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    public void RefreshAndCommit(
        string addName = "",
        string commitId = "",
        IReadOnlyList<Server.Commit>? commits = null
    ) => repoView.RefreshAndCommit(addName, commitId, commits);

    public bool CanUndoUncommitted() => !context.ViewRepo.Repo.Status.IsOk;

    public void ToggleDetails() => repoView.ToggleDetails();

    public void Commit(bool isAmend, IReadOnlyList<Commit>? commits = null) =>
        Do(async repo =>
        {
            var commitResult = await CommitAsync(isAmend, commits);
            if (commitResult is not CommitResult result)
                return commitResult.Error;

            // Said rather than nothing happening, as it used to for a key pressed with nothing to do
            if (result == CommitResult.NothingToCommit)
                return new Notice(isAmend ? "Only a commit not yet pushed can be amended" : "Nothing to commit");
            if (result == CommitResult.Committed)
            {
                Refresh();
                status.Info(isAmend ? "Amended the last commit" : $"Committed to '{CurrentBranchName()}'");
            }
            return Result.Ok;
        });

    // The commit itself, without the refresh, so a command that has more to do after the commit
    // can await it and act on what the user did. MergeToBranch needs the difference: it can only
    // switch back off the target branch if the merge it staged there was actually committed.
    public async Task<Result<CommitResult>> CommitAsync(bool isAmend, IReadOnlyList<Commit>? commits = null)
    {
        var repo = context.ViewRepo;
        // Before the detached head check below, which a rebase would otherwise answer with
        // "create/switch to a branch first" — a rebase does leave HEAD detached, so that message is
        // both true and useless: the way out is to continue the rebase, not to make a branch.
        if (!OfferContinueInsteadOfCommit())
            return CommitResult.Cancelled;

        if (!isAmend && repo.Repo.Status.IsOk)
            return CommitResult.NothingToCommit;
        if (isAmend && !CommitRewrite.IsNotPushed(repo.Repo, repo.Repo.CurrentCommit()))
            return CommitResult.NothingToCommit;

        if (repo.Repo.CurrentBranch().IsDetached == true)
        {
            UI.ErrorMessage("Cannot commit in detached head state.\nPlease create/switch to a branch first.");
            return CommitResult.Cancelled;
        }

        // Ahead of the dialog, so the user is not asked for a message and then refused
        if (!await context.RepoCmds.ConfirmConflictsResolvedAsync("Commit"))
            return CommitResult.Cancelled;

        if (!await CheckBinaryOrLargeAddedFilesAsync())
            return CommitResult.Cancelled;

        if (
            !commitDlg.Show(
                repo.Repo,
                () => ShowUncommittedDiff(true),
                isAmend,
                commits,
                out var message,
                out var paths
            )
        )
            return CommitResult.Cancelled;

        // A pre-commit hook can make a commit take a while, e.g. one that formats the code. The
        // files left unticked in the dialog are left as they are, so with some unticked only the
        // ticked ones are committed.
        using (status.Progress(isAmend ? "Amending the last commit" : $"Committing to '{CurrentBranchName()}'"))
        {
            var committed =
                paths == null
                    ? await server.CommitAllChangesAsync(message, isAmend, repo.Path)
                    : await server.CommitFilesAsync(message, isAmend, paths, repo.Path);
            if (committed is Error e)
                return new Error($"Failed to commit", e);
        }

        return CommitResult.Committed;
    }

    string CurrentBranchName() => context.ViewRepo.Repo.CurrentBranch().NiceNameUnique;

    // A rebase, an 'am', and a cherry pick or revert of several commits are finished by continuing
    // them, not by committing: 'git commit' makes the commit git stopped on but leaves the
    // operation mid flight, with its remaining commits never applied. So point at the command that
    // does finish it. Returns false when it has answered for the commit.
    //
    // Which operation it is does not decide this — whether a commit is the whole of what is left
    // does, which is Status.IsFinishedByCommit. Testing the operation was wrong for gmd's own
    // Undo/Revert Commit: 'git revert --no-commit' records REVERT_HEAD even when it applies
    // cleanly, so an ordinary staged revert looked like a stopped one and its commit dialog could
    // not be opened at all.
    bool OfferContinueInsteadOfCommit()
    {
        var repo = context.ViewRepo;
        if (repo.Repo.Status.IsFinishedByCommit)
            return true;

        var name = context.RepoCmds.OperationName();
        var choice = UI.InfoMessage(
            $"{name} In Progress",
            $"A {name.ToLower()} is in progress.\n\n"
                + $"It is finished with Continue {name}, not by committing:\n"
                + "committing here would leave it part way through.",
            0,
            [$"Continue {name}", "Cancel"]
        );
        if (choice == 0)
            context.RepoCmds.ContinueOperation();

        return false;
    }

    public void ShowUncommittedDiff(bool isFromCommit = false) => ShowDiff(Repo.UncommittedId, "", isFromCommit);

    public void ShowCurrentRowDiff()
    {
        var repo = context.ViewRepo;
        var id1 = repo.RowCommit.Id;
        var id2 = "";
        var selection = repoView.Selection;
        var (i1, i2) = (selection.I1, selection.I2);
        if (i2 - i1 > 0)
        { // User has selected multiple commits
            id1 = repo.Repo.ViewCommits[i1].Id;
            id2 = repo.Repo.ViewCommits[i2].Id;
            // A range diff is the changes the selected commits made, i.e. from before the oldest to
            // the newest, which is only that when they are commits of one branch: rows of other
            // branches in between would be in it as well. Said on the status line, as no error.
            if (id1 == Repo.UncommittedId || id2 == Repo.UncommittedId)
            {
                status.Notice("The uncommitted changes are no commit to diff a range to: select commits only");
                return;
            }
            if (repo.Repo.CommitById[id1].BranchPrimaryName != repo.Repo.CommitById[id2].BranchPrimaryName)
            {
                status.Notice(
                    "The selected commits are on different branches: Mark for Diff in the commit menu diffs any two"
                );
                return;
            }
        }

        ShowDiff(id1, id2);
    }

    public void ShowDiff(string commitId, string commitId2, bool isFromCommit = false) =>
        Do(async repo =>
        {
            if (commitId == Repo.EmptyRepoCommitId)
                return Result.Ok;

            // How the diff view gets this same diff again, at whatever context it is asked for, and
            // whether it is the uncommitted changes alone, which it can commit and discard
            DiffSource source;
            if (commitId2 == "")
            {
                source = DiffSource.Single(n => server.GetCommitDiffAsync(commitId, n, repo.Path)) with
                {
                    CanCommit = commitId == Repo.UncommittedId,
                };
            }
            else
            {
                repoView.ClearSelection();
                var msg = $"Diff between {commitId.Sid()} and {commitId2.Sid()}";
                source = DiffSource.Single(n => server.GetDiffRangeAsync(commitId2, commitId, msg, n, repo.Path));
            }

            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not CommitDiff[] diffs)
                return new Error("Failed to get diff", diffsResult.Error);

            // Only the uncommitted diff can have conflicts, and reading them here keeps the await
            // off the main loop — see the note on IDiffView.Show
            var conflicts = ConflictState.None;
            if (source.CanCommit && !repo.Repo.Status.IsOk)
            {
                if (await server.GetConflictStateAsync(repo.Path) is ConflictState state)
                    conflicts = state;
            }

            // Shown once the command is done, outside Do, as every view of its own is: Do's progress
            // would be running under it, and the view's own, e.g. the reload of '+', would then only
            // be counted, with no marquee and with the keys not held while git works
            UI.Post(() =>
            {
                var rsp = diffView.Show(diffs, source, repo.Path, conflicts);
                if (rsp == DiffResult.Commit && !isFromCommit)
                {
                    RefreshAndCommit();
                }
                else if (rsp == DiffResult.Refresh && !isFromCommit)
                {
                    Refresh();
                }
            });
            return Result.Ok;
        });

    public void MarkForDiff(string commitId)
    {
        var repo = context.ViewRepo;
        diffMark.Set(repo.Path, commitId);
        var sid = commitId.Sid();
        status.Info($"Marked {sid} for a diff: Diff with {sid} in the menu of another commit diffs the two");
    }

    // The changes from the older of the two commits to the newer, whichever is marked, i.e. what
    // changed on the way from the one to the other, as 'git diff <older> <newer>'
    public void DiffWithMarked(string commitId) =>
        Do(async repo =>
        {
            var markedId = diffMark.IdIn(repo.Path);
            if (markedId == "" || !repo.Repo.CommitById.TryGetValue(markedId, out var marked))
                return new Notice("No commit is marked for a diff: Mark for Diff in the commit menu marks one");
            var commit = repo.Repo.CommitById[commitId];

            var (older, newer) = marked.GitIndex > commit.GitIndex ? (marked, commit) : (commit, marked);
            var message = $"Diff {older.Sid} to {newer.Sid}";
            var source = DiffSource.Single(n =>
                server.GetPreviewMergeDiffAsync(older.Id, newer.Id, message, n, repo.Path)
            );
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not CommitDiff[] diffs)
                return new Error("Failed to get diff", diffsResult.Error);

            UI.Post(() => diffView.Show(diffs, source, repo.Path)); // Outside Do, see ShowDiff
            return Result.Ok;
        });

    public void CherryPick() =>
        Do(async repo =>
        {
            var sha = repo.RowCommit.Id;
            var selection = repoView.Selection;
            var (i1, i2) = (selection.I1, selection.I2);
            if (i2 - i1 > 0)
            { // User selected range of commits
                if (CommitsToPick(repo.Repo, i1, i2) is not { } commits)
                    return new Notice(
                        "The selected commits are not one line of commits: select the commits of a branch"
                    );

                foreach (var commit in commits)
                {
                    if (await server.CherryPickAsync(commit.Id, repo.Path) is Error pickError)
                    {
                        return new Error($"Failed to cherry pick", pickError);
                    }
                    if (await server.CommitAllChangesAsync(commit.Message, false, repo.Path) is Error commitError)
                    {
                        return new Error($"Failed to commit", commitError);
                    }
                }
            }
            else
            { // User selected one commit
                if (await server.CherryPickAsync(sha, repo.Path) is Error e)
                {
                    return new Error($"Failed to cherry pick", e);
                }
            }

            repoView.ClearSelection();
            RefreshAndCommit();
            return Result.Ok;
        });

    public void Stash() =>
        Do(async repo =>
        {
            if (repo.Repo.Status.IsOk)
                return Result.Ok;
            var commitMsg = repo.Repo.CurrentCommit().Subject;
            var msgResult = addStashDlg.Show();
            if (msgResult is not string msg)
                return Result.Ok;
            msg = msg == "" ? commitMsg : msg;

            if (await server.StashAsync(msg, repo.Path) is Error e)
            {
                return new Error($"Failed to stash changes", e);
            }

            Refresh();
            return Result.Ok;
        });

    public void StashPop(string name) =>
        Do(async repo =>
        {
            if (!repo.Repo.Status.IsOk)
                return Result.Ok;

            if (await server.StashPopAsync(name, repo.Path) is Error e)
            {
                return new Error($"Failed to pop stash {name}", e);
            }

            Refresh();
            return Result.Ok;
        });

    // As Stash Pop, and keeps the stash, which is said, since nothing else shows it
    public void StashApply(string name) =>
        Do(async repo =>
        {
            if (!repo.Repo.Status.IsOk)
                return new Notice(Why.Changes);

            if (await server.StashApplyAsync(name, repo.Path) is Error e)
                return new Error($"Failed to apply stash {name}", e);

            Refresh();
            var message = repo.Repo.Stashes.FirstOrDefault(s => s.Name == name)?.Message ?? name;
            status.Info($"Applied '{message}' and kept the stash: Stash Drop removes it");
            return Result.Ok;
        });

    public void StashDiff(string name) =>
        Do(async repo =>
        {
            var source = DiffSource.Single(n => server.GetStashDiffAsync(name, n, repo.Path));
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not CommitDiff[] diffs)
                return new Error($"Failed to diff stash {name}", diffsResult.Error);

            UI.Post(() => diffView.Show(diffs, source, repo.Path)); // Outside Do, see ShowDiff
            return Result.Ok;
        });

    public void StashDrop(string name) =>
        Do(async repo =>
        {
            var message = repo.Repo.Stashes.FirstOrDefault(s => s.Name == name)?.Message ?? name;
            if (!Confirm.DropStash(message))
                return Result.Ok;

            if (await server.StashDropAsync(name, repo.Path) is Error e)
            {
                return new Error($"Failed to drop stash {name}", e);
            }

            Refresh();
            return Result.Ok;
        });

    public void UndoCommit(string id) =>
        Do(async repo =>
        {
            if (!CanUndoCommit())
                return Result.Ok;
            var commit = repo.Repo.CommitById[id];
            // A merge is reverted against the parent drawn on its line, so it undoes the side drawn as
            // merged in, which for a pull merge, whose parents gmd swaps, is git's first parent
            if (await server.UndoCommitAsync(id, commit.MainlineParentNumber, repo.Path) is Error e)
            {
                return new Error($"Failed to undo commit", e);
            }

            Refresh();
            return Result.Ok;
        });

    public bool CanUndoCommit() => context.ViewRepo.Repo.Status.IsOk;

    public void UncommitLastCommit() =>
        Do(async repo =>
        {
            if (!CanUncommitLastCommit())
                return Result.Ok;

            if (await server.UncommitLastCommitAsync(repo.Path) is Error e)
            {
                return new Error($"Failed to undo commit", e);
            }

            Refresh();
            return Result.Ok;
        });

    // The selected rows as commits to pick, oldest first: the newest and the commits down git's first
    // parents to the oldest, or null when the oldest is not on that line, e.g. rows of two branches,
    // or the uncommitted changes are among them. The walk used to go on past the first commit and
    // threw there, ending gmd.
    internal static IReadOnlyList<Commit>? CommitsToPick(Repo repo, int i1, int i2)
    {
        var (newest, oldest) = (repo.ViewCommits[i1], repo.ViewCommits[i2]);
        if (newest.IsUncommitted || oldest.IsUncommitted)
            return null;

        List<Commit> commits = [newest];
        var c = newest;
        while (c.Id != oldest.Id)
        {
            if (c.GitParentIds.Count == 0 || !repo.CommitById.TryGetValue(c.GitParentIds[0], out var parent))
                return null;
            c = parent;
            commits.Add(c);
        }

        commits.Reverse();
        return commits;
    }

    public void UncommitUntilCommit(string id) =>
        Do(async repo =>
        {
            var commit = repo.Repo.CommitById[id];
            if (CommitRewrite.WhyNotUncommit(repo.Repo, commit) is var whyNot && whyNot != "")
                return new Notice(whyNot);

            // Down git's first parent, the line the branch was made along, which a pull merge whose
            // parents gmd swaps to draw it does not show first
            if (await server.UncommitUntilCommitAsync(commit.GitParentIds[0], repo.Path) is Error e)
            {
                return new Error($"Failed to undo commit", e);
            }

            Refresh();
            return Result.Ok;
        });

    public void SquashCommits(string id1, string id2) =>
        Do(async repo =>
        {
            var c1 = repo.Repo.CommitById[id1];
            var c2 = repo.Repo.CommitById[id2];
            if (CommitRewrite.WhyNotSquash(repo.Repo, c1, c2) is var whyNot && whyNot != "")
                return new Notice(whyNot);

            var commits = new List<Commit>();
            var c = c1;
            while (c.Id != c2.ParentIds[0])
            {
                commits.Add(c);
                if (!c.ParentIds.Any())
                    break;
                c = repo.Repo.CommitById[c.ParentIds[0]];
            }

            if (!squashDlg.Show(commits, out var message))
                return Result.Ok;

            if (await server.SquashCommitsAsync(repo.Repo, id1, id2, message) is Error e)
            {
                return new Error("Failed to squash commits", e);
            }
            repoView.ClearSelection();

            RefreshAndCommit();

            return Result.Ok;
        });

    // An older commit, given a new message, the changes in the files ticked, or both, in the commit
    // dialog
    public void AmendOlderCommit(string id) => Do(repo => AmendOlderCommitAsync(id));

    async Task<Result> AmendOlderCommitAsync(string id)
    {
        var repo = context.ViewRepo;
        var commit = repo.Repo.CommitById[id];
        if (CommitRewrite.WhyNotAmend(repo.Repo, commit) is var whyNot && whyNot != "")
            return new Notice(whyNot);

        if (!commitDlg.ShowAmend(repo.Repo, () => ShowUncommittedDiff(true), commit, out var message, out var paths))
            return Result.Ok;

        using (status.Progress($"Amending {commit.Sid}"))
        {
            if (await server.AmendOlderCommitAsync(repo.Repo, id, message, paths) is Error e)
                return new Error($"Failed to amend {commit.Sid}", e);
        }

        Refresh();
        status.Info($"Amended '{message.Split('\n')[0].Trim()}' on '{CurrentBranchName()}': Undo takes it back");
        return Result.Ok;
    }

    // Takes a commit out of the current branch, asked first, since its changes go with it
    public void DropCommit(string id) =>
        Do(async repo =>
        {
            var commit = repo.Repo.CommitById[id];
            if (CommitRewrite.WhyNotDrop(repo.Repo, commit) is var whyNot && whyNot != "")
                return new Notice(whyNot);
            if (!Confirm.DropCommit(commit.Sid, commit.Subject, CurrentBranchName()))
                return Result.Ok;

            using (status.Progress($"Dropping {commit.Sid}"))
            {
                if (await server.DropCommitAsync(repo.Repo, id) is Error e)
                    return new Error($"Failed to drop {commit.Sid}", e);
            }

            Refresh();
            status.Info($"Dropped '{commit.Subject}' from '{CurrentBranchName()}': Undo brings it back");
            return Result.Ok;
        });

    public bool CanUncommitLastCommit()
    {
        var repo = context.ViewRepo;
        if (!repo.Repo.ViewCommits.Any())
            return false;

        // A clean tree, since with changes the top row is the virtual uncommitted commit rather than
        // a commit, so the reset would take back a row the user was not on
        return repo.Repo.Status.IsOk && CommitRewrite.IsNotPushed(repo.Repo, repo.Repo.ViewCommits[0]);
    }

    public void UndoUncommittedFile(string path) =>
        Do(async repo =>
        {
            if (!Confirm.UndoFile(path, repo.Repo.Status.AddedFiles.Contains(path)))
                return Result.Ok;

            if (await server.UndoUncommittedFileAsync(path, repo.Path) is Error e)
            {
                return new Error($"Failed to undo {path}", e);
            }

            Refresh();
            return Result.Ok;
        });

    public void UndoUncommittedFiles(IReadOnlyList<string> paths) =>
        Do(async repo =>
        {
            if (!Confirm.UndoFiles(paths))
                return Result.Ok;

            await UndoUncommittedFilesAsync(paths);
            Refresh();
            return Result.Ok;
        });

    public async Task UndoUncommittedFilesAsync(IReadOnlyList<string> paths)
    {
        var repo = context.ViewRepo;
        var failedPath = new List<string>();
        foreach (var path in paths)
        {
            if (await server.UndoUncommittedFileAsync(path, repo.Path) is Error)
            {
                failedPath.Add(path);
            }
        }
        if (failedPath.Any())
        {
            UI.ErrorMessage($"Failed to undo {failedPath.Count} files:\n{string.Join("\n", failedPath)}");
        }
    }

    public void AddTag() =>
        Do(async repo =>
        {
            var commit = repo.RowCommit;
            var branch = repo.Repo.BranchByName[commit.BranchName];
            var isPushable = branch.IsRemote || branch.RemoteName != "";

            if (commit.IsUncommitted)
                return new Notice("A tag is put on a commit: move to one first");

            if (addTagDlg.Show(isPushable) is not TagInfo tag)
                return Result.Ok;

            if (tag.message == "")
            {
                if (await server.AddTagAsync(tag.name, commit.Id, repo.Path) is Error e)
                    return new Error($"Failed to add tag {tag.name}", e);
            }
            else
            {
                var added = await server.AddAnnotatedTagAsync(tag.name, tag.message, commit.Id, repo.Path);
                if (added is Error e)
                    return new Error($"Failed to add tag {tag.name} '{tag.message}'", e);
            }

            Refresh();
            if (!tag.isPush)
                return Result.Ok;

            // Pushed apart from adding it, so a push that fails says the tag was added: it said the
            // tag had failed, which was there, and which Push in the list of tags then pushes
            using (status.Progress($"Pushing the tag '{tag.name}'"))
            {
                if (await server.PushTagAsync(tag.name, repo.Path) is Error pushError)
                    return new Error($"Added the tag {tag.name}, but failed to push it", pushError);
            }
            status.Info(TagRows.Pushed(tag.name));
            return Result.Ok;
        });

    public void DeleteTag(string name) =>
        Do(async repo =>
        {
            var tagsResult = await server.GetTagsAsync(repo.Path);
            if (tagsResult is not IReadOnlyList<RepoTag> tags)
                return new Error("Failed to read the tags", tagsResult.Error);
            if (tags.FirstOrDefault(t => t.Name == name) is not RepoTag tag)
                return new Notice($"There is no tag '{name}' any more");

            return await RemoveTagAsync(tag);
        });

    // Every tag, to show its commit, push it or remove it, see TagsDlg
    public void ShowTags() =>
        Do(async repo =>
        {
            var tagsResult = await server.GetTagsAsync(repo.Path);
            if (tagsResult is not IReadOnlyList<RepoTag> tags)
                return new Error("Failed to read the tags", tagsResult.Error);
            if (tags.Count == 0)
                return new Notice("There are no tags: Add Tag in the Tag menu puts one on a commit");

            var hasOrigin = repo.Repo.AllBranches.Any(b => b.IsRemote);
            if (tagsDlg.Show(TagRows.Items(tags, repo.Repo, hasOrigin)) is not TagChoice choice)
                return Result.Ok;

            var tag = choice.Item.Tag;
            switch (choice.Action)
            {
                case TagAction.Show:
                    Refresh(choice.Item.Commit!.BranchName, tag.CommitId);
                    return Result.Ok;
                case TagAction.Push:
                    using (status.Progress($"Pushing the tag '{tag.Name}'"))
                    {
                        if (await server.PushTagAsync(tag.Name, repo.Path) is Error e)
                            return new Error($"Failed to push the tag {tag.Name}", e);
                    }
                    status.Info(TagRows.Pushed(tag.Name));
                    return Result.Ok;
                default:
                    return await RemoveTagAsync(tag);
            }
        });

    // Removes a tag, asked first, and on origin too when origin has it, which gmd's record of origin's
    // tags says, rather than whether the branch of its commit has a remote branch, as it used to
    async Task<Result> RemoveTagAsync(RepoTag tag)
    {
        var repo = context.ViewRepo;
        if (!Confirm.RemoveTag(tag.Name, tag.IsOnOrigin))
            return Result.Ok;

        using (status.Progress($"Removing the tag '{tag.Name}'"))
        {
            if (await server.RemoveTagAsync(tag.Name, tag.IsOnOrigin, repo.Path) is Error e)
                return new Error($"Failed to remove the tag {tag.Name}", e);
        }

        Refresh();
        status.Info(TagRows.Removed(tag));
        return Result.Ok;
    }

    public void ShowFileHistory() =>
        Do(async repo =>
        {
            var commit = repo.RowCommit;
            // The files to pick from are the ones the reference has, while the history then shown
            // for the picked one is its full history, so the title names where the list came from
            var (reference, title) = commit.IsUncommitted
                ? (commit.BranchName, $"Select File of Branch {commit.BranchName}")
                : (commit.Id, $"Select File of Commit {commit.Id.Sid()}");

            var filesResult = await server.GetFilePathsAsync(reference, repo.Path);
            if (filesResult is not IReadOnlyList<string> files)
                return new Error($"Failed to get files", filesResult.Error);

            var browser = new FileBrowseDlg();
            if (browser.Show(files, title) is not string path)
                return Result.Ok;

            var source = new DiffSource(n => server.GetFileDiffAsync(path, n, repo.Path));
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not CommitDiff[] diffs)
                return new Error($"Failed to show file history", diffsResult.Error);

            UI.Post(() => diffView.Show(diffs, source, repo.Path)); // Outside Do, see ShowDiff
            return Result.Ok;
        });

    public void BlameFile() =>
        Do(async repo =>
        {
            var commit = repo.RowCommit;
            // The uncommitted row has no tree of its own, so its branch supplies the file list. It
            // then blames the working tree, so the uncommitted lines are part of the answer.
            var (reference, title) = commit.IsUncommitted
                ? (commit.BranchName, $"Blame File of Branch {commit.BranchName}")
                : (commit.Id, $"Blame File of Commit {commit.Id.Sid()}");

            var filesResult = await server.GetFilePathsAsync(reference, repo.Path);
            if (filesResult is not IReadOnlyList<string> files)
                return new Error($"Failed to get files", filesResult.Error);

            var browser = new FileBrowseDlg();
            if (browser.Show(files, title) is not string path)
                return Result.Ok;

            // Git blames a binary file as text, which arrives as mojibake. Only checked when the
            // file is in the working tree, since blaming an old revision of a since deleted file
            // is legitimate and IsText says 'not text' for a file that is not there.
            var fullPath = System.IO.Path.Join(repo.Path, path);
            if (File.Exists(fullPath) && !Files.IsText(fullPath))
            {
                return new Error($"Cannot blame a binary file:\n{path}");
            }

            var blameReference = commit.IsUncommitted ? "" : commit.Id;
            var blameResult = await server.GetBlameAsync(path, blameReference, repo.Path);
            if (blameResult is not Server.Blame blame)
                return new Error($"Failed to blame {path}", blameResult.Error);

            UI.Post(() => blameView.Show(blame, repo.Repo)); // Outside Do, see ShowDiff
            return Result.Ok;
        });

    void Do(Func<IViewRepo, Task<Result>> action) => context.Do(action);

    async Task<bool> CheckBinaryOrLargeAddedFilesAsync()
    {
        var repo = context.ViewRepo;
        var addFiles = repo.Repo.Status.AddedFiles.ToList();
        var addAndModified = addFiles
            .Concat(repo.Repo.Status.ModifiedFiles)
            .Concat(repo.Repo.Status.RenamedTargetFiles)
            .ToList();

        var binaryFiles = addAndModified.Where(f => !Files.IsText(Path.Join(repo.Path, f))).ToList();

        if (binaryFiles.Any())
        {
            var msg =
                $"There are {binaryFiles.Count} binary modified files:\n"
                + $"  {string.Join("\n  ", binaryFiles)}"
                + "\n\nDo you want to commit them as they are\nor first undo/revert them and then commit?";
            // Cancel is the default: Undo reverts a changed binary file and deletes a new one, which is
            // not what an Enter pressed out of habit should do
            var rsp = UI.InfoMessage("Binary Files Detected !", msg, 2, ["Commit", "Undo", "Cancel"]);
            if (rsp == 2 || rsp == -1)
                return false; // Cancel

            if (rsp == 1)
            {
                await UndoUncommittedFilesAsync(binaryFiles);
                UI.Post(() =>
                {
                    RefreshAndCommit();
                });
                return false;
            }
        }

        var largeFiles = addFiles
            .Where(f => !binaryFiles.Contains(f))
            .Where(f => Files.IsLarger(Path.Join(repo.Path, f), 100 * 1000))
            .Select(f => $"{f} ({Files.FileSize(Path.Join(repo.Path, f)).FileSize()})")
            .ToList();

        if (largeFiles.Any())
        {
            var msg =
                $"There are {largeFiles.Count} added large files:\n"
                + $"  {string.Join("\n  ", largeFiles)}"
                + "\n\nDo you want to continue?";
            if (0 != UI.InfoMessage("Large Files Detected !", msg, 1, ["Yes", "No"]))
            {
                return false;
            }
        }

        return true;
    }
}
