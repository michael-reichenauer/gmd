
namespace Terminal.Gui;

public static class ViewExtensions
{
    public static string GetText(this View source) => source?.Text?.ToString()?.Trim() ?? "";
}

