using gmd.Cui.Common;

namespace gmd.Cui.RepoView;

// How every command in the *Commands classes is run: in the background, with the progress
// spinner shown while it runs and an error message box if it fails. A Notice, a command that did
// not run for a reason that is not a failure, goes on the status line instead of in a box.
static class CommandRunner
{
    public static void Do(IProgress progress, IStatusLine status, Func<Task<Result>> action)
    {
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                var result = await action();
                if (result is Notice notice)
                    status.Notice(notice.Message);
                else if (result is Error e)
                    UI.ErrorMessage($"{e.AllMessages()}");
            }
        });
    }
}
