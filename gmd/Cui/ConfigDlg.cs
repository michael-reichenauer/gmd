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

        var repoConf = repoConfig.Get(repoPath);
        var conf = config.Get();
        var dlg = new UIDialog("Config", width, height);

        // Repo specific config
        dlg.AddLabel(1, 0, $"Repo '{repoPath}':");
        var isSyncMetaData = dlg.AddCheckBox(1, 1, "Push/Sync branch structure metadata to server", repoConf.SyncMetaData);

        // General config
        dlg.AddLabel(1, 3, new string('─', width - 2));
        dlg.AddLabel(1, 4, $"General:");
        var isCheckUpdates = dlg.AddCheckBox(1, 5, "Check for new releases", conf.CheckUpdates);
        var isAutoUpdate = dlg.AddCheckBox(1, 6, "Auto update when starting", conf.AutoUpdate);
        var isAllowPreview = dlg.AddCheckBox(1, 7, "Allow preview releases", conf.AllowPreview);

        dlg.AddOK(true, () =>
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

        dlg.AddCancel();
        dlg.Show();
    }
}

