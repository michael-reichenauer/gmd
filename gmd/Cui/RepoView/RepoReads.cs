namespace gmd.Cui.RepoView;

// The reads of the repo the log view has running, and whether what one read is still right to show
// when it is done. A read takes a while, and the view goes on meanwhile: a command refreshes, a file
// changes, the user shows or hides a branch. Shown as it came back, a read that finished last but
// started first put the older repo back over the newer one, and a refresh undid a show or hide made
// while it ran, being read with the branches shown before it. So every repo shown is counted, and a
// read that something was shown since it started is overtaken: the view reads again rather than
// show it.
//
// The counts of the reads running are what a change told of meanwhile waits for (RepoView's pending
// changes): they were two flags, which the first of two reads to finish cleared while the other ran.
//
// All of it happens on the UI thread, where the reads' awaits come back to. State and counting only,
// with no view, so it is tested without a terminal.
class RepoReads
{
    long shownCount;
    int repoReads;
    int statusReads;

    // A read running, of the whole repo or of the status alone, and the count of repos shown when it
    // started
    public record Read(long ShownCount, bool IsRepo);

    public Read StartRepo()
    {
        repoReads++;
        return new(shownCount, true);
    }

    public Read StartStatus()
    {
        statusReads++;
        return new(shownCount, false);
    }

    // The read is done, whether what it read is shown or not
    public void End(Read read)
    {
        if (read.IsRepo)
            repoReads--;
        else
            statusReads--;
    }

    // A repo was shown, read or made from the one shown by a show or hide of branches
    public void Shown() => shownCount++;

    // Something was shown since the read started, which what it read would undo
    public bool IsOvertaken(Read read) => read.ShownCount != shownCount;

    public bool IsReadingRepo => repoReads > 0;
    public bool IsReading => repoReads > 0 || statusReads > 0;
}
