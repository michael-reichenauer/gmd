using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Commits;

interface ICommitDlg
{
    // The message, and the paths to commit, or null for all the changes, see CommitFiles. Ctrl+D
    // shows the uncommitted diff, with showDiff. An amend of a commit on origin offers to push the
    // branch with force when done (forcePush), which isForcePush says was ticked.
    bool Show(
        Repo repo,
        Action showDiff,
        bool isAmend,
        IReadOnlyList<Server.Commit>? commits,
        ForcePush forcePush,
        out string message,
        out IReadOnlyList<string>? paths,
        out bool isForcePush
    );

    // The new message of an older commit, and the paths of the files to add to it, none at first, and
    // none for a new message alone
    bool ShowAmend(
        Repo repo,
        Action showDiff,
        Server.Commit commit,
        ForcePush forcePush,
        out string message,
        out IReadOnlyList<string> paths,
        out bool isForcePush
    );
}

class CommitDlg : ICommitDlg
{
    readonly ISpellChecker spellChecker;
    readonly LeftOutLines leftOut;
    IReadOnlyList<Server.Commit>? commits;
    UITextView message = null!;

    // The checklist shows this many files at most, and scrolls for more
    const int MaxFileRows = 8;

    internal CommitDlg(ISpellChecker spellChecker, LeftOutLines leftOut)
    {
        this.spellChecker = spellChecker;
        this.leftOut = leftOut;
    }

    public bool Show(
        Repo repo,
        Action showDiff,
        bool isAmend,
        IReadOnlyList<Server.Commit>? commits,
        ForcePush forcePush,
        out string commitMessage,
        out IReadOnlyList<string>? paths,
        out bool isForcePush
    )
    {
        this.commits = commits;
        paths = null;
        isForcePush = false;
        if (!isAmend && repo.Status.IsOk)
        {
            commitMessage = "";
            return false;
        }

        // The files, to untick what is not to be committed. Not while an operation is in progress,
        // whose result git commits whole, refusing a commit of some files of a merge, nor for an
        // amend with no changes, which rewords the last commit.
        var files = repo.Status.IsMerging ? null : new CommitFiles(repo.Status, leftOut: leftOut);
        var message = isAmend ? repo.CurrentCommit().Message : repo.Status.MergeMessage;
        var isOk = ShowDialog(
            repo,
            showDiff,
            isAmend,
            null,
            message,
            files,
            forcePush,
            out commitMessage,
            out isForcePush
        );
        paths = files?.PathsToCommit;
        return isOk;
    }

    public bool ShowAmend(
        Repo repo,
        Action showDiff,
        Server.Commit commit,
        ForcePush forcePush,
        out string commitMessage,
        out IReadOnlyList<string> paths,
        out bool isForcePush
    )
    {
        commits = null;
        var files = new CommitFiles(repo.Status, isTicked: false, leftOut);
        var isOk = ShowDialog(
            repo,
            showDiff,
            true,
            commit,
            commit.Message,
            files,
            forcePush,
            out commitMessage,
            out isForcePush
        );
        paths = files.TickedPaths;
        return isOk;
    }

