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
    CommittedAndPushed, // An amend of a commit on origin, pushed with force once made
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
    void UnmarkForDiff();
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
    void MoveCommit(string id, bool isUp);
    void ResetBranchTo(string id);
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
    readonly LeftOutLines leftOut;

    public CommitCommands(
        CommandContext context,
        ICommitDlg commitDlg,
        ISquashDlg squashDlg,
        IDiffView diffView,
        IBlameView blameView,
        IAddTagDlg addTagDlg,
        IAddStashDlg addStashDlg,
        ITagsDlg tagsDlg,
        DiffMark diffMark,
        LeftOutLines leftOut
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
        this.leftOut = leftOut;
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
                return new Notice(isAmend ? CommitRewrite.WhyNotAmendLast(repo.Repo) : "Nothing to commit");
            if (result is CommitResult.Committed or CommitResult.CommittedAndPushed)
            {
                Refresh();
                status.Info(
                    result == CommitResult.CommittedAndPushed
                        ? $"Amended the last commit, and pushed '{CurrentBranchName()}' with force"
                    : isAmend ? "Amended the last commit"
                    : $"Committed to '{CurrentBranchName()}'"
                );
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
        if (isAmend && CommitRewrite.WhyNotAmendLast(repo.Repo) != "")
            return CommitResult.NothingToCommit;
        if (isAmend && !IsPushedRewriteConfirmed(repo.Repo, [repo.Repo.CurrentCommit()]))
            return CommitResult.Cancelled;

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

        var forcePush = isAmend
            ? CommitRewrite.ForcePushOf(repo.Repo, [repo.Repo.CurrentCommit()])
            : ForcePush.NotOffered;
        if (
            !commitDlg.Show(
                repo.Repo,
                () => ShowUncommittedDiff(true),
                isAmend,
                commits,
                forcePush,
                out var message,
                out var paths,
                out var isForcePush
            )
        )
            return CommitResult.Cancelled;

        // A pre-commit hook can make a commit take a while, e.g. one that formats the code. The
        // files left unticked in the dialog are left as they are, so with some unticked only the
        // ticked ones are committed, and so are the lines left out with x in the diff. Not while an
        // operation is in progress, whose result git commits whole.
        leftOut.ForRepo(repo.Path);
        var changes = repo.Repo.Status;
        var committedPaths = paths ?? new CommitFiles(changes).TickedPaths;
        var leftOutLines = changes.IsMerging
            ? new Dictionary<string, IReadOnlyList<ChosenLine>>()
            : committedPaths.Where(leftOut.Paths.Contains).ToDictionary(p => p, leftOut.Of);
        using (status.Progress(isAmend ? "Amending the last commit" : $"Committing to '{CurrentBranchName()}'"))
        {
            var committed =
                leftOutLines.Count > 0
                    ? await server.CommitLinesAsync(message, isAmend, committedPaths, leftOutLines, repo.Path)
                : paths == null ? await server.CommitAllChangesAsync(message, isAmend, repo.Path)
                : await server.CommitFilesAsync(message, isAmend, paths, repo.Path);
            if (committed is Error e)
                return new Error($"Failed to commit", e);
        }

        // What was left out is now just uncommitted changes, which the next commit takes unless left out
        leftOut.PutBack(committedPaths);

        if (!isForcePush)
            return CommitResult.Committed;
        if (await ForcePushRewrittenAsync(repo.Repo) is Error pushError)
        {
            Refresh();
            return pushError;
        }
        return CommitResult.CommittedAndPushed;
    }

    // Pushes the current branch with force once its commits on origin were rewritten, leased on
    // origin's tip as the repo shown before the rewrite has it, so that a commit pushed there since is
    // not dropped with the old version: the push is then refused, and the branch is one rewritten here
    // (RewrittenHere), which 'p' pushes once that commit is dealt with
    async Task<Result> ForcePushRewrittenAsync(Repo shown)
    {
        var branch = shown.CurrentBranch();
        using (status.Progress($"Force pushing '{branch.NiceNameUnique}'"))
        {
            if (await server.ForcePushBranchAsync(shown, branch.Name) is Error e)
                return new Error($"Rewritten here, but not pushed to '{branch.RemoteName}'", e);
        }
        return Result.Ok;
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
                    IsCommittedWhole = repo.Repo.Status.IsMerging,
                    Sides = commitId == Repo.UncommittedId ? DiffSides.Uncommitted : null,
                };
            }
            else
            {
                repoView.ClearSelection();
                var msg = RangeTitle(repo.Repo, commitId, commitId2);
                source = DiffSource.Single(n => server.GetDiffRangeAsync(commitId2, commitId, msg, n, repo.Path)) with
                {
                    Sides = new($"{commitId2}~", commitId),
                };
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
                var rsp = diffView.Show(diffs, source, repo.Repo, conflicts);
                if (rsp == DiffResult.Commit && !isFromCommit)
                {
                    RefreshAndCommit();
                }
                else if (rsp == DiffResult.Continue && !isFromCommit)
                {
                    context.RepoCmds.ContinueOperation();
                }
                else if (rsp == DiffResult.Refresh && !isFromCommit)
                {
                    Refresh();
                }
            });
            return Result.Ok;
        });

    // The title of a range diff, which is the changes the selected commits made, the oldest one's
    // too, so it counts them: 'Diff of 3 commits, 4a15fb13 to 17d85ba8'. The diff of two commits
    // marked is 'Diff 4a15fb13 to 17d85ba8', the changes after the older one, which it leaves out.
    // The commits counted are those of the branch, as the selection is drawn on them alone.
    internal static string RangeTitle(Repo repo, string newerId, string olderId)
    {
        var (newer, older) = (repo.CommitById[newerId], repo.CommitById[olderId]);
        var count = repo
            .ViewCommits.Skip(newer.ViewIndex)
            .Take(older.ViewIndex - newer.ViewIndex + 1)
            .Count(c => c.BranchPrimaryName == newer.BranchPrimaryName);
        return $"Diff of {count} commits, {older.Sid} to {newer.Sid}";
    }

    // The log draws the mark, so it is drawn anew, the rows of both the old and the new mark
    public void MarkForDiff(string commitId)
    {
        var repo = context.ViewRepo;
        diffMark.Set(repo.Path, commitId);
        repoView.View.SetNeedsDraw();
        var sid = commitId.Sid();
        status.Info(
            $"Marked {sid} for a diff ({DiffMark.Marker}): Diff with {sid} in the menu of another commit diffs the two"
        );
    }

    public void UnmarkForDiff()
    {
        var sid = diffMark.IdIn(context.ViewRepo.Path).Sid();
        diffMark.Clear();
        repoView.View.SetNeedsDraw();
        status.Info($"Unmarked {sid} for a diff");
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
            ) with
            {
                Sides = new(older.Id, newer.Id),
            };
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not CommitDiff[] diffs)
                return new Error("Failed to get diff", diffsResult.Error);

            UI.Post(() => diffView.Show(diffs, source, repo.Repo)); // Outside Do, see ShowDiff
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
                if (WhyNotCherryPick(repo.Repo, i1, i2) is var whyNot && whyNot != "")
                    return new Notice(whyNot);
                var commits = CommitsToPick(repo.Repo, i1, i2)!;

                // Each committed as it was made, by git's own cherry pick, which a conflict stops
                // for the resolver, and Continue goes on with the rest
                var branchName = CurrentBranchName();
                using (status.Progress($"Cherry picking {commits.Count} commits into '{branchName}'"))
                {
                    if (await server.CherryPickCommitsAsync(repo.Repo, commits) is Error pickError)
                        return new Error($"Failed to cherry pick", pickError);
                }

                repoView.ClearSelection();
                Refresh();
                status.Info($"Cherry picked {commits.Count} commits into '{branchName}': Undo takes them back");
                return Result.Ok;
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
            var source = DiffSource.Single(n => server.GetStashDiffAsync(name, n, repo.Path)) with
            {
                Sides = new($"{name}^", name),
            };
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not CommitDiff[] diffs)
                return new Error($"Failed to diff stash {name}", diffsResult.Error);

            UI.Post(() => diffView.Show(diffs, source, repo.Repo)); // Outside Do, see ShowDiff
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
            status.Info(Confirm.Discarded($"the stash '{message}'"));
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

    // Why the commits of the rows selected cannot be cherry picked into the current branch, or "" when
    // they can: one line of another branch's commits, with no merge among them, which git picks only
    // as the changes of one of its sides
    internal static string WhyNotCherryPick(Repo repo, int i1, int i2)
    {
        if (repo.Status.IsMerging)
            return Why.InProgress;
        if (!repo.Status.IsOk)
            return Why.Changes;
        if (CommitsToPick(repo, i1, i2) is not { } commits)
            return "The selected commits are not one line of commits: select the commits of a branch";
        var current = repo.CurrentBranch();
        if (commits.Any(c => c.BranchPrimaryName == current.PrimaryName))
            return "The selected commits are on the current branch already";
        if (commits.FirstOrDefault(c => c.GitParentIds.Count > 1) is { } merge)
            return $"{merge.Sid} is a merge, which is no commit to pick: select the commits around it";
        return "";
    }

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
            if (!IsPushedRewriteConfirmed(repo.Repo, CommitRewrite.RewrittenBy(repo.Repo, c2)))
                return Result.Ok;

            var commits = CommitRewrite.Squashed(repo.Repo, c1, c2);

            var forcePush = CommitRewrite.ForcePushOf(repo.Repo, CommitRewrite.RewrittenBy(repo.Repo, c2));
            if (!squashDlg.Show(repo.Repo, commits, forcePush, out var message, out var isForcePush))
                return Result.Ok;

            if (await server.SquashCommitsAsync(repo.Repo, id1, id2, message) is Error e)
            {
                return new Error("Failed to squash commits", e);
            }
            repoView.ClearSelection();

            if (isForcePush && await ForcePushRewrittenAsync(repo.Repo) is Error pushError)
            {
                RefreshAndCommit();
                return pushError;
            }
            RefreshAndCommit();
            if (isForcePush)
                status.Info($"Squashed {commits.Count} commits, and pushed '{CurrentBranchName()}' with force");

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
        if (!IsPushedRewriteConfirmed(repo.Repo, CommitRewrite.RewrittenBy(repo.Repo, commit)))
            return Result.Ok;

        var forcePush = CommitRewrite.ForcePushOf(repo.Repo, CommitRewrite.RewrittenBy(repo.Repo, commit));
        if (
            !commitDlg.ShowAmend(
                repo.Repo,
                () => ShowUncommittedDiff(true),
                commit,
                forcePush,
                out var message,
                out var paths,
                out var isForcePush
            )
        )
            return Result.Ok;

        using (status.Progress($"Amending {commit.Sid}"))
        {
            if (await server.AmendOlderCommitAsync(repo.Repo, id, message, paths) is Error e)
                return new Error($"Failed to amend {commit.Sid}", e);
        }
        if (isForcePush && await ForcePushRewrittenAsync(repo.Repo) is Error pushError)
        {
            Refresh();
            return pushError;
        }

        Refresh();
        var subject = message.Split('\n')[0].Trim();
        status.Info(
            isForcePush
                ? $"Amended '{subject}', and pushed '{CurrentBranchName()}' with force"
                : $"Amended '{subject}' on '{CurrentBranchName()}': Undo takes it back"
        );
        return Result.Ok;
    }

    // Takes a commit out of the current branch, asked first, since its changes go with it
    public void DropCommit(string id) =>
        Do(async repo =>
        {
            var commit = repo.Repo.CommitById[id];
            if (CommitRewrite.WhyNotDrop(repo.Repo, commit) is var whyNot && whyNot != "")
                return new Notice(whyNot);
            var rewritten = CommitRewrite.RewrittenBy(repo.Repo, commit);
            if (!IsPushedRewriteConfirmed(repo.Repo, rewritten))
                return Result.Ok;
            var forcePush = CommitRewrite.ForcePushOf(repo.Repo, rewritten);
            var choice = Confirm.DropCommit(commit.Sid, commit.Subject, CurrentBranchName(), forcePush);
            if (choice == RewriteChoice.Cancel)
                return Result.Ok;

            using (status.Progress($"Dropping {commit.Sid}"))
            {
                if (await server.DropCommitAsync(repo.Repo, id) is Error e)
                    return new Error($"Failed to drop {commit.Sid}", e);
            }
            if (choice == RewriteChoice.RewriteAndPush && await ForcePushRewrittenAsync(repo.Repo) is Error pushError)
            {
                Refresh();
                return pushError;
            }

            Refresh();
            status.Info(
                choice == RewriteChoice.RewriteAndPush
                    ? $"Dropped '{commit.Subject}', and pushed '{CurrentBranchName()}' with force"
                    : $"Dropped '{commit.Subject}' from '{CurrentBranchName()}': Undo brings it back"
            );
            return Result.Ok;
        });

    // Commits on origin of a branch others build on, or someone else's, are rewritten only once the
    // user said so (CommitRewrite.PushedWarning), with No the default
    static bool IsPushedRewriteConfirmed(Repo repo, IReadOnlyList<Commit> commits) =>
        CommitRewrite.PushedWarning(repo, commits) is var warning && (warning == "" || Confirm.RewritePushed(warning));

    // A commit moved past the next one up the branch, or down it, i.e. the two swapped, which a
    // conflict between them stops as any rebase, and Undo takes back. Commits on origin then need the
    // force push 'p' offers, asked first on a branch others build on, as any rewrite of them.
    public void MoveCommit(string id, bool isUp) =>
        Do(async repo =>
        {
            var commit = repo.Repo.CommitById[id];
            if (CommitRewrite.WhyNotMove(repo.Repo, commit, isUp) is var whyNot && whyNot != "")
                return new Notice(whyNot);
            var other = CommitRewrite.Neighbour(repo.Repo, commit, isUp)!;
            if (!IsPushedRewriteConfirmed(repo.Repo, CommitRewrite.RewrittenBy(repo.Repo, isUp ? commit : other)))
                return Result.Ok;

            using (status.Progress($"Moving {commit.Sid}"))
            {
                if (await server.MoveCommitAsync(repo.Repo, id, isUp) is Error e)
                    return new Error($"Failed to move {commit.Sid}", e);
            }

            Refresh();
            status.Info($"Moved '{commit.Subject}' {(isUp ? "above" : "below")} '{other.Subject}': Undo takes it back");
            return Result.Ok;
        });

    // The current branch reset to a commit, of it or another: what its commits after it changed kept
    // as uncommitted changes, or discarded too, as asked, and Undo takes it back
    public void ResetBranchTo(string id) =>
        Do(async repo =>
        {
            var commit = repo.Repo.CommitById[id];
            if (CommitRewrite.WhyNotResetTo(repo.Repo, commit) is var whyNot && whyNot != "")
                return new Notice(whyNot);
            var branchName = CurrentBranchName();
            var choice = Confirm.ResetBranch(branchName, commit.Sid, commit.Subject, ResetWhat(repo.Repo, commit));
            if (choice == ResetChoice.Cancel)
                return Result.Ok;

            var isKeepChanges = choice == ResetChoice.KeepChanges;
            using (status.Progress($"Resetting '{branchName}' to {commit.Sid}"))
            {
                if (await server.ResetCurrentBranchAsync(repo.Repo, id, isKeepChanges) is Error e)
                    return new Error($"Failed to reset '{branchName}' to {commit.Sid}", e);
            }

            Refresh();
            var how = isKeepChanges ? "keeping the changes uncommitted" : "with the files";
            status.Info($"Reset '{branchName}' to {commit.Sid}, {how}: Undo takes it back");
            return Result.Ok;
        });

    // What a reset to the commit does to the branch: the commits after it that leave it, and whether
    // origin keeps some of them, which a pull then brings back; or the move sideways to it
    internal static string ResetWhat(Repo repo, Commit commit)
    {
        var branchName = repo.CurrentBranch().NiceNameUnique;
        if (CommitRewrite.LeftByReset(repo, commit) is not { } left)
            return $"'{branchName}' moves to it, a commit of '{commit.BranchNiceUniqueName}', off its own line.";

        var them = left.Count == 1 ? "The commit after it leaves" : $"The {left.Count} commits after it leave";
        var pushed = left.Count(c => !CommitRewrite.IsNotPushed(repo, c));
        var onOrigin =
            pushed == 0 ? ""
            : pushed == left.Count && pushed == 1 ? "\nIt is on origin, which keeps it, and a pull brings it back."
            : pushed == left.Count ? "\nThey are on origin, which keeps them, and a pull brings them back."
            : $"\n{pushed} of them are on origin, which keeps them, and a pull brings them back.";
        return $"{them} the branch.{onOrigin}";
    }

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
            status.Info(Confirm.Discarded(path));
            return Result.Ok;
        });

    public void UndoUncommittedFiles(IReadOnlyList<string> paths) =>
        Do(async repo =>
        {
            if (!Confirm.UndoFiles(paths))
                return Result.Ok;

            await UndoUncommittedFilesAsync(paths);
            Refresh();
            status.Info(Confirm.Discarded($"{paths.Count} file{(paths.Count == 1 ? "" : "s")}"));
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
            // The files to pick from are the ones the row's commit has, while the history then shown
            // for the picked one is every commit that changed it, opened at the row's commit as the
            // blame's ⇧l opens it at the line's, or at the newest when that one did not change it
            var commit = repo.RowCommit;
            var pickResult = await PickFileAsync(repo, "File History", "history");
            if (pickResult is not string path)
                return pickResult.Error;
            if (path == "")
                return Result.Ok;

            var source = new DiffSource(n => server.GetFileDiffAsync(path, n, repo.Path));
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not CommitDiff[] diffs)
                return new Error($"Failed to show file history", diffsResult.Error);

            var atCommitId = commit.IsUncommitted ? "" : commit.Id;
            UI.Post(() => diffView.Show(diffs, source, repo.Repo, atCommitId: atCommitId)); // Outside Do, see ShowDiff
            return Result.Ok;
        });

    public void BlameFile() =>
        Do(async repo =>
        {
            var commit = repo.RowCommit;
            var pickResult = await PickFileAsync(repo, "Blame File", "blame");
            if (pickResult is not string path)
                return pickResult.Error;
            if (path == "")
                return Result.Ok;

            // Git blames a binary file as text, which arrives as mojibake. Only checked when the
            // file is in the working tree, since blaming an old revision of a since deleted file
            // is legitimate and IsText says 'not text' for a file that is not there.
            var fullPath = System.IO.Path.Join(repo.Path, path);
            if (File.Exists(fullPath) && !Files.IsText(fullPath))
            {
                return new Error($"Cannot blame a binary file:\n{path}");
            }

            // The uncommitted row blames the working tree, so the uncommitted lines are part of the answer
            var blameReference = commit.IsUncommitted ? "" : commit.Id;
            var blameResult = await server.GetBlameAsync(path, blameReference, repo.Path);
            if (blameResult is not Server.Blame blame)
                return new Error($"Failed to blame {path}", blameResult.Error);

            UI.Post(() => blameView.Show(blame, repo.Repo)); // Outside Do, see ShowDiff
            return Result.Ok;
        });

    // A file of the row's commit, picked in the file browser, or "" when it was closed with none
    // picked. The uncommitted row has no tree of its own, so its branch supplies the files, and the
    // uncommitted changes are the ones listed first. Reading what a commit changed is only for the marks,
    // so a failure to is no reason not to offer its files.
    async Task<Result<string>> PickFileAsync(IViewRepo repo, string title, string action)
    {
        var commit = repo.RowCommit;
        var (reference, about, changedHeading) = commit.IsUncommitted
            ? (commit.BranchName, $"branch {commit.BranchName}", "Uncommitted changes")
            : (commit.Id, $"commit {commit.Id.Sid()}", $"Changed in commit {commit.Id.Sid()}");

        var filesTask = server.GetFilePathsAsync(reference, repo.Path);
        var changes = commit.IsUncommitted
            ? UncommittedChanges(repo.Repo.Status)
            : await server.GetChangedFilesAsync(commit.Id, repo.Path) switch
            {
                IReadOnlyList<ChangedFile> files => files
                    .DistinctBy(f => f.Path)
                    .ToDictionary(f => f.Path, f => f.Kind),
                Error e => LoggedNone(e),
            };

        var filesResult = await filesTask;
        if (filesResult is not IReadOnlyList<string> paths)
            return new Error($"Failed to get files", filesResult.Error);

        return new FileBrowseDlg().Show(title, paths, changes, changedHeading, about, action) is string path
            ? path
            : "";

        static Dictionary<string, char> LoggedNone(Error e)
        {
            Log.Warn($"Failed to read the changed files: {e.AllMessages()}");
            return [];
        }
    }

    // The files changed in the working tree that its branch has, with the letter the commit dialog
    // gives them; a new file is not one of the branch's, nor is a renamed file's new path
    static Dictionary<string, char> UncommittedChanges(Server.Status status)
    {
        Dictionary<string, char> changes = [];
        foreach (var path in status.ModifiedFiles)
            changes[path] = 'M';
        foreach (var path in status.DeletedFiles)
            changes[path] = 'D';
        foreach (var path in status.RenamedSourceFiles)
            changes[path] = 'R'; // Its old path, the one the branch has
        foreach (var path in status.ConflictsFiles)
            changes[path] = 'C';
        return changes;
    }

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
