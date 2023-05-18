
namespace Terminal.Gui;

public static class ViewExtensions
{
    public static string GetText(this TextField source) => source?.Text?.ToString()?.Trim() ?? "";
}

