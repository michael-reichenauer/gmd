using gmd.Common;
using gmd.Cui.Common;
using gmd.Installation;

namespace gmd.Cui.App;

interface IConfigDlg
{
    // Whether what the repo is read with changed, so it needs reading again
    bool Show(string repoPath);
}

class ConfigDlg : IConfigDlg
{
    readonly Config config;
    readonly IRepoConfigService repoConfig;
    private readonly IUpdater updater;

    internal ConfigDlg(Config config, IRepoConfigService repoConfig, IUpdater updater)
    {
        this.config = config;
        this.repoConfig = repoConfig;
        this.updater = updater;
    }

    public bool Show(string repoPath)
    {
        int width = 60;
        int height = 26;

        var repoConf = repoConfig.Get(repoPath);
        var dlg = new UIDialog("Config", width, height);

        // Repo specific config
        dlg.AddLabel(1, 0, $"Repo '{repoPath}':");
        var isSyncMetaData = dlg.AddCheckBox(
            1,
            1,
            "Push/Sync branch structure metadata to server",
            repoConf.SyncMetaData
        );
        dlg.AddLabel(1, 3, "Integration branches, besides develop and dev:");
        var integrationBranches = dlg.AddTextField(1, 5, width - 4, string.Join(", ", repoConf.IntegrationBranches));

        // General config
        dlg.AddLine(1, 7, width - 2);

        dlg.AddLabel(1, 8, $"General:");
        var isCheckUpdates = dlg.AddCheckBox(1, 9, "Check for new releases", config.CheckUpdates);
        var manager = updater.PackageManager;
        var isAutoUpdate = dlg.AddCheckBox(1, 10, AutoUpdateText(manager), config.AutoUpdate);
        var isAllowPreview = dlg.AddCheckBox(1, 11, AllowPreviewText(manager), config.AllowPreview);
        // A package manager updates gmd, with stable releases alone, so neither is gmd's to do then
        isAutoUpdate.Enabled = manager == null;
        isAllowPreview.Enabled = manager == null;
        var isSpellCheck = dlg.AddCheckBox(1, 12, "Spell check commit messages", config.SpellCheck);
        var isShowKeyHints = dlg.AddCheckBox(1, 13, "Show key hints at the bottom of the log", config.ShowKeyHints);
        var isAddGmdToPath = dlg.AddCheckBox(1, 14, "Add gmd to PATH environment variable", IsGmdAddedToPathVariable());
        isAddGmdToPath.Visible = !Build.IsDevInstance() && Build.IsWindows;

        // The palette, see Palette.Choose, picked from the list or typed
        dlg.AddLabel(1, 16, "Colors:");
        var colors = dlg.AddComboTextField(10, 16, 10, 5, () => Palette.Settings, config.Colors);
        dlg.AddLabel(1, 18, Text.Dark("auto picks dark or light by the terminal's background,"));
        dlg.AddLabel(1, 19, Text.Dark("16 is the 16 colors of the terminal's theme"));
        var isColorCode = dlg.AddCheckBox(1, 21, "Color the code in diffs by its language", config.ColorCode);
        dlg.Validate(
            () => Palette.Settings.Contains(colors.Text),
            $"Colors is one of {string.Join(", ", Palette.Settings)}"
        );

        if (!dlg.ShowOkCancel())
            return false;

        // Update repo config
        var names = ParseNames(integrationBranches.Text);
        var isNamesChanged = !names.SequenceEqual(repoConf.IntegrationBranches);
        repoConfig.Set(
            repoPath,
            c =>
            {
                c.SyncMetaData = isSyncMetaData.Checked;
                c.IntegrationBranches = names;
            }
        );

        // Update general config
        config.Set(c =>
        {
            c.CheckUpdates = isCheckUpdates.Checked;
            c.AutoUpdate = isAutoUpdate.Checked;
            c.AllowPreview = isAllowPreview.Checked;
            c.SpellCheck = isSpellCheck.Checked;
            c.ShowKeyHints = isShowKeyHints.Checked;
            c.Colors = colors.Text;
            c.ColorCode = isColorCode.Checked;
        });
        UI.UsePalette(colors.Text);

        UpdatePathVariable(isAddGmdToPath.Checked);
        updater.CheckUpdateAvailableAsync().RunInBackground();
        return isNamesChanged;
    }

    // Saying why the box is greyed out when a package manager installed gmd
    internal static string AutoUpdateText(PackageManager? manager) =>
        manager == null ? "Auto update to new releases" : $"Auto update ({manager.Name} updates gmd)";

    internal static string AllowPreviewText(PackageManager? manager) =>
        manager == null ? "Allow preview releases" : $"Allow preview releases (not with {manager.Name})";

    // Branch names as typed, separated by commas or spaces
    internal static List<string> ParseNames(string text) =>
        text.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

    static void UpdatePathVariable(bool isAddGmdToPath)
    {
        if (Build.IsDevInstance() || !Build.IsWindows)
            return;

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
        string pathsVariables = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        var parts = pathsVariables.Split(';');

        return parts.FirstOrDefault(p => p.ToUpper() == folderPath) != null;
    }

    static void AddGmdToPathVariable()
    {
        if (IsGmdAddedToPathVariable())
            return;

        string folderPath = Path.GetDirectoryName(Environment.ProcessPath)!;
        string pathVariable = (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "").Trim();
        string newPathVariable = pathVariable != "" ? pathVariable + ";" + folderPath : folderPath;

        // Windows only, see UpdatePathVariable: the user PATH is a registry value there
        Environment.SetEnvironmentVariable("PATH", newPathVariable, EnvironmentVariableTarget.User);

        UI.InfoMessage(
            "Gmd",
            "Added gmd to PATH environment variable\n\n"
                + "You need to restart running terminals for the change to take effect"
        );
    }

    static void RemoveGmdFromPathVariable()
    {
        if (!IsGmdAddedToPathVariable())
            return;

        string folderPath = Path.GetDirectoryName(Environment.ProcessPath)!.ToUpper();

        string pathVariables = (
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? ""
        ).Trim();
        var parts = pathVariables.Split(';');
        string newPathVariable = String.Join(';', parts.Where(p => p.ToUpper() != folderPath));

        // Windows only, see UpdatePathVariable
        Environment.SetEnvironmentVariable("PATH", newPathVariable, EnvironmentVariableTarget.User);

        UI.InfoMessage(
            "Gmd",
            "Removed gmd from PATH environment variable\n\n"
                + "You need to restart running terminals for the change to take effect"
        );
    }
}
