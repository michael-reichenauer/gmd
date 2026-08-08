namespace gmd.Cui.Diff;

// The number of unchanged lines git is asked to show above and below every change (its
// '--unified=<n>'). The diff view steps a file along Levels with the '+' and '-' keys, so this is
// the one place the numbers live. Everything that asks the server for a diff is in the Cui layer,
// so the layers below just pass on whatever they are given.
static class DiffContext
{
    // What a diff is shown with until the user asks for more.
    public const int Default = 6;

    // Git has no 'all', so 'whole file' is a context larger than any file: git stops at the ends
    // of the file rather than erroring. A file longer than this stays truncated.
    public const int WholeFile = 100_000;

    public static readonly IReadOnlyList<int> Levels = [Default, 15, WholeFile];

    // The next level up (step 1) or down (step -1), or the current one when already at an end.
    public static int Step(int contextLines, int step)
    {
        var i = Levels.FindIndexBy(l => l == contextLines);
        if (i == -1)
            return Default;

        var next = i + step;
        return next < 0 || next >= Levels.Count ? contextLines : Levels[next];
    }

    // How a non-default level is named in the file header row, e.g. '(context 15)'.
    public static string ToText(int contextLines) =>
        contextLines == WholeFile ? "(whole file)" : $"(context {contextLines})";
}
