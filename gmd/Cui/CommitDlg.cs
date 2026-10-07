using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDlg
{
    // The message, and the paths to commit, or null for all the changes, see CommitFiles
    bool Show(
        IViewRepo repo,
        bool isAmend,
        IReadOnlyList<Server.Commit>? commits,
        out string message,
        out IReadOnlyList<string>? paths
    );
}

class CommitDlg : ICommitDlg
{
    readonly ISpellChecker spellChecker;
    IReadOnlyList<Server.Commit>? commits;
    UITextView message = null!;

    // The checklist shows this many files at most, and scrolls for more
    const int MaxFileRows = 8;

    internal CommitDlg(ISpellChecker spellChecker)
    {
        this.spellChecker = spellChecker;
    }

    public bool Show(
        IViewRepo repo,
        bool isAmend,
        IReadOnlyList<Server.Commit>? commits,
        out string commitMessage,
        out IReadOnlyList<string>? paths
    )
    {
        this.commits = commits;
        paths = null;
        if (!isAmend && repo.Repo.Status.IsOk)
        {
            commitMessage = "";
            return false;
        }

        (string subjectPart, string messagePart) = ParseMessage(repo, isAmend);

        var commit = repo.Repo.ViewCommits[0];
        int filesCount = repo.Repo.Status.ChangesCount;
        string branchName = commit.BranchName;
        var title = isAmend ? "Amend" : "Commit";

        // The files, to untick what is not to be committed. Not while an operation is in progress,
        // whose result git commits whole, refusing a commit of some files of a merge, nor for an
        // amend with no changes, which rewords the last commit.
        var files = repo.Repo.Status.IsMerging ? null : new CommitFiles(repo.Repo.Status);
        if (files?.Files.Count == 0)
            files = null;
        var fileRows = files == null ? 0 : Math.Min(files.Files.Count, MaxFileRows);
        var listHeight = files == null ? 0 : fileRows + 2;

        var dlg = new UIDialog(title, 74, 18 + listHeight, (key) => OnKey(repo, key));

        var heading = dlg.AddLabel(1, 0, Heading(title, filesCount, isAmend, files, branchName));
        heading.Width = Dim.Fill(); // It grows as files are ticked again, see UILabel.Text
        var subject = dlg.AddInputField(1, 2, 50, subjectPart, InputMarkers.Both, spellChecker);

        message = dlg.AddMultiLineInputView(1, 4, 70, 10, messagePart, spellChecker);
        dlg.Validate(() => GetMessage(subject, message) != "", "Empty commit message");

        if (files != null)
        {
            dlg.AddLabel(1, 16, Text.Dark("Files, Space unticks one or ticks it again, and a all:"));
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
                heading.Text = Heading(title, filesCount, isAmend, files, branchName);
                list.SetNeedsDisplay();
            }
            list.RegisterKeyHandler(Key.Space, () => Toggle(() => files.Toggle(list.CurrentIndex)));
            list.RegisterKeyHandler(Key.a, () => Toggle(files.ToggleAll));
            list.RegisterKeyHandler(Key.A, () => Toggle(files.ToggleAll));
            list.RegisterMouseHandler(
                MouseFlags.Button1Clicked,
                (_, y) =>
                {
                    list.SetCurrentIndex(list.FirstIndex + y);
                    Toggle(() => files.Toggle(list.FirstIndex + y));
                }
            );
            dlg.Validate(() => files.TickedCount > 0, "No file is ticked to commit: Space ticks one");
        }

        dlg.ShowOkCancel(subject);

        commitMessage = GetMessage(subject, message);
        paths = files?.PathsToCommit;
        return dlg.IsOK;
    }

    // "Commit 3 changes on 'main':", and once some are unticked "Commit 2 of 3 changes on 'main':"
    static Text Heading(string title, int count, bool isAmend, CommitFiles? files, string branchName)
    {
        var changes =
            files == null || files.TickedCount == files.Files.Count
                ? Changes(count, isAmend)
                : $"{files.TickedCount} of {Changes(files.Files.Count, isAmend)}";
        return Text.White($"{title} {changes} on '{branchName}':");
    }

    // A file of the checklist: ticked or not, as a check box is drawn, what changed, and the path;
    // an unticked one dark, being left out
    static Text FileRow(CommitFiles files, int index, int width)
    {
        var file = files.Files[index];
        var isTicked = files.IsTicked(index);
        var path = file.Path.Length + 4 <= width ? file.Path : $"┅{file.Path[^Math.Max(0, width - 5)..]}";
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
        return text.White($" {path}");
    }

    private bool OnKey(IViewRepo repo, Key key)
    {
        if (key == (Key.D | Key.CtrlMask) || key == (Key.Space | Key.CtrlMask))
        {
            repo.CommitCmds.ShowUncommittedDiff(true);
            return true;
        }
        if (key == (Key.A | Key.CtrlMask))
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

        var msg = message.Text.ToString();
        if (msg != "")
        {
            msg += "\n";
        }

        message.Text = $"{msg}{MergedSubjects(commits)}";
        message.SetNeedsDisplay();
    }

    // The list Ctrl-A adds to the message of a merge: the subjects of the merged commits, oldest
    // first, one per line. Only the subject, since whole messages made a merge message too long to
    // read. A merge git named ('Merge branch ...') says nothing in its subject, so its list is
    // taken instead, i.e. the lines of its body that are list items, which is what Ctrl-A wrote when
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

    static (string, string) ParseMessage(IViewRepo repo, bool isAmend)
    {
        string msg = repo.Repo.Status.MergeMessage;

        if (isAmend)
        {
            var c = repo.Repo.CurrentCommit();
            msg = c.Message;
        }

        if (msg.Trim() == "")
        {
            return ("", "");
        }
        var lines = msg.Split('\n');
        if (lines.Length == 1)
        {
            return (lines[0], "");
        }

        string subject = lines[0];
        string message;
        if (lines.Length > 2 && lines[1] == "")
        {
            message = string.Join('\n', lines.Skip(2));
        }
        else
        {
            message = string.Join('\n', lines.Skip(1));
        }

        return (subject, message);
    }

    // "1 change", "2 changes", and for an amend with none, which rewords the last commit, what it
    // does amend
    static string Changes(int count, bool isAmend) =>
        count == 0 && isAmend ? "the last commit"
        : count == 1 ? "1 change"
        : $"{count} changes";

    static string GetMessage(UITextField subject, TextView message)
    {
        string subjectText = subject.Text;
        string msgText = message.Text.ToString()?.TrimEnd() ?? "";
        if (msgText.Trim() == "")
        {
            msgText = "";
        }

        if (subjectText != "" && msgText.Length > 0)
        {
            return subjectText + "\n\n" + msgText;
        }
        if (subjectText != "")
        {
            return subjectText;
        }

        return msgText;
    }
}
