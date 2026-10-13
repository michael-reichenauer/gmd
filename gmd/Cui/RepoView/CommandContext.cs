using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// What every command class of the log view works with: the repo shown now, the view it is shown in,
// the server, the status line and the progress, how a command is run, and the other command classes.
//
// One, made with the log view, as the command classes are. They used to be made again with every
// snapshot of the repo, i.e. at every refresh, show or hide, and a command held the repo of when it
// was made: one that refreshed and went on had to fetch the view's new snapshot, and the command
// classes of that, to act on what it had just done. A command reads the repo when it runs instead,
// and Do hands it the snapshot as it starts, which it keeps until it ends.
class CommandContext
{
    public CommandContext(IRepoView view, IServer server, IProgress progress, IStatusLine status)
    {
        View = view;
        Server = server;
        Progress = progress;
        Status = status;
    }

    // The repo shown now, which every refresh replaces
    public IViewRepo ViewRepo => View.ViewRepo;

    public IRepoView View { get; }
    public IServer Server { get; }
    public IProgress Progress { get; }
    public IStatusLine Status { get; }

    // The command classes, which call one another, so they are set here once all are made
    public IRepoCommands RepoCmds { get; set; } = null!;
    public ICommitCommands CommitCmds { get; set; } = null!;
    public IBranchCommands BranchCmds { get; set; } = null!;

    // Runs a command in the background with the repo shown as it starts, see CommandRunner
    public void Do(Func<IViewRepo, Task<Result>> action) => CommandRunner.Do(this, action);
}
