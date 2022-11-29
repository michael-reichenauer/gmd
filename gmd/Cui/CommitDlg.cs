using gmd.Cui.Common;
using NStack;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDlg
{
    bool Show(IRepo repo, out string message);
}

class CommitDlg : ICommitDlg
{
    public bool Show(IRepo repo, out string message)
    {
        message = "";
        bool isOk = false;

        (string subjectText, string messageText) = ParseMessage(repo);

        if (repo.Status.IsOk)
        {
            return false;
        }

        var commit = repo.Commits[0];
        int filesCount = repo.Status.ChangesCount;
        string branchName = commit.BranchName;

        Label infoLabel = new Label(1, 0, $"Commit {filesCount} changes on '{branchName}':")
        { ColorScheme = ColorSchemes.Label };

        TextField subjectField = new TextField(1, 2, 50, "") { Text = subjectText, ColorScheme = ColorSchemes.TextField };
        Label sep1 = new Label(0, 3, "└" + new string('─', 49) + "┘") { ColorScheme = ColorSchemes.Indicator };

        MessageTextView messageView = new MessageTextView()
        { X = 1, Y = 4, Width = 69, Height = 10, Text = messageText, ColorScheme = ColorSchemes.TextField };
        Label sep3 = new Label(0, 14, "└" + new string('─', 67) + "┘") { ColorScheme = ColorSchemes.Indicator };

        Button okButton = Buttons.OK(true, () =>
        {
            if (GetMessage(subjectField, messageView) == "")
            {
                UI.ErrorMessage("Empty commit message");
                subjectField!.SetFocus();
                return false;
            }
            isOk = true;
            Application.RequestStop();
            return true;
        });

        Dialog dialog = new CustomDialog("Commit", 72, 18, new[] { okButton, Buttons.Cancel() },
            (key) => OnKey(repo, key))
        {
            Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded, BorderBrush = Color.Blue },
            ColorScheme = ColorSchemes.Dialog,
        };
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(infoLabel, subjectField, sep1, messageView, sep3);

        subjectField.SetFocus();
        UI.ShowCursor();
        UI.RunDialog(dialog);

        message = GetMessage(subjectField, messageView);
        return isOk;
    }

    private bool OnKey(IRepo repo, Key key)
    {
        if (key == (Key.D | Key.CtrlMask))
        {
            repo.Cmd.ShowUncommittedDiff();
            return true;
        }

        return false;
    }


    (string, string) ParseMessage(IRepo repo)
    {
        string msg = repo.Status.MergeMessage;

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
        string message = "";
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


    string SubjectText(TextField subjectField) => subjectField!.Text.ToString()?.Trim() ?? "";


    string GetMessage(TextField subjectField, MessageTextView messageView)
    {
        string subjectText = SubjectText(subjectField);
        string msgText = messageView!.Text.ToString()?.TrimEnd() ?? "";
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

    class MessageTextView : TextView
    {
        public override bool ProcessKey(KeyEvent keyEvent)
        {
            if (keyEvent.Key == Key.Tab)
            {   // Ensure tab sets focus on next control and not insert tab in text
                return false;
            }
            return base.ProcessKey(keyEvent);
        }

        public override Border Border { get => new Border() { }; set => base.Border = value; }
    }

    class CustomDialog : Dialog
    {
        private readonly Func<Key, bool> onKey;

        public CustomDialog(ustring title, int width, int height, Button[] buttons, Func<Key, bool> onKey)
        : base(title, width, height, buttons)
        {
            this.onKey = onKey;
        }

        public override bool ProcessHotKey(KeyEvent keyEvent) => onKey(keyEvent.Key);
    }
}

