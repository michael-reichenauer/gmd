using gmdTest.Fixtures;

namespace gmdTest.Git;

// gmd.Git is meant to stay a general git library, which could be one of its own: it uses nothing of
// gmd but itself and gmd.Utils (Result, Cmd, Log and small helpers), and nothing beyond .NET. The
// layers above know it, never the other way round, and the server's converters are the boundary
// between git's model and gmd's.
//
// Checked in the compiled code (TypeUses) rather than the sources, since C# finds a type without its
// namespace being named, e.g. one in the namespace 'gmd' itself.
[TestClass]
public class GitNamespaceTest
{
    [TestMethod]
    public void TestGmdGitUsesNothingButItselfGmdUtilsAndDotNet()
    {
        var uses = TypeUses
            .Of(TypeUses.In("gmd.Git"))
            .Where(u => !IsAllowed(u.Used))
            .Select(TypeUses.Describe)
            .Distinct()
            .Order()
            .ToList();

        Assert.AreEqual("", string.Join("\n", uses));
    }

    static bool IsAllowed(Type type) =>
        type.IsGenericParameter
        || IsIn(type, "gmd.Git")
        || IsIn(type, "gmd.Utils")
        || IsIn(type, "System")
        || IsIn(type, "Microsoft")
        || (type.Namespace == null && type.Name.StartsWith('<')); // Made by the compiler

    static bool IsIn(Type type, string ns) => TypeUses.IsIn(type, ns);
}
