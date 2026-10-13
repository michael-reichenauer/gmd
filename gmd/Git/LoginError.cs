using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace gmd.Git;

// A remote command that failed to log in: git, or the ssh it runs, wanted something only the user
// can give, a passphrase, a password or whether to trust a host, and it was not given, or the remote
// refused what it was given. Git gets no terminal to ask on (Cmd.NeverAskOnTheTerminal); the askpass
// asks instead (Askpass), but not for a command run in the background, nor when the question was
// cancelled, and not at all when the askpass cannot reach whoever asks. What failed is told from
// what git said (Problem, Detail, Failure), and the error it wraps is what git said. What to do
// about it is for the UI to say, in its own words.
class LoginError : Error
{
    public LoginError(
        LoginProblem problem,
        string detail,
        LoginFailure failure,
        Error inner,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : base(Describe(problem, detail, failure), inner, memberName, sourceFilePath, sourceLineNumber)
    {
        Problem = problem;
        Detail = detail;
        Failure = failure;
    }

    public LoginProblem Problem { get; }

    // The host of an untrusted host, the key file of a passphrase, or the question of any other
    // question, when git or ssh said it; else ""
    public string Detail { get; }

    public LoginFailure Failure { get; }

    // The login error of a remote command that failed to log in, or the result as it was
    public static Result ToLogin(Result result) =>
        result is CmdError e && Parse(e.ErrorOutput) is LoginIssue issue
            ? new LoginError(issue.Problem, issue.Detail, issue.Failure, e)
            : result;

    // The login error in an error, however deeply a command wrapped it ("Failed to push ...")
    public static LoginError? Find(Error error) =>
        error as LoginError ?? (error.Inner is Error inner ? Find(inner) : null);

    // What failed to log in, from what git said, or null when it was no login that failed. A line the
    // askpass wrote is the surest sign, since it names what was wanted, and says whether the question
    // was cancelled, not asked, or could not be; the other cases are what git and ssh say when a
    // question could not be asked at all, or was answered and refused.
    internal static LoginIssue? Parse(string gitError)
    {
        if (Asked(gitError, Askpass.CancelledMarker) is string cancelled)
            return Of(LoginFailure.Cancelled, cancelled);
        if (Asked(gitError, Askpass.NotAskedMarker) is string notAsked)
            return Of(LoginFailure.NotAsked, notAsked);
        if (Asked(gitError, Askpass.Marker) is string question)
            return Of(LoginFailure.Failed, question);

        if (gitError.Contains("terminal prompts disabled"))
            return new LoginIssue(LoginFailure.Failed, LoginProblem.UserNameAndPassword, "");
        if (gitError.Contains("Host key verification failed"))
            return new LoginIssue(LoginFailure.Failed, LoginProblem.UntrustedHost, "");
        if (gitError.Contains("Permission denied (publickey"))
            return new LoginIssue(LoginFailure.Failed, LoginProblem.KeyRefused, "");
        if (gitError.Contains("Authentication failed for"))
            return new LoginIssue(LoginFailure.Failed, LoginProblem.PasswordRefused, "");

        return null;
    }

    // The question after a marker the askpass wrote, or null when it wrote none
    static string? Asked(string gitError, string marker)
    {
        var index = gitError.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : gitError[(index + marker.Length)..];
    }

    // What a question asked for, by the words of ssh and git
    static LoginIssue Of(LoginFailure failure, string question)
    {
        if (question.Contains("passphrase", StringComparison.OrdinalIgnoreCase))
            return new(failure, LoginProblem.Passphrase, Match(question, @"key '([^']+)'"));
        if (question.Contains("authenticity of host") || question.Contains("continue connecting"))
            return new(failure, LoginProblem.UntrustedHost, Match(question, @"host '([^' ]+)"));
        if (question.StartsWith("Username for") || question.StartsWith("Password for"))
            return new(failure, LoginProblem.UserNameAndPassword, "");
        if (question.Contains("password", StringComparison.OrdinalIgnoreCase))
            return new(failure, LoginProblem.SshPassword, "");

        return new(failure, LoginProblem.OtherQuestion, question.Split('\n')[0].Trim());
    }

    static string Match(string text, string pattern) =>
        Regex.Match(text, pattern) is { Success: true } m ? m.Groups[1].Value : "";

    // What failed, said as a fact, with nothing of what to do about it
    static string Describe(LoginProblem problem, string detail, LoginFailure failure) =>
        failure == LoginFailure.Cancelled
            ? "The login was cancelled"
            : problem switch
            {
                LoginProblem.Passphrase => "The ssh key needs its passphrase",
                LoginProblem.UntrustedHost => detail != ""
                    ? $"The host {detail} is not trusted yet"
                    : "The host of the remote is not trusted yet",
                LoginProblem.UserNameAndPassword => "The remote needs a user name and password",
                LoginProblem.SshPassword => "The remote asks for an ssh password",
                LoginProblem.KeyRefused => "The remote refused the ssh key",
                LoginProblem.PasswordRefused => "The remote refused the user name and password",
                _ => $"Git asked '{detail}'",
            };
}

// What failed to log in: the failure, what was wanted or refused, and the detail git or ssh gave
record LoginIssue(LoginFailure Failure, LoginProblem Problem, string Detail);

// Why a login failed: it was asked and failed, or could not be asked, it was cancelled, or it was not
// asked, since the command ran in the background
public enum LoginFailure
{
    Failed,
    Cancelled,
    NotAsked,
}

// What a login that failed wanted, or what the remote refused
public enum LoginProblem
{
    Passphrase, // The passphrase of an ssh key
    UntrustedHost, // Whether to trust a host ssh has not met before
    UserNameAndPassword, // A user name and password, over https
    SshPassword, // An ssh password, where a key is the usual way
    OtherQuestion, // Any other question
    KeyRefused, // The remote refused the ssh key
    PasswordRefused, // The remote refused the user name and password
}
