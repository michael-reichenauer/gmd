using Terminal.Gui;


namespace gmd.Cui;

internal class UI
{

    internal static void Post(Action action)
    {
        Application.MainLoop.Invoke(action);
    }


    internal static object AddTimeout(TimeSpan timeout, Func<MainLoop, bool> callback)
    {
        return Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(20), callback);
    }

}