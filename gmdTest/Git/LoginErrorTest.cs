using gmd.Git;

namespace gmdTest.Git;

// What gmd says when a remote command fails to log in. Git gets no terminal to ask on, so it fails
// instead, and its error output is all there is to go by: the question gmd was asked as the askpass
// program (Askpass), or what git and ssh say when a question could not be asked, or was refused.
// The outputs below are git's and OpenSSH's own.
[TestClass]
public class LoginErrorTest
{
    const string ReadFromRemote = """
        fatal: Could not read from remote repository.

        Please make sure you have the correct access rights
        and the repository exists.
        """;

    [TestMethod]
    public void TestAPassphraseNamesTheKeyToAdd()
    {
        var advice = LoginError.Advice(
            $"""
            gmd cannot ask: Enter passphrase for key '/home/anna/.ssh/id_ed25519':
            git@github.com: Permission denied (publickey).
            {ReadFromRemote}
            """
        );

        Assert.AreEqual(
            "The ssh key needs its passphrase, which gmd cannot ask for: "
                + "add the key to the ssh agent, ssh-add /home/anna/.ssh/id_ed25519",
            advice
        );
    }

    // ssh asks whether to trust a host it has not met in a question of four lines, and the host is
    // the first thing in it
    [TestMethod]
    public void TestAnUntrustedHostIsNamed()
    {
        var advice = LoginError.Advice(
            $"""
            gmd cannot ask: The authenticity of host 'github.com (140.82.121.4)' can't be established.
            ED25519 key fingerprint is SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU.
            This key is not known by any other names.
            Are you sure you want to continue connecting (yes/no/[fingerprint])?
            Host key verification failed.
            {ReadFromRemote}
            """
        );

        Assert.AreEqual(
            "The host github.com is not trusted yet, and gmd cannot ask: trust it once in a terminal, ssh github.com",
            advice
        );
    }

    // Git asks SSH_ASKPASS for a user name over https too, when there is no GIT_ASKPASS, and then
    // tries the terminal, which GIT_TERMINAL_PROMPT=0 has closed. Without gmd as the askpass, e.g.
    // with a GIT_ASKPASS of the user's that gave up, only the second half is there.
    [TestMethod]
    [DataRow(
        """
            gmd cannot ask: Username for 'https://github.com':
            error: unable to read askpass response from '/home/anna/gmd/gmd'
            fatal: could not read Username for 'https://github.com': terminal prompts disabled
            """
    )]
    [DataRow("fatal: could not read Username for 'https://github.com': terminal prompts disabled")]
    public void TestAUserNameAndPasswordNeedACredentialHelper(string gitError)
    {
        Assert.AreEqual(
            "The remote needs a user name and password, which gmd cannot ask for: "
                + "set up a git credential helper, e.g. Git Credential Manager",
            LoginError.Advice(gitError)
        );
    }

    [TestMethod]
    public void TestAnSshPasswordNeedsAKeyInstead()
    {
        var advice = LoginError.Advice(
            "gmd cannot ask: git@example.com's password: \nPermission denied, please try again."
        );

        Assert.AreEqual(
            "The remote asks for an ssh password, which gmd cannot ask for: use an ssh key in the ssh agent instead",
            advice
        );
    }

    // A question that is none of the above is quoted, its first line
    [TestMethod]
    public void TestAnyOtherQuestionIsQuoted()
    {
        var advice = LoginError.Advice("gmd cannot ask: Verification code: \nPermission denied.");

        Assert.AreEqual(
            "Git asked 'Verification code:', which gmd cannot ask: run the command once in a terminal",
            advice
        );
    }

    // Asked nothing, but refused: no key the server knows, or a stored login that is out of date
    [TestMethod]
    public void TestARefusedLoginSaysWhatToCheck()
    {
        Assert.AreEqual(
            "The remote refused the ssh key: check that the key is in the ssh agent (ssh-add -l) and known to the server",
            LoginError.Advice($"git@github.com: Permission denied (publickey).\n{ReadFromRemote}")
        );
        Assert.AreEqual(
            "The remote refused the stored login: update the password or token in the git credential helper",
            LoginError.Advice(
                "remote: Invalid username or password.\nfatal: Authentication failed for 'https://github.com/a/b.git/'"
            )
        );
    }

    [TestMethod]
    public void TestOtherFailuresAreNoLogin()
    {
        Assert.IsNull(
            LoginError.Advice("fatal: unable to access 'https://x.invalid/': Could not resolve host: x.invalid")
        );
        Assert.IsNull(LoginError.Advice(" ! [rejected]        main -> main (non-fast-forward)"));
    }

    // A command wraps what failed under it ("Failed to push ..."), and what is shown is its message
    // and the advice, not git's dozen lines and the command line after them
    [TestMethod]
    public void TestTheTextShownEndsWithTheAdvice()
    {
        var git = new CmdError(
            new CmdResult("git push", 128, "", $"git@github.com: Permission denied (publickey).\n{ReadFromRemote}")
        );
        var login = AssertError(LoginError.ToLogin(git));
        var pushError = new Error("Failed to push branch:\nmain", login);

        Assert.AreSame(login, LoginError.Find(pushError));
        Assert.AreEqual(
            """
            Failed to push branch:
            main,
            The remote refused the ssh key: check that the key is in the ssh agent (ssh-add -l) and known to the server
            """,
            LoginError.Text(pushError)
        );
    }

    [TestMethod]
    public void TestAnyOtherErrorIsShownWhole()
    {
        var git = new CmdError(new CmdResult("git push", 1, "", "error: failed to push some refs"));
        var pushError = new Error("Failed to push branch:\nmain", git);

        Assert.AreSame(git, AssertError(LoginError.ToLogin(git)), "Not a login, so left as it was");
        Assert.IsNull(LoginError.Find(pushError));
        Assert.AreEqual(pushError.AllMessages(), LoginError.Text(pushError));
    }
}
