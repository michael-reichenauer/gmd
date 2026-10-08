namespace gmd.Cui.RepoView;

// The commit marked for a diff with another, by Mark for Diff in the commit menu, until another is
// marked: Diff with it, in the menu of any other commit, diffs the two, wherever they are in the log,
// which a selected range does only for commits of one branch. One for the life of gmd, since the
// commands and menus are made anew with every refresh, and of one repository, the one it was
// marked in.
[SingleInstance]
class DiffMark
{
    string repoPath = "";
    string commitId = "";

    public void Set(string repoPath, string commitId)
    {
        this.repoPath = repoPath;
        this.commitId = commitId;
    }

    // The commit marked in the repository, or "" when none is
    public string IdIn(string repoPath) => this.repoPath == repoPath ? commitId : "";
}