    // The dialog of a commit, or of an amend, of the last commit or of an older one
    bool ShowDialog(
        Repo repo,
        Action showDiff,
        bool isAmend,
        Server.Commit? older,
        string original,
        CommitFiles? files,
        ForcePush forcePush,
        out string commitMessage,
        out bool isForcePush
    )
    {
        (string subjectPart, string messagePart) = CommitMessage.Split(original);

        int filesCount = repo.Status.ChangesCount;
        // The current branch, which the top row is only while there are changes: with none it is
        // the newest commit shown, of whichever branch, e.g. main after a fetch
        string branchName = repo.CurrentBranch()?.NiceNameUnique ?? "";
        var title = isAmend ? "Amend" : "Commit";
        if (files?.Files.Count == 0)
            files = null;
        // Fewer on a small terminal, where the list scrolls, so that the dialog, its buttons too, fits
        // on the screen: 20 rows are the rest of the dialog, and a 24 row terminal leaves 4 for files
        var maxFileRows = Math.Clamp(UI.ScreenSize.Height - 20, 1, MaxFileRows);
        var fileRows = files == null ? 0 : Math.Min(files.Files.Count, maxFileRows);
        var listHeight = files == null ? 0 : fileRows + 2;
        Text HeadingText() =>
            older == null
                ? Heading(title, filesCount, isAmend, files, branchName)
                : OlderHeading(older, files, branchName);

        // The force push box, when offered, is the row below the message
        var pushRows = forcePush == ForcePush.NotOffered ? 0 : 1;
        // A merge's dialog says what Ctrl-L adds, below that, where the files would be listed, when
        // it adds anything
        var isMergedHint = files == null && commits?.Count > 0 && MergedSubjects(commits) != "";
        var hintRows = isMergedHint ? 1 : 0;
        var dlg = new UIDialog(title, 74, 18 + listHeight + pushRows + hintRows, (key) => OnKey(showDiff, key));

        var heading = dlg.AddLabel(1, 0, HeadingText());
        heading.Width = Dim.Fill(); // It grows as files are ticked again, see UILabel.Text
        var subject = dlg.AddInputField(1, 2, 50, subjectPart, InputMarkers.Both, spellChecker);

        message = dlg.AddMultiLineInputView(1, 4, 70, 10, messagePart, spellChecker);
        dlg.Validate(() => GetMessage(subject, message) != "", "Empty commit message");
        if (isMergedHint)
            dlg.AddLabel(1, 15 + pushRows, Text.Dark("Ctrl-L adds the subjects of the merged commits"));

        if (files != null)
        {
            var label =
                older == null
                    ? "Files, Space unticks one or ticks it again, and a all:"
                    : "Files to add to it, Space ticks one or unticks it again, and a all:";
            dlg.AddLabel(1, 16, Text.Dark(label));
            var list = dlg.AddContentView(
                1,
                17,
                70,
                fileRows,
                (first, count, _, width) =>
                    (
                        files.Files.Skip(first).Take(count).Select((_, i) => FileRow(files, first + i, width)),
                        files.Files.Count
                    )
            );
            list.IsShowCursor = false;
            list.IsHighlightCurrentIndex = true;

            void Toggle(Action toggle)
            {
                toggle();
                heading.Text = HeadingText();
                list.SetNeedsDraw();
            }
            list.RegisterKeyHandler(Key.Space, () => Toggle(() => files.Toggle(list.CurrentIndex)));
            list.RegisterKeyHandler(Key.A, () => Toggle(files.ToggleAll));
            list.RegisterKeyHandler(Key.A.WithShift, () => Toggle(files.ToggleAll));
            list.RegisterMouseHandler(
                MouseFlags.LeftButtonClicked,
                (_, y) =>
                {
                    list.SetCurrentIndex(list.FirstIndex + y);
                    Toggle(() => files.Toggle(list.FirstIndex + y));
                }
            );
            if (!isAmend)
                dlg.Validate(() => files.TickedCount > 0, "No file is ticked to commit: Space ticks one");
        }

        // An older commit is amended with the whole of each file ticked (an 'amend!' commit, see
        // CommitRewriteService), so lines left out of one would go in, unasked
        if (older != null && files != null)
            dlg.Validate(
                () => files.TickedWithLinesLeftOut.Count == 0,
                "A ticked file has lines left out with x in the diff, which an older commit cannot be amended without: put them back, or untick it"
            );

        // A commit amended with the message it has and no file is not amended at all. The last one
        // can be given a new message alone with changes there too, by unticking them all: a ticked
        // file was asked for, so the message could not be fixed without taking work in progress in.
        if (older != null || (isAmend && files != null))
            dlg.Validate(
                () =>
                    GetMessage(subject, message) != CommitMessage.Join(subjectPart, messagePart)
                    || files?.TickedCount > 0,
                "Nothing to amend: edit the message or tick a file"
            );

        var push =
            forcePush == ForcePush.NotOffered
                ? null
                : dlg.AddCheckBox(1, 15 + listHeight, ForcePushLabel(repo), forcePush == ForcePush.Ticked);

        dlg.ShowOkCancel(subject);

        commitMessage = GetMessage(subject, message);
        isForcePush = dlg.IsOK && push?.Checked == true;
        return dlg.IsOK;
    }

    // The force push box of a dialog that rewrites commits on origin, naming where it pushes to
    internal static string ForcePushLabel(Repo repo) =>
        $"Push to '{repo.CurrentBranch()?.RemoteName}' with force when done";

