using gmdTest.Fixtures;

namespace gmdTest.Cui;

// The UI reaches git only through the server, IServer: it never runs a git command itself, nor uses the
// git layer's services or the server's implementation. It may use the git layer's value types and
// helpers, which carry no git process (WebLinks, RefName, GitDir, DiffOptions, ConflictError,
// LoginError and the like), since copying each into the server would only add converters.
//
// Checked in the compiled code (TypeUses), as GitNamespaceTest checks the git layer.
[TestClass]
public class CuiLayerTest
{
    [TestMethod]
    public void TestCuiReachesGitOnlyThroughTheServer()
    {
        var all = TypeUses.Of(TypeUses.In("gmd.Cui")).ToList();
        Assert.IsTrue(all.Any(u => u.Used == typeof(gmd.Server.IServer)), "The scan found no use of IServer");

        var uses = all.Where(u => IsForbidden(u.Used)).Select(TypeUses.Describe).Distinct().Order().ToList();

        Assert.AreEqual("", string.Join("\n", uses));
    }

    static bool IsForbidden(Type type) =>
        type == typeof(gmd.Git.IGit)
        || TypeUses.IsIn(type, "gmd.Git.Private")
        || TypeUses.IsIn(type, "gmd.Server.Private");
}
