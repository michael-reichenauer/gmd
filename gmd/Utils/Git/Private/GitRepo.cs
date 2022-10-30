namespace gmd.Utils.Git.Private;

internal class GitRepo : IGitRepo
{
    private IGitLog log;

    public GitRepo(string path)
    {
        log = new GitLog();
    }

    public Commit[] Log(int maxCount)
    {
        return log.Log(maxCount);
    }

}