    // "Amend 6d2212 on 'main':", and once files are ticked "Amend 6d2212 with 1 of 3 changes on 'main':"
    static Text OlderHeading(Server.Commit older, CommitFiles? files, string branchName)
    {
        var changes =
            files == null || files.TickedCount == 0 ? ""
            : files.TickedCount == files.Files.Count ? $" with {Changes(files.Files.Count, false)}"
            : $" with {files.TickedCount} of {Changes(files.Files.Count, false)}";
        return Text.White($"Amend {older.Sid}{changes} on '{branchName}':");
    }

    // "Commit 3 changes on 'main':", and once some are unticked "Commit 2 of 3 changes on 'main':"
    static Text Heading(string title, int count, bool isAmend, CommitFiles? files, string branchName)
    {
        var changes =
            files == null || files.TickedCount == files.Files.Count ? Changes(count, isAmend)
            : isAmend && files.TickedCount == 0 ? Changes(0, isAmend)
            : $"{files.TickedCount} of {Changes(files.Files.Count, isAmend)}";
        return Text.White($"{title} {changes} on '{branchName}':");
    }

    // A file of the checklist: ticked or not, as a check box is drawn, what changed, and the path;
    // an unticked one dark, being left out, and how many lines of a ticked one are left out
    static Text FileRow(CommitFiles files, int index, int width)
    {
        var file = files.Files[index];
        var isTicked = files.IsTicked(index);
        var leftOut = isTicked ? files.LeftOutCount(index) : 0;
        var leftOutText = leftOut == 0 ? "" : $"  {leftOut} line{(leftOut == 1 ? "" : "s")} left out";
        var pathWidth = width - leftOutText.Length;
        var path = file.Path.Length + 4 <= pathWidth ? file.Path : $"┅{file.Path[^Math.Max(0, pathWidth - 5)..]}";
        if (!isTicked)
            return Text.Dark($"□ {file.Kind} {path}");

        var kind = $"{file.Kind}";
        var text = Text.White("◙ ");
        text = file.Kind switch
        {
            'A' => text.Green(kind),
            'D' => text.Red(kind),
            _ => text.Yellow(kind),
        };
        return text.White($" {path}").Yellow(leftOutText);
    }

    private bool OnKey(Action showDiff, Key key)
    {
        if (key == Key.D.WithCtrl || key == Key.Space.WithCtrl)
        {
            showDiff();
            return true;
        }
        // After a merge, Ctrl-L adds the merged commits' subjects, as the dialog says. It was Ctrl-A,
        // which selects all everywhere else, and so not in the message of a merge.
        if (key == Key.L.WithCtrl && commits?.Count > 0)
        {
            AddMergeMessages();
            return true;
        }

        return false;
    }

    private void AddMergeMessages()
    {
        if (commits == null || commits.Count == 0)
            return;

        // Once: pressed again it would add the same list again below it
        var msg = message.Text.ToString();
        var subjects = MergedSubjects(commits);
        if (subjects == "" || msg.Contains(subjects))
            return;
        if (msg != "")
        {
            msg += "\n";
        }

        message.Text = $"{msg}{subjects}";
        message.SetNeedsDraw();
    }

    // The list Ctrl-L adds to the message of a merge: the subjects of the merged commits, oldest
    // first, one per line. Only the subject, since whole messages made a merge message too long to
    // read. A merge git named ('Merge branch ...') says nothing in its subject, so its list is
    // taken instead, i.e. the lines of its body that are list items, which is what this key wrote when
    // it was made. That is what merging dev into main carries over: the lists of the branches that
    // were merged into dev, whose own commits are not among the merged ones.
    internal static string MergedSubjects(IReadOnlyList<Server.Commit> commits) =>
        commits.Reverse().SelectMany(Subjects).Join("\n");

    static IEnumerable<string> Subjects(Server.Commit commit)
    {
        if (commit.ParentIds.Count > 1 && commit.Subject.StartsWith("Merge "))
            return commit.Message.Split('\n').Skip(1).Select(l => l.TrimEnd()).Where(l => l.StartsWith("- "));

        return commit.Subject.Trim() == "" ? [] : [$"- {commit.Subject}"];
    }

    // "1 change", "2 changes", and for an amend with none, which rewords the last commit, what it
    // does amend
    static string Changes(int count, bool isAmend) =>
        count == 0 && isAmend ? "the last commit"
        : count == 1 ? "1 change"
        : $"{count} changes";

    static string GetMessage(UITextField subject, UITextView message) => CommitMessage.Join(subject.Text, message.Text);
}
