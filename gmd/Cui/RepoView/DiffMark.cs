namespace gmd.Cui.RepoView;

// The commit marked for a diff with another, by Mark for Diff in the commit menu, until another is
// marked or it is unmarked: Diff with it, in the menu of any other commit, diffs the two, wherever
// they are in the log, which a selected range does only for commits of one branch. The log draws it
// with Marker before its subject, the rest of its row on a background of its own (Color.MarkBg), so
// that it is found again. One for the life of gmd, since the commands and menus are made
// anew with every refresh, and of one repository, the one it was marked in.
[SingleInstance]
class DiffMark
{
    // Drawn in the column before the subject, over a stash's 'ß', which the top bar still counts
    public const string Marker = "±";

    string repoPath = "";
    string commitId = "";

    public void Set(string repoPath, string commitId)
    {
        this.repoPath = repoPath;
        this.commitId = commitId;
    }

    public void Clear() => Set("", "");

    // The commit marked in the repository, or "" when none is
    public string IdIn(string repoPath) => this.repoPath == repoPath ? commitId : "";
}
