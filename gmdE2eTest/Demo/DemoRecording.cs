using System.Diagnostics;
using System.Text;
using System.Text.Json;
using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Demo;

// Records a scripted gmd session as an asciicast, the format asciinema records terminals in
// (https://docs.asciinema.org/manual/asciicast/v2/), which agg (https://github.com/asciinema/agg)
// then renders as a GIF. See scripts/demo.
//
// Nothing is recorded as it happens. Each frame is a capture of tmux's screen model, taken once
// the screen has settled, i.e. exactly what the end-to-end tests assert on, colors included. The
// time a frame is shown is chosen by the script rather than measured, so the animation is the
// same on every run however fast the machine is: a fixture with pinned dates, and a clock that
// only moves when a frame says so.
//
// Below gmd's screen is a caption, which says what the frames show, as a subtitle does: a panel
// three rows high across the cast under a blank row, black on a light color gmd fills no row with,
// so that it reads as no part of gmd. The keys it names are key caps, white on blue, written in the
// caption between backticks, e.g. "`c` combines both", and each caption is a scene, numbered at the
// panel's left of how many there are, e.g. '9/18': how far the demo has come, and that it started
// over.
sealed class DemoRecording
{
    const int CaretTimeoutMs = 10000;

    // The blank row between gmd's screen and the caption, and the caption's three rows
    const int CaptionRows = 4;

    // Black on bright white, the caption's panel, a key as a key cap, white on blue, and the number
    // of the scene in gray
    const string PanelColors = "\u001b[0;30;107m";
    const string KeyColors = "\u001b[0;97;44m";
    const string SceneColors = "\u001b[0;90;107m";

    // In the scene numbers until the cast is saved, when it is known how many scenes there are. Of
    // characters JSON leaves as they are, since the frames are serialized before it is replaced.
    const string SceneCountMark = "{{SCENES}}";

    readonly TmuxSession gmd;
    readonly int width;
    readonly int height;
    readonly Func<string, string> rewrite;
    readonly List<string> events = [];
    double time = 0;
    int frameCount = 0;
    string caption = "";
    int scene = 0;

    // The rewrite is applied to every captured screen, for what would otherwise differ between
    // runs, such as the time of the uncommitted row, which is DateTime.Now
    public DemoRecording(TmuxSession gmd, int width, int height, Func<string, string> rewrite)
    {
        this.gmd = gmd;
        this.width = width;
        this.height = height;
        this.rewrite = rewrite;
    }

    // What the frames from now on show, until the next caption, which starts a scene; "" for none,
    // which leaves the panel blank but for the number of the scene it is in
    public void Caption(string text)
    {
        if (text != "" && text != caption)
            scene++;
        caption = text;
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
                height = height + CaptionRows,
            }
        );
        var scenes = scene.ToString().PadRight(2); // As wide as the row was laid out for
        File.WriteAllLines(path, [header, .. events.Select(e => e.Replace(SceneCountMark, scenes))]);
    }

    void WaitForCaret(int x)
    {
        var clock = Stopwatch.StartNew(); // Not the time of day, which jumps, see TmuxSession.Poll
        while (gmd.CursorPosition.X != x)
        {
            if (clock.ElapsedMilliseconds > CaretTimeoutMs)
                Assert.Fail($"The caret did not move on to column {x}, it is at {gmd.CursorPosition.X}");
            Thread.Sleep(50);
        }
    }

    // The panel's middle row: the number of the scene at its left, and the caption after it, its keys
    // as key caps. Left aligned, so that each caption starts where the last did, by the number, rather
    // than in the middle, where each started somewhere else. The number is laid out two digits wide on
    // each side, ' 9/18', since how many scenes there are is only known once all are recorded: a row
    // a column too long would wrap the cast's last row.
    string CaptionLine()
    {
        var number = scene > 0 ? $"  {scene, 2}/{SceneCountMark}" : "";
        var left = NumberWidth + CaptionGap;
        var parts = caption.Split('`').Select((text, i) => (Text: i % 2 == 1 ? $" {text} " : text, IsKey: i % 2 == 1));
        var length = parts.Sum(p => p.Text.Length);
        Assert.IsTrue(left + length <= width, $"The caption is too long for the panel: '{caption}'");

        var line = new StringBuilder(PanelColors).Append(' ', NumberWidth - (scene > 0 ? NumberWidth : 0));
        line.Append(SceneColors).Append(number).Append(PanelColors).Append(' ', CaptionGap);
        foreach (var (text, isKey) in parts)
            line.Append(isKey ? KeyColors : PanelColors).Append(text);
        line.Append(PanelColors).Append(' ', width - left - length);
        return line.ToString();
    }

    // Between the number of the scene and the caption
    const int CaptionGap = 3;

    const int NumberWidth = 7; // '  ', two digits, '/', two digits

    string BlankPanelRow() => PanelColors + new string(' ', width);

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
            .Append("\u001b[0m")
            .Append($"\u001b[{height + 2};1H{BlankPanelRow()}")
            .Append($"\u001b[{height + 3};1H{CaptionLine()}")
            .Append($"\u001b[{height + 4};1H{BlankPanelRow()}\u001b[0m");

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
