using gmd.Cui.Common;

namespace gmd.Cui.RepoView;

// How every command in the *Commands classes is run: in the background, with the progress
// spinner shown while it runs and an error message box if it fails. Two failures are not shown as
// errors: a Notice, a command that did not run for a reason that is no failure, goes on the status
// line, and a command that stopped on conflicts shows the repo as it now is, part way through the
// operation, and what to do next (RepoCommands.ShowConflicts) rather than git's output.
static class CommandRunner
{
    public static void Do(IProgress progress, IStatusLine status, IViewRepo repo, Func<Task<Result>> action)
    {
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                var result = await action();
                if (result is Notice notice)
                {
                    status.Notice(notice.Message);
                }
                else if (result is Error e && Git.ConflictError.IsIn(e))
                {
                    // The view repo of the refreshed view, since the refresh replaces the snapshot
                    var repoView = repo.RepoView;
                    await repoView.RefreshAsync();
                    repoView.ViewRepo.Cmds.ShowConflicts();
                }
                else if (result is Error error)
                {
                    UI.ErrorMessage($"{error.AllMessages()}");
                }
            }
        });
    }
}
