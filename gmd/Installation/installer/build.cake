#tool nuget:?package=Tools.InnoSetup&version=6.2.1

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");


//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define paths.
var name = "gmd";

var baseVersion = "0.30";
var outputPath = $"{name}.exe";
var setupPath = $"{name}Setup.exe";
var issPath = $"./Setup/{name}.iss";
var uninstallerPath = $"Setup/Sign/Uninstaller.exe";
//var signedUninstallerPath = $"Setup/Sign/uninst-5.5.9 (u)-44666f8110.e32";



//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectories($"./**/obj/{configuration}");
    CleanDirectories($"./**/bin/{configuration}");

    if (FileExists(setupPath))
    {
        DeleteFile(setupPath);
    }
});


Task("Build-Setup-File")
    .Does(() =>
{
    //var version = GetFullVersionNumber(outputPath);
    var version = baseVersion;
    string isSigning = "False";

    InnoSetup(issPath, new InnoSetupSettings
    {
        QuietMode = InnoSetupQuietMode.QuietWithProgress,
        Defines = new Dictionary<string, string> {
            {"AppVersion", ""},
            {"ProductVersion", version},
            {"IsSigning", isSigning},
        }
    });
});


Task("Build-Setup")
    .IsDependentOn("Clean")
    .IsDependentOn("Build-Setup-File")
    .Does(() =>
{
})
.OnError(exception =>
{
    RunTarget("Clean");
    throw exception;
}); ;



Task("Default");



//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
