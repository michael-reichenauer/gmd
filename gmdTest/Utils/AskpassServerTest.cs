using System.IO.Pipes;
using System.Text;

namespace gmdTest.Utils;

// The channel the askpass of git asks gmd on: a real named pipe, which is a Unix domain socket on
// Linux and macOS, with an askpass played by the test. Each test starts a server of its own, which
// is left listening, since the server is for the life of gmd and has no stop.
[TestClass]
[TestCategory("Integration")]
public class AskpassServerTest
{
    readonly List<AskpassQuestion> asked = [];

    [TestCleanup]
    public void Cleanup() => Askpass.Channel = null;

    (string Name, string Token) Start(Func<AskpassQuestion, string?> answer)
    {
        new AskpassServer().Start(q =>
        {
            lock (asked)
                asked.Add(q);
            return Task.FromResult(answer(q));
        });
        return Askpass.Channel ?? throw new AssertFailedException("No channel");
    }

    // What the askpass does, the request sent and the answer read, short of printing it
    static (bool IsAnswered, string Answer) Ask(string name, Askpass.Request request)
    {
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        pipe.Connect(5000);
        using var writer = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);
        using var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
        Askpass.WriteRequest(writer, request);
        return (reader.ReadBoolean(), reader.ReadString());
    }

    [TestMethod]
    public void TestTheQuestionIsAskedAndAnswered()
    {
        var (name, token) = Start(_ => "secret");

        var answered = Ask(name, new(token, "1", "git push origin main", "", "Enter passphrase for key 'k': "));

        Assert.AreEqual((true, "secret"), answered);
        Assert.AreEqual(
            new AskpassQuestion("Enter passphrase for key 'k': ", "git push origin main", ""),
            asked.Single()
        );
    }

    // Several askpasses can wait at once, e.g. two commands asking side by side, each answered
    [TestMethod]
    public async Task TestQuestionsAskedAtOnceAreEachAnswered()
    {
        var (name, token) = Start(q => q.Question.ToUpperInvariant());

        var answers = await Task.WhenAll(
            Enumerable.Range(0, 3).Select(i => Task.Run(() => Ask(name, new(token, $"{i}", "git fetch", "", $"q{i}"))))
        );

        CollectionAssert.AreEqual(new[] { (true, "Q0"), (true, "Q1"), (true, "Q2") }, answers);
    }

    // Only an askpass of the git gmd started knows the token, so no other program can make gmd show
    // a dialog: the question is not asked, and the connection closed
    [TestMethod]
    public void TestAWrongTokenIsNotAsked()
    {
        var (name, _) = Start(_ => "secret");

        Assert.ThrowsExactly<EndOfStreamException>(() => Ask(name, new("wrong", "1", "git fetch", "", "Password: ")));
        Assert.AreEqual(0, asked.Count);
    }

    // Cancel answers nothing, and the questions of the same command after it are not asked again,
    // which ssh would, trying the next key or asking once more
    [TestMethod]
    public void TestACancelledCommandIsNotAskedAgain()
    {
        var (name, token) = Start(_ => null);

        Assert.AreEqual((false, ""), Ask(name, new(token, "1", "git push", "", "Enter passphrase for key 'a': ")));
        Assert.AreEqual((false, ""), Ask(name, new(token, "1", "git push", "", "Enter passphrase for key 'b': ")));
        Assert.AreEqual(1, asked.Count, "The second question of the command is not asked");

        Assert.AreEqual((false, ""), Ask(name, new(token, "2", "git fetch", "", "Enter passphrase for key 'a': ")));
        Assert.AreEqual(2, asked.Count, "Another command is asked");
    }
}
