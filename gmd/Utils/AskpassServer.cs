using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace gmd.Utils;

// A question an askpass brought from git or ssh: what was asked, by which command, and what kind of
// answer ssh wants, 'confirm' for a yes or no, 'none' for a notice only, or '' for one to type
record AskpassQuestion(string Question, string Command, string Kind);

interface IAskpassServer
{
    // Listens for the askpass of the git commands gmd runs, and has the user asked by this: the
    // answer, or null for Cancel
    void Start(Func<AskpassQuestion, Task<string?>> ask);
}

// The channel the askpass of the git that gmd runs asks this gmd on, see Askpass. A named pipe,
// which .NET makes a Unix domain socket on Linux and macOS, opened for the current user only, so no
// other user can connect, and a random token in the environment of the git that only an askpass
// it started knows, so no other program of the user can make gmd show a dialog either. Its name and
// the token go into the environment of every git command that may ask (Cmd.NeverAskOnTheTerminal).
//
// The questions are asked one at a time, since git commands can run side by side, and once a
// question of a command is cancelled, its later ones are not asked: ssh tries the next key, or asks
// again, after a Cancel. Nothing asked or answered is logged.
//
// Below the UI, so the dialog is given to it by the UI, as a function, rather than reached up for.
[SingleInstance]
class AskpassServer : IAskpassServer
{
    const int MaxCancelledIds = 100;

    readonly SemaphoreSlim oneAtATime = new(1, 1);
    readonly Queue<string> cancelledIds = [];
    Func<AskpassQuestion, Task<string?>>? ask;

    public void Start(Func<AskpassQuestion, Task<string?>> ask)
    {
        if (this.ask != null)
            return;
        this.ask = ask;

        // Short, since on macOS the socket's path, in the long per user temp folder, has to fit in
        // the 104 bytes a Unix domain socket's path may have
        var name = $"gmd-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}";
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        try
        {
            var first = NewPipe(name);
            Askpass.Channel = (name, token); // Once there is something to connect to
            ListenAsync(first, name, token).RunInBackground();
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Warn($"No askpass channel, so git's questions are not asked: {e.Message}");
        }
    }

    // Each connection is answered on a pipe of its own, while the next one waits for another
    async Task ListenAsync(NamedPipeServerStream pipe, string name, string token)
    {
        try
        {
            while (true)
            {
                await pipe.WaitForConnectionAsync();
                var connected = pipe;
                Task.Run(() => AnswerAsync(connected, token)).RunInBackground();
                pipe = NewPipe(name);
            }
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            // The commands ask no one from now on, and fail and say what to do instead, as before
            Askpass.Channel = null;
            Log.Warn($"Askpass channel failed: {e.Message}");
        }
    }

    static NamedPipeServerStream NewPipe(string name) =>
        new(
            name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
        );

    async Task AnswerAsync(NamedPipeServerStream pipe, string token)
    {
        using (pipe)
        {
            try
            {
                using var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
                using var writer = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);
                var request = Askpass.ReadRequest(reader);
                if (!IsToken(request.Token, token))
                {
                    Log.Warn("Askpass with the wrong token, not answered");
                    return;
                }

                var answer = await AskAsync(request);
                writer.Write(answer != null);
                writer.Write(answer ?? "");
                writer.Flush();
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Warn($"Askpass not answered: {e.Message}");
            }
        }
    }

    async Task<string?> AskAsync(Askpass.Request request)
    {
        if (IsCancelled(request.Id))
            return null;

        await oneAtATime.WaitAsync();
        try
        {
            if (IsCancelled(request.Id)) // While it waited for another question
                return null;

            var answer = await ask!(new AskpassQuestion(request.Question, request.Command, request.Kind));
            if (answer == null && request.Id != "")
            {
                lock (cancelledIds)
                {
                    cancelledIds.Enqueue(request.Id);
                    if (cancelledIds.Count > MaxCancelledIds)
                        cancelledIds.Dequeue();
                }
            }
            return answer;
        }
        finally
        {
            oneAtATime.Release();
        }
    }

    bool IsCancelled(string id)
    {
        lock (cancelledIds)
        {
            return id != "" && cancelledIds.Contains(id);
        }
    }

    static bool IsToken(string given, string token) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(token));
}
