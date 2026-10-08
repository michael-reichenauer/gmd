using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// The list of every tag, from the commit menu's Tag menu, which shows a tag's commit, pushes a tag
// origin does not have, or removes one. In E2eRepo.CreateWithOriginAsync v1.0 is on 'Add delta' and
// was never pushed.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class TagsDlgTest
{
    // A push says so, and the list says the tag is on origin at once, not only after a fetch
    [TestMethod]
    public async Task TestPushATagFromTheList()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        OpenTags(gmd);
        Assert.AreEqual(
            """
                 ╭ Tags ──────────────────────────────────────────────────────────────────────────────────────────────────────╮
                 │  Tag                 Commit  Date        Origin      Subject                                               │
                 │ ┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐ │
                 │ │v1.0                17d85b  2024-10-15  not pushed  Add delta                                           │ │
                 │ └────────────────────────────────────────────────────────────────────────────────────────────────────────┘ │
                 │  v1.0 on 17d85b of 'main', not on origin: Push pushes it                                                   │
                 │ [ Show ]  [ Push ]  [ Remove ]                                                                  [ Close ]  │
                 ╰────────────────────────────────────────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("not pushed"), repo.Path, 16, 8)
        );

        gmd.Send("p");
        Assert.AreEqual("Pushed the tag 'v1.0' to origin", ScreenText.LastLine(gmd.WaitFor("Pushed the tag")));
        Assert.AreEqual("v1.0", (await repo.GitAsync($"-C \"{repo.Path}-origin\" tag")).Trim());

        OpenTags(gmd);
        StringAssert.Contains(gmd.WaitFor("on origin too"), "v1.0 on 17d85b of 'main', on origin too");
    }

    // Tag is five moves down the commit menu, which opens on 'Amend ...' since 'Add zeta' is not
    // pushed, past 'Commit Diff', 'Mark for Diff', Undo and Stash, and Tags is one below Add Tag in it, Remove Tag being disabled on a commit with no tag
    static void OpenTags(TmuxSession gmd)
    {
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (int i = 0; i < 5; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Add Tag");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");
    }
}
