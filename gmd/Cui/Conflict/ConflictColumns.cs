namespace gmd.Cui.Conflict;

// The widths of the resolver's source columns: two panes, or three when the common ancestor is
// shown. Pure math with no view, so it is unit testable, the same reason BlameColumns exists.
record ConflictColumns(int PaneCount, int Pane, int Separators)
{
    // Below this a pane is too narrow to read code in, so the base pane is dropped rather than
    // squeezing all three. Done per draw, so widening the terminal brings it back by itself, as
    // BlameColumns steps its detail down. The same 30 as BlameColumns.MinCodeWidth, which puts the
    // three pane threshold at 92 columns.
    public const int MinPaneWidth = 30;

    public bool HasBase => PaneCount == 3;

    // Where a pane starts, counting the '│' between the ones before it
    public int StartOf(int pane) => pane * (Pane + 1);

    public int TotalWidth => PaneCount * Pane + Separators;

    public static ConflictColumns Calculate(int width, bool isShowBase)
    {
        if (isShowBase)
        {
            var pane = (width - 2) / 3;
            if (pane >= MinPaneWidth)
                return new ConflictColumns(3, pane, 2);
        }

        return new ConflictColumns(2, Math.Max(0, (width - 1) / 2), 1);
    }
}
