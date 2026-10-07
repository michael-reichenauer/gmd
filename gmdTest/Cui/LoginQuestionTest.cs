using gmd.Cui;

namespace gmdTest.Cui;

// How the login dialog shows what git or ssh asked: a host to trust by Yes or No, a passphrase or a
// password typed hidden, a user name typed as it is, and a hint that saves asking again. The
// questions are git's and OpenSSH's own.
[TestClass]
public class LoginQuestionTest
{
    static LoginQuestion Of(string question, string kind = "") =>
        LoginQuestion.Of(new AskpassQuestion(question, "git push --porcelain origin main", kind));

    [TestMethod]
    public void TestAPassphraseIsHiddenAndNamesTheKeyToAdd()
    {
        var q = Of("Enter passphrase for key '/home/anna/.ssh/id_ed25519': ");

        Assert.AreEqual(("Passphrase", false, true), (q.Title, q.IsYesNo, q.IsSecret));
        Assert.AreEqual("Tip: ssh-add /home/anna/.ssh/id_ed25519 in a terminal, and it is not asked again", q.Hint);
        CollectionAssert.AreEqual(
            new[] { "Enter passphrase for key '/home/anna/.ssh/id_ed25519':" },
            q.Lines.ToArray()
        );
    }

    // ssh asks whether to trust a host it has not met in four lines, answered 'yes'
    [TestMethod]
    public void TestAnUntrustedHostIsYesOrNo()
    {
        var q = Of(
            """
            The authenticity of host 'github.com (140.82.121.4)' can't be established.
            ED25519 key fingerprint is SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU.
            This key is not known by any other names.
            Are you sure you want to continue connecting (yes/no/[fingerprint])?
            """
        );

        Assert.AreEqual(("Trust Host", true, "yes"), (q.Title, q.IsYesNo, q.YesAnswer));
        CollectionAssert.AreEqual(
            new[]
            {
                "The authenticity of host 'github.com (140.82.121.4)' can't be",
                "established.",
                "ED25519 key fingerprint is",
                "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU.",
                "This key is not known by any other names.",
                "Are you sure you want to continue connecting",
                "(yes/no/[fingerprint])?",
            },
            q.Lines.ToArray()
        );
    }

    // ssh's own confirm is answered by the exit code, so Yes answers nothing
    [TestMethod]
    public void TestAConfirmIsYesOrNo()
    {
        var q = Of("Allow use of key /home/anna/.ssh/id_ed25519?", "confirm");

        Assert.AreEqual(("Confirm", true, ""), (q.Title, q.IsYesNo, q.YesAnswer));
    }

    // Git asks for a user name and a password over https, and most hosts want a token as the latter
    [TestMethod]
    [DataRow("Username for 'https://github.com': ", "User Name", false)]
    [DataRow("Password for 'https://anna@github.com': ", "Password", true)]
    public void TestAUserNameIsShownAndAPasswordHidden(string question, string title, bool isSecret)
    {
        var q = Of(question);

        Assert.AreEqual((title, false, isSecret), (q.Title, q.IsYesNo, q.IsSecret));
        Assert.AreEqual("Most hosts, GitHub among them, want a token as the password", q.Hint);
    }

    // An ssh password, and anything else, e.g. a one time code, is typed hidden, to be safe
    [TestMethod]
    [DataRow("git@example.com's password: ", "Password")]
    [DataRow("Verification code: ", "Git Asks")]
    public void TestAnythingElseIsHidden(string question, string title)
    {
        var q = Of(question);

        Assert.AreEqual((title, false, true, ""), (q.Title, q.IsYesNo, q.IsSecret, q.Hint));
    }

    // The command that asks, without its options, which say nothing to the user
    [TestMethod]
    public void TestAskedByIsTheCommandWithoutItsOptions()
    {
        Assert.AreEqual(
            "Asked by: git push origin refs/heads/main:refs/heads/main",
            LoginQuestion.AskedBy("git push --porcelain origin refs/heads/main:refs/heads/main")
        );
        Assert.AreEqual(LoginQuestion.Width, LoginQuestion.AskedBy($"git fetch {new string('x', 100)}").Length);
    }
}
