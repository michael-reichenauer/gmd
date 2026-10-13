namespace gmd.Cui.Commits;

// A commit message as the commit and squash dialogs edit it: the subject in a field of its own and
// the body below it, which git keeps as one text with an empty line between the two
static class CommitMessage
{
    // The first line, and the rest after the empty line that follows it, if there is one
    public static (string Subject, string Body) Split(string message)
    {
        if (message.Trim() == "")
            return ("", "");

        var lines = message.Split('\n');
        if (lines.Length == 1)
            return (lines[0], "");

        var bodyStart = lines.Length > 2 && lines[1] == "" ? 2 : 1;
        return (lines[0], string.Join('\n', lines.Skip(bodyStart)));
    }

    // The two as one message, with an empty line between them when there are both
    public static string Join(string subject, string body)
    {
        body = body.TrimEnd();
        if (body.Trim() == "")
            body = "";

        if (subject != "" && body != "")
            return subject + "\n\n" + body;
        return subject != "" ? subject : body;
    }
}
