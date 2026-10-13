using gmd.Git;

namespace gmdTest.Git;

// What failed when a remote command failed to log in, read from git's error output: what the
// askpass wrote, a cancel, a command that was not asked, or one that could not be, or what git and
// ssh say when a question could not be asked, or was refused. The outputs are git's and OpenSSH's
// own. What gmd says about each is LoginAdviceTest's.
[TestClass]
public class LoginErrorTest
{
    const string ReadFromRemote = """
        fatal: Could not read from remote repository.

        Please make sure you have the correct access rights
        and the repository exists.
        """;

    [TestMethod]
    [DataRow(
        "gmd cannot ask: Enter passphrase for key '/home/anna/.ssh/id_ed25519':",
        LoginFailure.Failed,
        LoginProblem.Passphrase,
        "/home/anna/.ssh/id_ed25519"
    )]
    [DataRow(
        "gmd cannot ask: The authenticity of host 'github.com (140.82.121.4)' can't be established.",
        LoginFailure.Failed,
        LoginProblem.UntrustedHost,
        "github.com"
    )]
    [DataRow(
        "gmd cannot ask: Username for 'https://github.com':",
        LoginFailure.Failed,
        LoginProblem.UserNameAndPassword,
        ""
    )]
    [DataRow(
        "gmd cannot ask: git@example.com's password: \nPermission denied, please try again.",
        LoginFailure.Failed,
        LoginProblem.SshPassword,
        ""
    )]
    [DataRow(
        "gmd cannot ask: Verification code: \nPermission denied.",
        LoginFailure.Failed,
        LoginProblem.OtherQuestion,
        "Verification code:"
    )]
    [DataRow(
        "gmd login cancelled: Enter passphrase for key '/home/anna/.ssh/id_ed25519':\nPermission denied (publickey).",
        LoginFailure.Cancelled,
        LoginProblem.Passphrase,
        "/home/anna/.ssh/id_ed25519"
    )]
    [DataRow(
        "gmd did not ask: The authenticity of host 'github.com (140.82.121.4)' can't be established.",
        LoginFailure.NotAsked,
        LoginProblem.UntrustedHost,
        "github.com"
    )]
    [DataRow(
        "fatal: could not read Username for 'https://github.com': terminal prompts disabled",
        LoginFailure.Failed,
        LoginProblem.UserNameAndPassword,
        ""
    )]
    [DataRow("Host key verification failed.", LoginFailure.Failed, LoginProblem.UntrustedHost, "")]
    [DataRow("git@github.com: Permission denied (publickey).", LoginFailure.Failed, LoginProblem.KeyRefused, "")]
    [DataRow(
        "fatal: Authentication failed for 'https://github.com/a/b.git/'",
        LoginFailure.Failed,
        LoginProblem.PasswordRefused,
        ""
    )]
    public void TestWhatFailedIsReadFromWhatGitSaid(
        string gitError,
        LoginFailure failure,
        LoginProblem problem,
        string detail
    )
    {
        Assert.AreEqual(new LoginIssue(failure, problem, detail), LoginError.Parse($"{gitError}\n{ReadFromRemote}"));
    }

    [TestMethod]
    public void TestOtherFailuresAreNoLogin()
    {
        Assert.IsNull(
            LoginError.Parse("fatal: unable to access 'https://x.invalid/': Could not resolve host: x.invalid")
        );
        Assert.IsNull(LoginError.Parse(" ! [rejected]        main -> main (non-fast-forward)"));
    }

    // The error says what failed as a fact, with nothing of what to do about it, which is the UI's
    // to say, and wraps what git said; it is found however deeply a command wrapped it
    [TestMethod]
    public void TestALoginErrorSaysWhatFailedAndWrapsWhatGitSaid()
    {
        var git = new CmdError(new CmdResult("git push", 128, "", "git@github.com: Permission denied (publickey)."));

        var login = (LoginError)AssertError(LoginError.ToLogin(git));
        var pushError = new Error("Failed to push branch", login);

        Assert.AreEqual("The remote refused the ssh key", login.Message);
        Assert.AreSame(git, login.Inner);
        Assert.AreSame(login, LoginError.Find(pushError));
    }

    [TestMethod]
    public void TestAnyOtherErrorIsLeftAsItWas()
    {
        var git = new CmdError(new CmdResult("git push", 1, "", "error: failed to push some refs"));

        Assert.AreSame(git, AssertError(LoginError.ToLogin(git)));
        Assert.IsNull(LoginError.Find(new Error("Failed to push branch", git)));
    }
}
