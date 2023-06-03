using System.Runtime.InteropServices;
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
        int height = 18;

        var repoConf = repoConfig.Get(repoPath);
        var conf = config.Get();
        var dlg = new UIDialog("Config", width, height);

        // Repo specific config
        dlg.AddLabel(1, 0, $"Repo '{repoPath}':");
        var isSyncMetaData = dlg.AddCheckBox(1, 1, "Push/Sync branch structure metadata to server", repoConf.SyncMetaData);

        // General config
        dlg.AddLine(1, 3, width - 2);

        dlg.AddLabel(1, 4, $"General:");
        var isCheckUpdates = dlg.AddCheckBox(1, 5, "Check for new releases", conf.CheckUpdates);
        var isAutoUpdate = dlg.AddCheckBox(1, 6, "Auto update when starting", conf.AutoUpdate);
        var isAllowPreview = dlg.AddCheckBox(1, 7, "Allow preview releases", conf.AllowPreview);

        CheckBox isAddGmdtoPath = null!;
        //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            isAddGmdtoPath = dlg.AddCheckBox(1, 7, "Add gmd to PATH environment variable", IsGmdAddedToPath());
        }


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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (isAddGmdtoPath.Checked)
                {
                    AddGmdToPath();
                }
                else
                {
                    // DeleteInPathVariable();
                }
            }

            updater.CheckUpdateAvailableAsync().RunInBackground();
        }
    }

    static bool IsGmdAddedToPath()
    {
        string folderPath = Path.GetDirectoryName(Environment.ProcessPath)!;
        string pathsVariables = Environment.GetEnvironmentVariable(
            "PATH", EnvironmentVariableTarget.User)!;
        return false;

    }

    static void AddGmdToPath()
    {
        string folderPath = Path.GetDirectoryName(Environment.ProcessPath)!;

        // Environment.SetEnvironmentVariable("TEST1XX", "TestValue",
        //     EnvironmentVariableTarget.User);

        string pathsVariables = Environment.GetEnvironmentVariable(
            "PATH", EnvironmentVariableTarget.User)!;

        // string keyName = @"Environment\";
        // string pathsVariables = (string)Registry.CurrentUser.OpenSubKey(keyName)
        //     .GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames);

        pathsVariables = pathsVariables.Trim();

        if (!pathsVariables.Contains(folderPath))
        {
            if (!string.IsNullOrEmpty(pathsVariables) && !pathsVariables.EndsWith(";"))
            {
                pathsVariables += ";";
            }

            pathsVariables += folderPath;
            // Environment.SetEnvironmentVariable(
            //     "PATH", pathsVariables, EnvironmentVariableTarget.User);
        }
    }


    //  static void DeleteInPathVariable()
    // {
    //     string programFilesFolderPath = ProgramInfo.GetProgramFolderPath();

    //     string keyName = @"Environment\";
    //     string pathsVariables = (string)Registry.CurrentUser.OpenSubKey(keyName)
    //         .GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames);

    //     string pathPart = programFilesFolderPath;
    //     if (pathsVariables.Contains(pathPart))
    //     {
    //         pathsVariables = pathsVariables.Replace(pathPart, "");
    //         pathsVariables = pathsVariables.Replace(";;", ";");
    //         pathsVariables = pathsVariables.Trim(";".ToCharArray());

    //         Registry.SetValue("HKEY_CURRENT_USER\\" + keyName, "PATH", pathsVariables);
    //     }
    // }
}

