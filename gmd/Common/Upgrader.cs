using static System.Environment;

namespace gmd.Common;

// cSpell:ignore gmdconfig
class Upgrader
{
    internal static void UpgradeData()
    {
        string oldStatePath = Path.Join(Environment.GetFolderPath(
            SpecialFolder.UserProfile), ".gmdstate.json");
        string newStatePath = Path.Join(Environment.GetFolderPath(
           SpecialFolder.UserProfile), ".gmdstate");
        if (File.Exists(oldStatePath) && !File.Exists(newStatePath))
        {
            File.Move(oldStatePath, newStatePath);
        }

        string oldConfigPath = Path.Join(Environment.GetFolderPath(
            SpecialFolder.UserProfile), ".gmdconfig.json");
        string newConfigPath = Path.Join(Environment.GetFolderPath(
           SpecialFolder.UserProfile), ".gmdconfig");
        if (File.Exists(oldConfigPath) && !File.Exists(newConfigPath))
        {
            File.Move(oldConfigPath, newConfigPath);
        }
    }
}
