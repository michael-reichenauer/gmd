using static System.Environment;

namespace gmd.Common;

// cSpell:ignore gmdconfig
class Upgrader
{
    internal static void UpgradeData()
    {
        string oldStatePath = Path.Join(GetFolderPath(
            SpecialFolder.UserProfile), ".gmdstate.json");
        string newStatePath = Path.Join(GetFolderPath(
           SpecialFolder.UserProfile), ".gmdstate");
        if (File.Exists(oldStatePath))
        {
            File.Delete(oldStatePath);
        }
        if (File.Exists(newStatePath))
        {
            File.Delete(newStatePath);
        }
    }
}
