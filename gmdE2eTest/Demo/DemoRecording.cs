using System.Text;
using System.Text.Json;
using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Demo;

// Records a scripted gmd session as an asciicast, the format asciinema records terminals in
// (https://docs.asciinema.org/manual/asciicast/v2/), which agg (https://github.com/asciinema/agg)
// then renders as a GIF. See ./demo.
//
// Nothing is recorded as it happens. Each frame is a capture of tmux's screen model, taken once
// the screen has settled, i.e. exactly what the end-to-end tests assert on, colors included. The
// time a frame is shown is chosen by the script rather than measured, so the animation is the
// same on every run however fast the machine is: a fixture with pinned dates, and a clock that
// only moves when a frame says so.
sealed class DemoRecording
{
    const int CaretTimeoutMs = 10000;

    readonly TmuxSession gmd;
    readonly int width;
    readonly int height;
    readonly Func<string, string> rewrite;
    readonly List<string> events = [];
    double time = 0;
    int frameCount = 0;

    // The rewrite is applied to every captured screen, for what would otherwise differ between
    // runs, such as the time of the uncommitted row, which is DateTime.Now
    public DemoRecording(TmuxSession gmd, int width, int height, Func<string, string> rewrite)
    {
        this.gmd = gmd;
        this.width = width;
        this.height = height;
        this.rewrite = rewrite;
    }

    // Adds the screen as it is now, shown for the given number of seconds. Every frame is a marker
    // in the cast, named by the label or else numbered, so that one frame can be rendered on its
    // own ('agg --select marker:<label>'), which is how a frame is looked at without playing the
    // whole animation.
    public void Frame(double seconds, string label = "")
    {
        frameCount++;
        events.Add(Event("m", label != "" ? label : $"frame-{frameCount}"));
        events.Add(Event("o", ToOutput(rewrite(gmd.CaptureScreen()))));
        time += seconds;
    }

    // Types the text one character at a time with a frame for each, so the animation shows it
    // being typed. A character is waited for by the caret moving on, which is exact where waiting
    // for the text would not be: what has been typed so far may already be on screen elsewhere.
    public void Type(string text, double secondsPerCharacter)
    {
        var x = gmd.CursorPosition.X;
        for (int i = 0; i < text.Length; i++)
        {
            gmd.SendText(text[i].ToString());
            WaitForCaret(x + i + 1);
            gmd.WaitForStable();
            Frame(secondsPerCharacter);
        }
    }

    public void Save(string path)
    {
        var header = JsonSerializer.Serialize(
            new
            {
                version = 2,
                width,
                height,
            }
        );
        File.WriteAllLines(path, [header, .. events]);
    }

    void WaitForCaret(int x)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(CaretTimeoutMs);
        while (gmd.CursorPosition.X != x)
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"The caret did not move on to column {x}, it is at {gmd.CursorPosition.X}");
            Thread.Sleep(50);
        }
    }

    string Event(string type, string data) => JsonSerializer.Serialize<object[]>([Math.Round(time, 3), type, data]);

    // A whole screen as terminal output: clear, then the rows as captured. The captured rows only
    // emit an escape where the colors change, carrying the colors of one row on into the next, so
    // they are joined as a terminal would receive them rather than written row by row. Then the
    // cursor, which the log view hides and a text field shows as its caret.
    string ToOutput(string screen)
    {
        var rows = screen.Split('\n').Take(height);
        var output = new StringBuilder()
            .Append("\u001b[0m\u001b[2J\u001b[H")
            .AppendJoin("\r\n", rows)
            .Append("\u001b[0m");

        if (gmd.IsCursorVisible)
        {
            var (x, y) = gmd.CursorPosition;
            output.Append($"\u001b[{y + 1};{x + 1}H\u001b[?25h");
        }
        else
        {
            output.Append("\u001b[?25l");
        }

        return output.ToString();
    }
}
