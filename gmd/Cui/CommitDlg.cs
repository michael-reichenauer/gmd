using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDlg
{
    bool Show(IViewRepo repo, bool isAmend, IReadOnlyList<Server.Commit>? commits, out string message);
}

class CommitDlg : ICommitDlg
{
    readonly ISpellChecker spellChecker;
    IReadOnlyList<Server.Commit>? commits;
    UITextView message = null!;

    internal CommitDlg(ISpellChecker spellChecker)
    {
        this.spellChecker = spellChecker;
    }

    public bool Show(IViewRepo repo, bool isAmend, IReadOnlyList<Server.Commit>? commits, out string commitMessage)
    {
        this.commits = commits;
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

        var dlg = new UIDialog(title, 74, 18, (key) => OnKey(repo, key));

        dlg.AddLabel(1, 0, $"{title} {Changes(filesCount, isAmend)} on '{branchName}':");
        var subject = dlg.AddInputField(1, 2, 50, subjectPart, InputMarkers.Both, spellChecker);

        message = dlg.AddMultiLineInputView(1, 4, 70, 10, messagePart, spellChecker);
        dlg.Validate(() => GetMessage(subject, message) != "", "Empty commit message");

        dlg.ShowOkCancel(subject);

        commitMessage = GetMessage(subject, message);
        return dlg.IsOK;
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
