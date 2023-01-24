using static System.Environment;

namespace gmd.Common;

class Upgrader
{
    internal void UpdradeData()
    {
        string oldStatePath = Path.Join(Environment.GetFolderPath(
            SpecialFolder.UserProfile), ".gmdstate.json");
        if (File.Exists(oldStatePath))
        {
            string newStatePath = Path.Join(Environment.GetFolderPath(
           SpecialFolder.UserProfile), ".gmdstate");
            File.Move(oldStatePath, newStatePath);
        }

        string oldConfigPath = Path.Join(Environment.GetFolderPath(
            SpecialFolder.UserProfile), ".gmdconfig.json");
        if (File.Exists(oldConfigPath))
        {
            string newConfigPath = Path.Join(Environment.GetFolderPath(
                SpecialFolder.UserProfile), ".gmdconfig");
            File.Move(oldConfigPath, newConfigPath);
        }
    }
}
