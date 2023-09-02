using gmd.Common;
using gmd.Cui.Common;
using gmd.Installation;

namespace gmd.Cui;

interface IConfigDlg
{
    void Show(string repoPath);
}

class ConfigDlg : IConfigDlg
{
    readonly Config config;
    readonly IRepoConfig repoConfig;
    private readonly IUpdater updater;

    internal ConfigDlg(Config config, IRepoConfig repoConfig, IUpdater updater)
    {
        this.config = config;
        this.repoConfig = repoConfig;
        this.updater = updater;
    }


    public void Show(string repoPath)
    {
        int width = 60;
        int height = 18;

        var repoConf = repoConfig.Get(repoPath);
        var dlg = new UIDialog("Config", width, height);

        // Repo specific config
        dlg.AddLabel(1, 0, $"Repo '{repoPath}':");
        var isSyncMetaData = dlg.AddCheckBox(1, 1, "Push/Sync branch structure metadata to server", repoConf.SyncMetaData);

        // General config
        dlg.AddLine(1, 3, width - 2);

        dlg.AddLabel(1, 4, $"General:");
        var isCheckUpdates = dlg.AddCheckBox(1, 5, "Check for new releases", config.CheckUpdates);
        var isAutoUpdate = dlg.AddCheckBox(1, 6, "Auto update when starting", config.AutoUpdate);
        var isAllowPreview = dlg.AddCheckBox(1, 7, "Allow preview releases", config.AllowPreview);
        var isAddGmdToPath = dlg.AddCheckBox(1, 8, "Add gmd to PATH environment variable", IsGmdAddedToPathVariable());
        isAddGmdToPath.Visible = !Build.IsDevInstance() && Build.IsWindows;

        if (dlg.ShowOkCancel())
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

            UpdatePathVariable(isAddGmdToPath.Checked);
            updater.CheckUpdateAvailableAsync().RunInBackground();
        }
    }

    static void UpdatePathVariable(bool isAddGmdToPath)
    {
        if (Build.IsDevInstance() || !Build.IsWindows) return;

        if (isAddGmdToPath)
        {
            AddGmdToPathVariable();
        }
        else
        {
            RemoveGmdFromPathVariable();
        }
    }

    static bool IsGmdAddedToPathVariable()
    {
        string folderPath = Path.GetDirectoryName(Environment.ProcessPath)!.ToUpper();
        string pathsVariables = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "".Trim();
        var parts = pathsVariables.Split(';');

        return parts.FirstOrDefault(p => p.ToUpper() == folderPath) != null;
    }


    static void AddGmdToPathVariable()
    {
        if (IsGmdAddedToPathVariable()) return;

        string folderPath = Path.GetDirectoryName(Environment.ProcessPath)!;
        string pathVariable = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "".Trim();
        string newPathVariable = pathVariable != "" ? pathVariable + ";" + folderPath : folderPath;

        if (Build.IsWindows)
        {
            Environment.SetEnvironmentVariable("PATH", newPathVariable, EnvironmentVariableTarget.User);
        }
        else
        {
            // Add to path for Linux and Mac
            UI.InfoMessage("Not implemented yet", "Add gmd to PATH environment variable");

        }

        UI.InfoMessage("Gmd", "Added gmd to PATH environment variable\n\n" +
            "You need to restart running terminals for the change to take effect");
    }


    static void RemoveGmdFromPathVariable()
    {
        if (!IsGmdAddedToPathVariable()) return;

        string folderPath = Path.GetDirectoryName(Environment.ProcessPath)!.ToUpper();

        string pathVariables = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "".Trim();
        var parts = pathVariables.Split(';');
        string newPathVariable = String.Join(';', parts.Where(p => p.ToUpper() != folderPath));

        if (Build.IsWindows)
        {
            Environment.SetEnvironmentVariable("PATH", newPathVariable, EnvironmentVariableTarget.User);
        }
        else
        {
            // Remove path for Linux and Mac
            UI.InfoMessage("Not implemented yet", "Remove gmd from PATH environment variable");
        }

        UI.InfoMessage("Gmd", "Removed gmd to PATH environment variable\n\n" +
            "You need to restart running terminals for the change to take effect");
    }


    //       if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    //         {
    //             name = "gmd_osx";
    //         }
    //         else
    //         if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    //         {
    //             name = "gmd_linux";
    //         }
    //         else
    //         if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    // {
    //     name = "gmd_windows";
}


