using gmd.Cui.Common;

namespace gmd.Cui.RepoView;

// How every command in the *Commands classes is run: in the background, with the progress
// spinner shown while it runs and an error message box if it fails.
static class CommandRunner
{
    public static void Do(IProgress progress, Func<Task<R>> action)
    {
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                if (!Try(out var e, await action()))
                {
                    UI.ErrorMessage($"{e.AllErrorMessages()}");
                }
            }
        });
    }
}
