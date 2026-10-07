using gmd.Cui.Common;

namespace gmd.Cui;

interface ILoginDlg
{
    // Asks the user what git or ssh asked, on the UI thread, from any thread: the answer, or null
    // for Cancel or No
    Task<string?> AskAsync(AskpassQuestion question);
}

// The dialog git's and ssh's questions are asked in, a passphrase, a password, a user name or
// whether to trust a host, brought by the askpass of the git command that asks (AskpassServer). The
// command waits for the answer, so this comes up over whatever it was doing, and takes keys even
// though input is otherwise stopped while git runs (UI.RunDialog). Nothing typed is logged or kept:
// a credential helper, if one is set up, keeps what git was given, as it does for a terminal prompt.
class LoginDlg : ILoginDlg
{
    readonly IStatusLine status;

    internal LoginDlg(IStatusLine status)
    {
        this.status = status;
    }

    public Task<string?> AskAsync(AskpassQuestion question)
    {
        var answered = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        UI.Post(() =>
        {
            try
            {
                answered.SetResult(Ask(question));
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Warn($"Login dialog failed: {e.Message}");
                answered.SetResult(null);
            }
        });
        return answered.Task;
    }

    string? Ask(AskpassQuestion asked)
    {
        // A notice only, e.g. to touch a security key, which ssh takes down itself once it is done
        if (asked.Kind == "none")
        {
            status.Info(asked.Question.ReplaceLineEndings(" "));
            return "";
        }

        var question = LoginQuestion.Of(asked);
        if (question.IsYesNo)
        {
            // No is the default: trusting a host is a decision, as ssh makes it one by asking for 'yes'
            var text = string.Join('\n', question.Lines);
            var hint = question.Hint == "" ? "" : $"\n\n{question.Hint}";
            var askedBy = LoginQuestion.AskedBy(asked.Command);
            var yesNo = UI.InfoMessage(question.Title, $"{text}{hint}\n\n{askedBy}", 1, ["Yes", "No"]);
            return yesNo == 0 ? question.YesAnswer : null;
        }

        // The question, the field to type in below it, then the hint and the command that asks
        var y = question.Lines.Count + 1;
        var hints = question.HintLines;
        var dlg = new UIDialog(question.Title, LoginQuestion.Width + 4, y + hints.Count + 7);
        for (int i = 0; i < question.Lines.Count; i++)
            dlg.AddLabel(1, i, question.Lines[i]);

        var field = dlg.AddTextField(2, y, LoginQuestion.Width - 2);
        field.Secret = question.IsSecret;
        for (int i = 0; i < hints.Count; i++)
            dlg.AddLabel(1, y + 2 + i, Text.Dark(hints[i]));
        dlg.AddLabel(1, y + 2 + hints.Count, Text.Dark(LoginQuestion.AskedBy(asked.Command)));

        // The answer as typed, not trimmed: a passphrase may well end in a space
        return dlg.ShowOkCancel(field) ? field.RawText : null;
    }
}
