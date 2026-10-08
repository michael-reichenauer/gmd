using System.IO.Pipes;
using System.Text;

namespace gmd.Utils;

// gmd as the askpass program of the git it runs. Git, and the ssh it runs, ask for a passphrase, a
// password or whether to trust a host on the terminal, and gmd owns the terminal: a question drawn
// there scrolled the UI away, took the keys typed for gmd as its answer, and waited for an Enter that
// the raw terminal sends as '\r'. So Cmd gives git no terminal to ask on, and points ssh at gmd
// instead (Cmd.NeverAskOnTheTerminal), and git as well, which asks SSH_ASKPASS when it has no
// GIT_ASKPASS. Started as one, gmd gets the question as its only argument.
//
// It asks the gmd that started the git, over the channel that gmd listens on (AskpassServer), which
// shows the question in a dialog, and prints the answer on stdout, where ssh and git read it. It
// answers nothing when there is no one to ask: for a command run in the background, which must not
// raise a dialog by itself (NeverAsk), or when the channel cannot be reached. Then it says why on
// stderr, which ssh and git pass on into the error output of the command, where LoginError finds it,
// and exits with 1, which both take as no answer. Cancel in the dialog is said the same way.
static class Askpass
{
    // Set in the environment of every git process, and so of the ssh it runs and of the askpass that
    // runs, so that gmd started as one knows it is one rather than a gmd to show a repository in
    public const string Variable = "GMD_ASKPASS";

    // The channel to ask on, and the token to show, set only for a command that may ask, and the
    // command itself, for the dialog to say what asks, with an id, since its own questions after a
    // Cancel are not asked again
    public const string ChannelVariable = "GMD_ASKPASS_CHANNEL";
    public const string TokenVariable = "GMD_ASKPASS_TOKEN";
    public const string CommandVariable = "GMD_ASKPASS_COMMAND";
    public const string IdVariable = "GMD_ASKPASS_ID";

    // The starts of the lines it writes, which LoginError looks for in the output of git: a command
    // that could have asked but could not reach the gmd that ran it, one run in the background,
    // which is never asked, and a question the user cancelled
    public const string Marker = "gmd cannot ask: ";
    public const string NotAskedMarker = "gmd did not ask: ";
    public const string CancelledMarker = "gmd login cancelled: ";

    const int ConnectTimeoutMs = 5000;

    static readonly AsyncLocal<bool> isNeverAsked = new();
    static (string Name, string Token)? channel;

    public static bool IsAsked => Environment.GetEnvironmentVariable(Variable) == "1";

    // The channel the running gmd listens on, null until it listens, e.g. while the command line is
    // handled before the UI starts, when there is no dialog to ask in
    public static (string Name, string Token)? Channel
    {
        get => channel;
        set => channel = value;
    }

    // Whether a command started now may ask: not inside NeverAsk
    public static bool IsAskingAllowed => !isNeverAsked.Value;

    // The git commands started inside, here and in what they await, never ask the user: the ones run
    // in the background, which would otherwise raise a dialog no one asked for. They fail as before,
    // and LoginError says a login is needed. An AsyncLocal, so it follows the awaits down to Cmd.
    public static Disposable NeverAsk()
    {
        var was = isNeverAsked.Value;
        isNeverAsked.Value = true;
        return new Disposable(() => isNeverAsked.Value = was);
    }

    // Touches nothing else: no log, no config, no terminal, since it runs inside a git command, often
    // while the gmd that started that command is drawing on the terminal
    public static int Answer(string[] args)
    {
        var question = string.Join(' ', args).Trim();
        var channelName = Env(ChannelVariable);
        if (channelName == "")
        {
            Console.Error.WriteLine($"{NotAskedMarker}{question}");
            return 1;
        }

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                channelName,
                PipeDirection.InOut,
                PipeOptions.CurrentUserOnly
            );
            pipe.Connect(ConnectTimeoutMs);
            using var writer = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);
            using var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);

            // What ssh means the question to be: 'confirm' for a yes or no, 'none' for a notice only,
            // e.g. to touch a security key, and nothing for an answer to type
            var kind = Env("SSH_ASKPASS_PROMPT");
            WriteRequest(writer, new(Env(TokenVariable), Env(IdVariable), Env(CommandVariable), kind, question));

            if (!reader.ReadBoolean())
            {
                Console.Error.WriteLine($"{CancelledMarker}{question}");
                return 1;
            }
            Console.Out.Write($"{reader.ReadString()}\n");
            return 0;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Console.Error.WriteLine($"{Marker}{question}");
            return 1;
        }
    }

    // What an askpass sends: the token the gmd it asks gave out, which command asks, the kind of
    // question, and the question
    internal record Request(string Token, string Id, string Command, string Kind, string Question);

    internal static void WriteRequest(BinaryWriter writer, Request request)
    {
        writer.Write(request.Token);
        writer.Write(request.Id);
        writer.Write(request.Command);
        writer.Write(request.Kind);
        writer.Write(request.Question);
        writer.Flush();
    }

    internal static Request ReadRequest(BinaryReader reader) =>
        new(reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString());

    static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";
}
