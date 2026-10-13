using gmd.Common;
using gmd.Cui.Common;
using gmd.Installation;

namespace gmd.Cui.App;

interface IUpdateDlg
{
    // Asks whether to update to the new version, with a button for its change log first, and updates
    // if so, after which gmd quits, since the new version runs once gmd is started again. Ok when
    // not asked to, or when there is no new version known.
    Task<Result> ShowAsync();
}

// The question before an update, the same from the start page, the menus and the ⇓ in the bar: the
// version running and the new one, and what updating does, including that gmd then has to be
// started again, which is better known before than after. 'Change Log' shows what the new version
// brings, in the browser, and asks again.
class UpdateDlg : IUpdateDlg
{
    const int updateButton = 0;
    const int changeLogButton = 1;

    readonly Config config;
    readonly IUpdater updater;
    readonly IProgress progress;
    readonly IBrowserService browser;
    readonly IClipboardService clipboard;
    readonly IStatusLine status;

    public UpdateDlg(
        Config config,
        IUpdater updater,
        IProgress progress,
        IBrowserService browser,
        IClipboardService clipboard,
        IStatusLine status
    )
    {
        this.config = config;
        this.updater = updater;
        this.progress = progress;
        this.browser = browser;
        this.clipboard = clipboard;
        this.status = status;
    }

    public async Task<Result> ShowAsync()
    {
        var releases = config.Releases;
        if (!Version.TryParse(releases.LatestVersion, out var latest))
            return Result.Ok;

        var message = Message(
            Build.Version(),
            Build.Time().ToLocalTime(),
            latest,
            Build.GetBuildTime(releases.LatestVersion).ToLocalTime(),
            releases.IsPreview
        );
        while (true)
        {
            var button = UI.InfoMessage("Update Available", message, updateButton, ["Update", "Change Log", "Cancel"]);
            if (button == changeLogButton)
            {
                await ShowChangeLogAsync(ChangeLogUrl(releases));
                continue;
            }
            if (button != updateButton)
            {
                Log.Info("Skip update");
                return Result.Ok;
            }
            break;
        }

        Log.Info($"Updating to {latest.Txt()} ...");
        using (progress.Show())
        {
            var updateTask = updater.UpdateAsync();
            UI.ShowMessageWhile(
                "Updating",
                $"Updating to Gmd {latest.Txt()},\nthis might take a while ...",
                updateTask
            );
            if (await updateTask is Error e)
                return new Error($"Failed to update to {latest.Txt()}", e);
        }

        UI.InfoMessage("Restart Required", $"Gmd is updated to {latest.Txt()},\nwhich runs once gmd is started again.");
        UI.Shutdown();
        return Result.Ok;
    }

    //   Gmd 1.6 (1444.518) is available, a preview.
    //
    //   This version:  1.5 (1444.48)    built 2026-10-13
    //   New version:   1.6 (1444.518)   built 2026-10-19
    //
    //   Gmd quits once it is updated, and the new
    //   version runs when gmd is started again.
    internal static string Message(
        Version running,
        DateTime runningBuilt,
        Version latest,
        DateTime latestBuilt,
        bool isPreview
    )
    {
        // The version lines end in a space, which keeps them off the right border of the box
        var width = Math.Max(running.Txt().Length, latest.Txt().Length);
        return $"Gmd {latest.Txt()} is available{(isPreview ? ", a preview" : "")}.\n\n"
            + $"This version:  {running.Txt().PadRight(width)}   built {runningBuilt.IsoDate()} \n"
            + $"New version:   {latest.Txt().PadRight(width)}   built {latestBuilt.IsoDate()} \n\n"
            + "Gmd quits once it is updated, and the new\n"
            + "version runs when gmd is started again.";
    }

    // The change log, every release newest first, so it covers the ones skipped too, or for a
    // preview, the notes of its release, which say what is new on dev and is not in the change log
    internal static string ChangeLogUrl(Releases releases) =>
        releases.IsPreview ? Project.ReleaseUrl(releases.LatestVersion) : Project.ChangeLogUrl;

    // In the browser, or with none to open it in, e.g. over ssh, the link copied and shown
    async Task ShowChangeLogAsync(string url)
    {
        if (await browser.OpenAsync(url) is not Error e)
        {
            status.Info("Opened the change log in the browser");
            return;
        }

        if (clipboard.Set(url) is Error copyError)
        {
            UI.ErrorMessage(
                $"{e.Message}, and the link could not be copied either:\n\n{url}\n\n{copyError.AllMessages()}"
            );
            return;
        }

        UI.InfoMessage(
            "Change Log",
            $"{e.Message}, so the link to the change log\nis copied to the clipboard:\n\n{url}"
        );
    }
}
