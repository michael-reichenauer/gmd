namespace gmd.Utils.Git.Private;

internal interface IGitLog
{
    Commit[] Log(int maxCount);
}

internal class GitLog : IGitLog
{
    public Commit[] Log(int maxCount)
    {
        return new Commit[] { };
    }
}