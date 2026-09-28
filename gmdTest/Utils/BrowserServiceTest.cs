using System.Runtime.InteropServices;

namespace gmdTest.Utils;

// Which ways there are to open a page, per platform and session, and that the first one that works
// is taken. The platform and the environment are handed in, so every platform is tested here.
[TestClass]
public class BrowserServiceTest
{
    const string Url = "https://github.com/user/repo/tree/dev";

    [TestMethod]
    public void TestALinuxDesktopUsesXdgOpen()
    {
        Assert.AreEqual("xdg-open " + Url, Openers(OSPlatform.Linux, ("DISPLAY", ":0")));
        Assert.AreEqual("xdg-open " + Url, Openers(OSPlatform.Linux, ("WAYLAND_DISPLAY", "wayland-0")));
    }

    // Over ssh, or in a container, there is no desktop and nothing to try, which the caller answers
    // by copying the link
    [TestMethod]
    public void TestNoDesktopHasNoWay()
    {
        Assert.AreEqual("", Openers(OSPlatform.Linux));
    }

    // $BROWSER is what the user or the environment chose, e.g. VS Code's helper for opening the page
    // on the user's own machine, so it comes first
    [TestMethod]
    public void TestBrowserComesFirst()
    {
        Assert.AreEqual(
            $"/vscode/helpers/browser.sh {Url}\nxdg-open {Url}",
            Openers(OSPlatform.Linux, ("BROWSER", "/vscode/helpers/browser.sh"), ("DISPLAY", ":0"))
        );
    }

    // A list like PATH, and '%s' where the page goes when it is not last
    [TestMethod]
    public void TestBrowserIsAListAndCanPlaceThePage()
    {
        Assert.AreEqual(
            $"firefox --new-tab {Url}\nchromium {Url}",
            Openers(OSPlatform.Linux, ("BROWSER", "firefox --new-tab %s:chromium"))
        );
    }

    [TestMethod]
    public void TestWslOpensThePageOnTheWindowsSide()
    {
        Assert.AreEqual(
            $"wslview {Url}\nrundll32.exe url.dll,FileProtocolHandler {Url}",
            Openers(OSPlatform.Linux, ("WSL_DISTRO_NAME", "Ubuntu"))
        );
    }

    [TestMethod]
    public void TestMacOsAndWindowsUseTheirOwnOpener()
    {
        Assert.AreEqual($"open {Url}", Openers(OSPlatform.OSX));
        Assert.AreEqual($"rundll32.exe url.dll,FileProtocolHandler {Url}", Openers(OSPlatform.Windows));
    }

    // An opener that fails, e.g. one that is not installed, is passed over for the next
    [TestMethod]
    public async Task TestTheFirstOpenerThatWorksIsTaken()
    {
        var cmd = new FakeCmd((path, _, _) => path == "wslview" ? FakeCmd.Fail("not found", -1) : FakeCmd.Ok(""));

        AssertOk(await new BrowserService(cmd).OpenAsync(Url, OSPlatform.Linux, Env(("WSL_DISTRO_NAME", "Ubuntu"))));

        CollectionAssert.AreEqual(new[] { "wslview", "rundll32.exe" }, cmd.Calls.Select(c => c.Path).ToArray());
    }

    [TestMethod]
    public async Task TestNoWaySaysSo()
    {
        var cmd = new FakeCmd("");

        var error = AssertError(await new BrowserService(cmd).OpenAsync(Url, OSPlatform.Linux, Env()));

        Assert.AreEqual("There is no browser to open it in here", error.Message);
        Assert.AreEqual(0, cmd.Calls.Count);
    }

    [TestMethod]
    public async Task TestEveryWayFailingSaysSo()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("gio: Operation not supported", 4));

        var error = AssertError(await new BrowserService(cmd).OpenAsync(Url, OSPlatform.Linux, Env(("DISPLAY", ":0"))));

        Assert.AreEqual("The browser could not be opened", error.Message);
    }

    static string Openers(OSPlatform os, params (string Name, string Value)[] vars) =>
        string.Join("\n", BrowserService.OpenersFor(Url, os, Env(vars)).Select(o => $"{o.Path} {o.Args}"));

    static Func<string, string?> Env(params (string Name, string Value)[] vars) =>
        name => vars.FirstOrDefault(v => v.Name == name).Value;
}
