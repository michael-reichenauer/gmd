namespace gmd.Server;

// A changed line of a file's uncommitted changes, chosen in the diff to be discarded: whether it is
// a line added in the working tree or one removed from the last commit, its number in that version,
// and its text as the diff showed it, which is how a line that changed since is told from one that
// did not (ChosenLines)
record ChosenLine(bool IsAdded, int LineNbr, string Text)
{
    public override string ToString() => $"{(IsAdded ? '+' : '-')}{LineNbr} {Text}";
}
