using gmd.Git;

namespace gmd.Cui;

// What to do about a login that failed, in gmd's words. The git layer tells what failed (LoginError):
// what was wanted or refused, and whether it was asked, cancelled or not asked at all, since a
// command run in the background never asks. This says what to do about that here: a key to press,
// or what to set up so that gmd need not ask.
static class LoginAdvice
{
    public static string Of(LoginError login) =>
        login.Failure switch
        {
            LoginFailure.Cancelled => "The login was cancelled",
            LoginFailure.NotAsked => NotAsked(login),
            _ => Failed(login),
        };

    // What to show of an error: all its messages, or for a failed login, those of the errors that
    // wrap it and then the advice, leaving out git's own account of it, a dozen lines ending in the
    // command line
    public static string Text(Error error)
    {
        if (LoginError.Find(error) is not LoginError login)
            return error.AllMessages();

        List<string> messages = [];
        for (Error? e = error; e != null && e != login; e = e.Inner)
            messages.Add(e.Message);
        messages.Add(Of(login));
        return string.Join(",\n", messages);
    }

    // A command run in the background, which never asks, wanted a login. Said on the status line,
    // and the user asks for the login by fetching.
    static string NotAsked(LoginError login) =>
        login.Problem == LoginProblem.UntrustedHost
            ? $"The host{(login.Detail != "" ? $" {login.Detail}" : "")} is not trusted yet: r fetches and asks whether to trust it"
            : "The remote wants a login: r fetches and asks for it";

    static string Failed(LoginError login) =>
        login.Problem switch
        {
            LoginProblem.Passphrase =>
                "The ssh key needs its passphrase, which gmd cannot ask for: add the key to the ssh agent, "
                    + (login.Detail != "" ? $"ssh-add {login.Detail}" : "ssh-add"),
            LoginProblem.UntrustedHost => login.Detail != ""
                ? $"The host {login.Detail} is not trusted yet, and gmd cannot ask: trust it once in a terminal, ssh {login.Detail}"
                : "The host of the remote is not trusted yet, and gmd cannot ask: connect to it once with ssh in a terminal",
            LoginProblem.UserNameAndPassword => "The remote needs a user name and password, which gmd cannot ask for: "
                + "set up a git credential helper, e.g. Git Credential Manager",
            LoginProblem.SshPassword =>
                "The remote asks for an ssh password, which gmd cannot ask for: use an ssh key in the ssh agent instead",
            LoginProblem.KeyRefused =>
                "The remote refused the ssh key: check that the server knows the key, and that the ssh agent "
                    + "has it (ssh-add -l) or its passphrase was typed right",
            LoginProblem.PasswordRefused =>
                "The remote refused the user name and password: most hosts, GitHub among them, want a token "
                    + "as the password, and a git credential helper may have stored an old one",
            _ => $"Git asked '{login.Detail}', which gmd cannot ask: run the command once in a terminal",
        };
}
