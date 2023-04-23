using gmd.Common;
using gmd.Cui.Common;
using gmd.Installation;
using Terminal.Gui;

namespace gmd.Cui;

interface IConfigDlg
{
    void Show(string repoPath);
}

class ConfigDlg : IConfigDlg
{
    readonly IConfig config;
    readonly IRepoConfig repoConfig;
    private readonly IUpdater updater;

    internal ConfigDlg(IConfig config, IRepoConfig repoConfig, IUpdater updater)
    {
        this.config = config;
        this.repoConfig = repoConfig;
        this.updater = updater;
    }


    public void Show(string repoPath)
    {
        int width = 60;
        int height = 15;

        // Repo specific config
        var repoInfoinfo = Components.Label(1, 0, $"Repo '{repoPath}':");
        var isSyncMetaData = Components.CheckBox("Push/Sync branch structure metadata to server",
            repoConfig.Get(repoPath).SyncMetaData, 1, 1);


        // General config
        var sep1 = new Label(0, 3, new string('─', width - 2));
        var commoninfo = Components.Label(1, 4, $"General:");
        var isCheckUpdates = Components.CheckBox("Check for new releases",
          config.Get().CheckUpdates, 1, 5);
        var isAutoUpdate = Components.CheckBox("Auto update when starting",
            config.Get().AutoUpdate, 1, 6);
        var isAllowPreview = Components.CheckBox("Allow preview releases",
            config.Get().AllowPreview, 1, 7);

        // Handle OK
        Button okButton = Buttons.OK(true, () =>
        {
            // Update repo config
            repoConfig.Set(repoPath, c => c.SyncMetaData = isSyncMetaData.Checked);

            // Update general config
            config.Set(c =>
            {
                c.CheckUpdates = isCheckUpdates.Checked;
                c.AutoUpdate = isAutoUpdate.Checked;
                c.AllowPreview = isAllowPreview.Checked;
            });
            updater.CheckUpdateAvailableAsync().RunInBackground();

            return true;
        });


        var dialog = Components.Dialog("Config", width, height, okButton, Buttons.Cancel());
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(repoInfoinfo, isSyncMetaData, sep1, commoninfo,
            isCheckUpdates, isAutoUpdate, isAllowPreview);

        UI.ShowCursor();
        UI.RunDialog(dialog);
    }
}

