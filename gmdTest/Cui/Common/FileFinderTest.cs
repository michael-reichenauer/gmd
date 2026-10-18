using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// Which files the file browser lists for what has been typed, and in what order
[TestClass]
public class FileFinderTest
{
    static readonly string[] Files =
    [
        "README.md",
        "gmd/Program.cs",
        "gmd/Common/ProgramCommands.cs",
        "gmd/Cui/Common/FileBrowseDlg.cs",
        "gmd/Cui/Common/ListDlg.cs",
        "gmd/Cui/Branches/FindBranchDlg.cs",
        "gmdTest/Cui/Common/FileTreeTest.cs",
        "docs/CONTRIBUTING.md",
    ];

    // With nothing typed, every file as given; the dialog shows the tree then
    [TestMethod]
    public void TestNothingTypedListsEveryFile()
    {
        Assert.AreSame(Files, FileFinder.Find(Files, "  "));
    }

    // Every word has to be in the path, in any order, and case does not matter
    [TestMethod]
    public void TestEveryWordHasToBeInThePath()
    {
        Assert.AreEqual(
            """
            gmd/Cui/Common/FileBrowseDlg.cs
            gmd/Cui/Common/ListDlg.cs
            """,
            Found("DLG common")
        );
        Assert.AreEqual("gmdTest/Cui/Common/FileTreeTest.cs", Found("test/cui"), "A word can span folders");
        Assert.AreEqual("", Found("nothing"));
    }

    // A name starting with the first word comes first, then a name that has it, then a path whose
    // folders alone have it; each with the files in fewer folders first, then in path order
    [TestMethod]
    public void TestAMatchInTheNameComesFirst()
    {
        Assert.AreEqual(
            """
            gmd/Program.cs
            gmd/Common/ProgramCommands.cs
            """,
            Found("program")
        );
        Assert.AreEqual(
            """
            gmd/Cui/Branches/FindBranchDlg.cs
            gmd/Cui/Common/FileBrowseDlg.cs
            gmdTest/Cui/Common/FileTreeTest.cs
            """,
            Found("f")
        );
        Assert.AreEqual(
            """
            gmd/Common/ProgramCommands.cs
            gmd/Cui/Common/FileBrowseDlg.cs
            gmd/Cui/Common/ListDlg.cs
            gmdTest/Cui/Common/FileTreeTest.cs
            """,
            Found("common"),
            "ProgramCommands has it in its name, the others in a folder"
        );
    }

    static string Found(string text) => string.Join("\n", FileFinder.Find(Files, text));
}
