
using Terminal.Gui;

namespace gmd.Cui;

class CommitDlg
{
    readonly Dialog dialog;
    readonly TextField subjectField;
    readonly MessageTextView messageView;

    internal string Message => MessageText();

    bool isOk = false;
    public CommitDlg(string branchName, int filesCount, string message = "")
    {
        (string subjectText, string messageText) = ParseMessage(message);

        Button okButton = new Button("OK", true);
        okButton.Clicked += () =>
        {
            if (Message == "")
            {
                UI.ErrorMessage("Empty commit message");
                subjectField!.SetFocus();
                return;
            }
            isOk = true;
            Application.RequestStop();
        };
        Button cancelButton = new Button("Cancel", false);
        cancelButton.Clicked += () => Application.RequestStop();

        dialog = new Dialog("Commit", 72, 17, new Button[] { okButton, cancelButton })
        {
            Border = { Effect3D = false }
        };

        var label = new Label(0, 0, $"Commit {filesCount} files on {branchName}:");
        subjectField = new TextField(0, 1, 50, "some text");
        subjectField.Text = subjectText;

        messageView = new MessageTextView() { X = 0, Y = 3, Width = 70, Height = 10 };
        messageView.Text = messageText;

        dialog.Add(label, subjectField, messageView);
        subjectField.SetFocus();
    }

    internal bool Show()
    {
        Application.Run(dialog);
        return isOk;
    }


    (string, string) ParseMessage(string msg)
    {
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


    string SubjectText() => subjectField.Text.ToString()?.Trim() ?? "";


    string MessageText()
    {
        string subjectText = SubjectText();
        string msgText = messageView.Text.ToString()?.TrimEnd() ?? "";
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
    }
}

