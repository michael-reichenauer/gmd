using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace gmd.Git;

// A remote command that failed to log in: git, or the ssh it runs, wanted something only the user
// can give, a passphrase, a password or whether to trust a host, and it was not given, or the remote
// refused what it was given. Git gets no terminal to ask on (Cmd.NeverAskOnTheTerminal); gmd asks in
// a dialog instead (Askpass), but not for a command run in the background, nor when the dialog was
// cancelled, and not at all when the askpass cannot reach gmd. The message says what to do, and the
// error it wraps is what git said, which says the same less helpfully.
class LoginError : Error
{
    public LoginError(
        string message,
        Error inner,
        LoginFailure failure = LoginFailure.Failed,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : base(message, inner, memberName, sourceFilePath, sourceLineNumber)
    {
        Failure = failure;
    }

    public LoginFailure Failure { get; }

    // The login error of a remote command that failed to log in, or the result as it was
    public static Result ToLogin(Result result) =>
        result is CmdError e && Advice(e.ErrorOutput) is var (advice, failure)
            ? new LoginError(advice, e, failure)
            : result;

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

    // What to do about what git said, and what failed, or null when it was no login that failed. A
    // line gmd wrote as the askpass program is the surest sign, since it names what was wanted, and
    // says whether the question was cancelled, not asked, or could not be; the other cases are what
    // git and ssh say when a question could not be asked at all, or was answered and refused.
    internal static (string Advice, LoginFailure Failure)? Advice(string gitError)
    {
        if (Asked(gitError, Askpass.CancelledMarker) is not null)
            return ("The login was cancelled", LoginFailure.Cancelled);
        if (Asked(gitError, Askpass.NotAskedMarker) is string notAsked)
            return (AdviceOnNotAsked(notAsked), LoginFailure.NotAsked);
        if (Asked(gitError, Askpass.Marker) is string question)
            return (AdviceOn(question), LoginFailure.Failed);

        if (gitError.Contains("terminal prompts disabled"))
            return (UserNameAndPassword, LoginFailure.Failed);
        if (gitError.Contains("Host key verification failed"))
            return (UntrustedHost(""), LoginFailure.Failed);
        if (gitError.Contains("Permission denied (publickey"))
            return (
                "The remote refused the ssh key: check that the server knows the key, and that the ssh agent "
                    + "has it (ssh-add -l) or its passphrase was typed right",
                LoginFailure.Failed
            );
        if (gitError.Contains("Authentication failed for"))
            return (
                "The remote refused the user name and password: most hosts, GitHub among them, want a token "
                    + "as the password, and a git credential helper may have stored an old one",
                LoginFailure.Failed
            );

        return null;
    }

    // The question after a marker the askpass wrote, or null when it wrote none
    static string? Asked(string gitError, string marker)
    {
        var index = gitError.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : gitError[(index + marker.Length)..];
    }

    // A command run in the background, which never asks, wanted a login. Said on the status line,
    // and the user asks for the login by fetching.
    static string AdviceOnNotAsked(string question)
    {
        if (question.Contains("authenticity of host") || question.Contains("continue connecting"))
        {
            var host = Regex.Match(question, @"host '([^' ]+)") is { Success: true } m ? $" {m.Groups[1].Value}" : "";
            return $"The host{host} is not trusted yet: r fetches and asks whether to trust it";
        }
        return "The remote wants a login: r fetches and asks for it";
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

// Why a login failed: it was asked and failed, or could not be asked, it was cancelled, or it was not
// asked, since the command ran in the background
enum LoginFailure
{
    Failed,
    Cancelled,
    NotAsked,
}
