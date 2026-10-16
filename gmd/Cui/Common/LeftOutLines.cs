using gmd.Server;

namespace gmd.Cui.Common;

// The changed lines left out of the next commit, by file, chosen with x in the diff of the
// uncommitted changes: every change is in a commit unless left out, as every file is ticked in the
// commit dialog unless unticked, so there is no staging area to keep in mind. The diff draws them
// dark, the commit dialog counts them, and the commit leaves them in the working tree, uncommitted.
//
// For the session, of the repository shown, as the shown branches' history is (ShownHistory). A
// commit puts a file's lines back in, being then just uncommitted changes. A line is known by its
// version, number and text (ChosenLine), so one that changed since it was left out is not found any
// more, which stops a commit rather than taking it in (Server.CommitLinesAsync).
[SingleInstance]
class LeftOutLines
{
    readonly Dictionary<string, HashSet<ChosenLine>> byPath = [];
    string repoPath = "";

    // The lines are of the repository shown, and those of another one are forgotten
    public void ForRepo(string path)
    {
        if (path == repoPath)
            return;
        repoPath = path;
        byPath.Clear();
    }

    public bool IsAny => byPath.Count > 0;

    public IReadOnlyCollection<string> Paths => byPath.Keys;

    public bool IsLeftOut(string path, ChosenLine line) =>
        byPath.TryGetValue(path, out var lines) && lines.Contains(line);

    public IReadOnlyList<ChosenLine> Of(string path) => byPath.TryGetValue(path, out var lines) ? [.. lines] : [];

    public int CountOf(string path) => byPath.TryGetValue(path, out var lines) ? lines.Count : 0;

    public void LeaveOut(string path, IReadOnlyList<ChosenLine> lines)
    {
        if (!byPath.TryGetValue(path, out var leftOut))
            leftOut = byPath[path] = [];
        leftOut.UnionWith(lines);
    }

    // The lines back in, or lines that are no more, e.g. discarded
    public void Remove(string path, IReadOnlyList<ChosenLine> lines)
    {
        if (!byPath.TryGetValue(path, out var leftOut))
            return;
        leftOut.ExceptWith(lines);
        if (leftOut.Count == 0)
            byPath.Remove(path);
    }

    // The file's left-out lines found again among its changed lines, read again: a line whose number
    // moved, the lines above it having changed, is found by its text where one line alone of its
    // version has that text and is not left out already. How many are not found, which stay as they
    // were, to stop a commit (Server.CommitLinesAsync) until found again or put back.
    public int Refind(string path, IReadOnlyList<ChosenLine> changes)
    {
        if (!byPath.TryGetValue(path, out var leftOut))
            return 0;

        var notFound = 0;
        foreach (var line in leftOut.Where(l => !changes.Contains(l)).ToList())
        {
            var same = changes
                .Where(c => c.IsAdded == line.IsAdded && c.Text == line.Text && !leftOut.Contains(c))
                .ToList();
            if (same.Count != 1)
            {
                notFound++;
                continue;
            }
            leftOut.Remove(line);
            leftOut.Add(same[0]);
        }
        return notFound;
    }

    // Every line of the files back in
    public void PutBack(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            byPath.Remove(path);
    }
}
