using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace gmd.Git;

// A remote command that failed to log in: git, or the ssh it runs, wanted something only the user
// can give, a passphrase, a password or whether to trust a host, or the remote refused what it was
// given. Git gets no terminal to ask on (Cmd.NeverAskOnTheTerminal), so such a command fails rather
// than drawing its question over the UI, and this says what to do instead. The message is that
// advice, and the error it wraps is what git said, which says the same less helpfully.
class LoginError : Error
{
    public LoginError(
        string message,
        Error inner,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : base(message, inner, memberName, sourceFilePath, sourceLineNumber) { }

    // The login error of a remote command that failed to log in, or the result as it was
    public static Result ToLogin(Result result) =>
        result is CmdError e && Advice(e.ErrorOutput) is string advice ? new LoginError(advice, e) : result;

    // The login error in an error, however deeply a command wrapped it ("Failed to push ...")
    public static LoginError? Find(Error error) =>
        error as LoginError ?? (error.Inner is Error inner ? Find(inner) : null);

    // What to show of an error: all its messages, or for a failed login, those down to the advice,
    // leaving out git's own account of it, a dozen lines ending in the command line
    public static string Text(Error error)
    {
        if (Find(error) == null)
            return error.AllMessages();

        List<string> messages = [];
        for (Error? e = error; e != null; e = e.Inner)
        {
            messages.Add(e.Message);
            if (e is LoginError)
                break;
        }
        return string.Join(",\n", messages);
    }

    // What to do about what git said, or null when it was no login that failed. A question gmd was
    // asked as the askpass program is the surest sign, since it names what was wanted; the other
    // cases are what git and ssh say when a question could not be asked at all, or was answered
    // and refused.
    internal static string? Advice(string gitError)
    {
        var index = gitError.IndexOf(Askpass.Marker, StringComparison.Ordinal);
        if (index >= 0)
            return AdviceOn(gitError[(index + Askpass.Marker.Length)..]);

        if (gitError.Contains("terminal prompts disabled"))
            return UserNameAndPassword;
        if (gitError.Contains("Host key verification failed"))
            return UntrustedHost("");
        if (gitError.Contains("Permission denied (publickey"))
            return "The remote refused the ssh key: check that the key is in the ssh agent (ssh-add -l) "
                + "and known to the server";
        if (gitError.Contains("Authentication failed for"))
            return "The remote refused the stored login: update the password or token in the git credential helper";

        return null;
    }

    const string UserNameAndPassword =
        "The remote needs a user name and password, which gmd cannot ask for: "
        + "set up a git credential helper, e.g. Git Credential Manager";

    static string AdviceOn(string question)
    {
        if (question.Contains("passphrase", StringComparison.OrdinalIgnoreCase))
        {
            var key = Regex.Match(question, @"key '([^']+)'");
            var add = key.Success ? $"ssh-add {key.Groups[1].Value}" : "ssh-add";
            return $"The ssh key needs its passphrase, which gmd cannot ask for: add the key to the ssh agent, {add}";
        }

        if (question.Contains("authenticity of host") || question.Contains("continue connecting"))
            return UntrustedHost(
                Regex.Match(question, @"host '([^' ]+)") is { Success: true } m ? m.Groups[1].Value : ""
            );

        if (question.StartsWith("Username for") || question.StartsWith("Password for"))
            return UserNameAndPassword;

        if (question.Contains("password", StringComparison.OrdinalIgnoreCase))
            return "The remote asks for an ssh password, which gmd cannot ask for: use an ssh key in the ssh agent instead";

        var asked = question.Split('\n')[0].Trim();
        return $"Git asked '{asked}', which gmd cannot ask: run the command once in a terminal";
    }

    static string UntrustedHost(string host) =>
        host != ""
            ? $"The host {host} is not trusted yet, and gmd cannot ask: trust it once in a terminal, ssh {host}"
            : "The host of the remote is not trusted yet, and gmd cannot ask: connect to it once with ssh in a terminal";
}
