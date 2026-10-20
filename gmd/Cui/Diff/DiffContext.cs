using gmd.Server;

namespace gmd.Cui.Diff;

// The number of unchanged lines git is asked to show above and below every change (its
// '--unified=<n>'). The diff view steps a file by StepLines with the '+' and '-' keys, and shows it
// whole from its menu, so this is the one place the numbers live. Everything that asks the server
// for a diff is in the Cui layer, so the layers below just pass on whatever they are given.
static class DiffContext
{
    // What a diff is shown with until the user asks for more.
    public const int Default = 6;

    // How many lines more '+' shows above and below every change of a file, and '-' fewer
    public const int StepLines = 15;

    // Git has no 'all', so 'whole file' is a context larger than any file: git stops at the ends
    // of the file rather than erroring. A file longer than this stays truncated.
    public const int WholeFile = 100_000;

    // The context a step up (step 1) or down (step -1) shows, never less than the default. The whole
    // file is shown from the menu rather than stepped to, so down from it is back to the context it
    // was shown at before, before, and up from it is where it is.
    public static int Step(int contextLines, int step, int before = Default) =>
        contextLines == WholeFile
            ? (step > 0 ? WholeFile : Math.Max(before, Default))
            : Math.Clamp(contextLines + step * StepLines, Default, WholeFile);

    // Whether a file's diff at a context shows the whole file, so that more context would show no
    // more of it: one hunk from the first line, which ends in fewer unchanged lines than the context,
    // since git shows that many after the last change wherever the file goes on. Git says nothing of
    // how long a file is, so one that ends just there is taken to go on, and a step more shows that
    // it did not. A file with no hunk, binary or renamed alone, has no lines to show more of.
    public static bool IsWholeFileShown(FileDiff file, int contextLines)
    {
        if (contextLines == WholeFile || file.SectionDiffs.Count == 0)
            return true;
        if (file.SectionDiffs.Count > 1)
            return false;

        var section = file.SectionDiffs[0];
        if (section.LeftLine > 1 || section.RightLine > 1)
            return false;

        var lines = section.LineDiffs;
        var unchangedAtEnd = lines.Count - 1 - lines.FindLastIndexBy(l => l.DiffMode != DiffMode.DiffSame);
        return unchangedAtEnd < contextLines;
    }

    // How a context other than the default is named in the file header row, e.g. '(context 21)',
    // i.e. what the file being looked at is showing.
    public static string ToText(int contextLines) =>
        contextLines == WholeFile ? "(whole file)" : $"(context {contextLines})";

    // How a context is named in a menu item, i.e. what picking it would show rather than what is
    // shown now. 'whole file' is worded the same in both, which is what ties the two together.
    public static string ToItemText(int contextLines) =>
        contextLines == WholeFile ? "whole file" : $"{contextLines} lines";
}
