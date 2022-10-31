using gmd.Utils;

namespace gmd.Utils.Git.Private;

internal interface IGitLog
{
    Commit[] Log(int maxCount);
}

internal class GitLog : IGitLog
{
    private readonly ICmd cmd;

    internal GitLog(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public Commit[] Log(int maxCount)
    {
        cmd.Run("git", "version");
        return new Commit[] { };
    }
}