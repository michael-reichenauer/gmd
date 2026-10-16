namespace gmd.Server;

// What a discard threw away, or a stash dropped, which gmd kept to put it back (DiscardedChanges):
// the record's commit, what it was, e.g. "3 lines of a.txt", and when
record DiscardedChange(string Id, string What, DateTime Time, bool IsStash)
{
    public override string ToString() => $"{Time:yyyy-MM-dd HH:mm} {What}";
}
