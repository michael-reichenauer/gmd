

namespace gmd.Cui.TerminalGui;

internal class TextViewEx : TextViewX
{
    private ColorText colorText;

    internal TextViewEx(ColorText colorText) : base()
    {
        this.colorText = colorText;
    }

    protected override void SetNormalColor()
    {
        Driver.SetAttribute(Colors.White);
    }

    protected override void SetReadOnlyColor(List<System.Rune> line, int x, int row)
    {
        Driver.SetAttribute(colorText.GetColor(x, row));
    }
}