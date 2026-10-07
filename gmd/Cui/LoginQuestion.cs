using System.Text.RegularExpressions;

namespace gmd.Cui;

// What git or ssh asked, as the login dialog shows it: the title, the question in lines that fit the
// dialog, whether it is answered by Yes or No or by typing, and then whether what is typed is hidden,
// and a hint that saves asking again. The questions are git's and OpenSSH's own, in English
// (Cmd.InEnglish), so they are told apart by their words.
//
// The decision with no view, so it is tested without a terminal; LoginDlg shows it.
record LoginQuestion(
    string Title,
    IReadOnlyList<string> Lines,
    bool IsYesNo,
    string YesAnswer, // What Yes answers: 'yes' to ssh's question, nothing to its confirm
    bool IsSecret,
    string Hint
)
{
    public const int Width = 66;

    public static LoginQuestion Of(AskpassQuestion asked)
    {
        var question = asked.Question;
        var lines = Wrap(question, Width);

        // ssh's own confirm, e.g. to use a key the agent was told to confirm, answered by the exit code
        if (asked.Kind == "confirm")
            return new("Confirm", lines, true, "", false, "");
        if (question.Contains("(yes/no"))
        {
            var isHost = question.Contains("authenticity of host");
            return new(
                isHost ? "Trust Host" : "Git Asks",
                lines,
                true,
                "yes",
                false,
                isHost ? "Yes adds the host to ~/.ssh/known_hosts, as ssh does, so it is not asked again" : ""
            );
        }

        if (question.Contains("passphrase", StringComparison.OrdinalIgnoreCase))
        {
            var key = Regex.Match(question, @"key '([^']+)'");
            var add = key.Success ? $"ssh-add {key.Groups[1].Value}" : "ssh-add";
            return new("Passphrase", lines, false, "", true, $"Tip: {add} in a terminal, and it is not asked again");
        }
        if (question.StartsWith("Username for"))
            return new("User Name", lines, false, "", false, TokenHint);
        if (question.StartsWith("Password for"))
            return new("Password", lines, false, "", true, TokenHint);
        if (question.Contains("password", StringComparison.OrdinalIgnoreCase))
            return new("Password", lines, false, "", true, "");

        // Anything else, e.g. a security key's PIN or a one time code, is typed hidden, to be safe
        return new("Git Asks", lines, false, "", true, "");
    }

    const string TokenHint = "Most hosts, GitHub among them, want a token as the password";

    // The hint in lines that fit the dialog, none when there is no hint
    public IReadOnlyList<string> HintLines => Hint == "" ? [] : Wrap(Hint, Width);

    // Which command asks, e.g. 'Asked by: git push origin main', with the options left out, which
    // say nothing to the user, and cut to the width
    public static string AskedBy(string command)
    {
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !w.StartsWith('-'));
        var text = $"Asked by: {string.Join(' ', words)}";
        return text.Length > Width ? $"{text[..(Width - 1)]}…" : text;
    }

    // The question's own lines, each broken at spaces to fit the width, with no empty lines at the
    // ends
    internal static IReadOnlyList<string> Wrap(string text, int width)
    {
        List<string> lines = [];
        foreach (var line in text.ReplaceLineEndings("\n").Trim('\n').Split('\n'))
        {
            var rest = line.TrimEnd();
            while (rest.Length > width)
            {
                var cut = rest.LastIndexOf(' ', width);
                cut = cut <= 0 ? width : cut;
                lines.Add(rest[..cut].TrimEnd());
                rest = rest[cut..].TrimStart();
            }
            lines.Add(rest);
        }
        return lines;
    }
}
