namespace gmd.Cui.Common;

// What the end-to-end tests (gmdE2eTest, TmuxSession) are told of gmd's state, which a screen does
// not show: whether gmd is idle, and how many keys it has had and how many of them it dropped.
//
// The tests used to take a screen unchanged for 300 ms as gmd being done and ready for a key, which
// on a loaded machine, as CI is, it often was not: keys are dropped while git works, before the
// marquee shows that it does (Progress, KeyGate), and a command ends in steps, a redraw, a refresh,
// a read of the branches merged on origin, with pauses between them. A key sent then was lost, or a
// screen read before the last step, and those were the flaky tests.
//
// It is written as the terminal's title, which tmux keeps as the pane's, by UI as each iteration of
// the main loop starts, i.e. after the frame of the one before was written: on the same output as the
// frames, so once tmux shows a title it has the whole frame before it, never half of one.
//
// Only when the tests start gmd (GMD_E2E=1); the state alone, with no Terminal.Gui, so it is tested
// without a terminal.
class E2eProbe
{
    internal const string Variable = "GMD_E2E";
    internal const string Prefix = "gmd-e2e";

    int keys;
    int dropped;
    string shown = "";

    internal static bool IsOn => Environment.GetEnvironmentVariable(Variable) == "1";

    public void KeyReceived(bool isDropped)
    {
        keys++;
        if (isDropped)
            dropped++;
    }

    // The title to write, or null when it is the one written last, so that an idle gmd writes nothing.
    // Unless 'isOverwritten': Terminal.Gui writes the title of the runnable on top as one opens or
    // closes, gmd's are empty, and the tests would then see no state until the next key.
    public string? TitleIfChanged(bool isBusy, bool isOverwritten = false)
    {
        var title = Title(isBusy);
        if (title == shown && !isOverwritten)
            return null;
        shown = title;
        return title;
    }

    // E.g. 'gmd-e2e idle 12 1', read back by TmuxSession's ProbeState
    public string Title(bool isBusy) => $"{Prefix} {(isBusy ? "busy" : "idle")} {keys} {dropped}";
}
