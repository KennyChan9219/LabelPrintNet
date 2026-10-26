using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace LabelPrint;

/// <summary>
/// Renders a nameplate to a 300 DPI bitmap. TODO(on-machine): compare a print with the VB6 picture boxes
/// and adjust the .lbl millimetre positions. Legacy binary .lbl files are not painted as artwork.
/// </summary>
internal static class NameplateRenderer
{
    private const double DotsPerInch = 300.0;
    private const double MillimetresPerInch = 25.4;

    public static Bitmap Render(TemplateDocument layout, NameplateBinding binding)
    {
        var scale = DotsPerInch / MillimetresPerInch;
        var width = Math.Max(1, (int)Math.Round(layout.WidthMm * scale));
        var height = Math.Max(1, (int)Math.Round(layout.HeightMm * scale));
        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        bitmap.SetResolution((float)DotsPerInch, (float)DotsPerInch);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        DrawBackground(graphics, layout, width, height);
        foreach (var field in layout.Fields)
            DrawField(graphics, field, binding.Get(field.Source), scale);
        return bitmap;
    }

    private static void DrawBackground(Graphics graphics, TemplateDocument layout, int width, int height)
    {
        if (!string.IsNullOrWhiteSpace(layout.BackgroundPath) && File.Exists(layout.BackgroundPath))
        {
            using var image = Image.FromFile(layout.BackgroundPath);
            graphics.DrawImage(image, new Rectangle(0, 0, width, height));
            return;
        }

        using var pen = new Pen(Color.FromArgb(40, 40, 40), 3);
        graphics.DrawRectangle(pen, 6, 6, width - 13, height - 13);
    }

    private static void DrawField(Graphics graphics, TemplateField field, string text, double scale)
    {
        var rectangle = new RectangleF(
            (float)(field.Xmm * scale),
            (float)(field.Ymm * scale),
            (float)(field.WidthMm * scale),
            (float)(field.HeightMm * scale));
        if (rectangle.Width <= 1 || rectangle.Height <= 1 || text.Length == 0)
            return;

        using var font = new Font(
            field.FontFamily,
            field.SizePt,
            field.Bold ? FontStyle.Bold : FontStyle.Regular,
            GraphicsUnit.Point);
        using var brush = new SolidBrush(ParseColor(field.ColorHex));
        using var format = new StringFormat
        {
            Alignment = field.Align switch
            {
                TextAlign.Center => StringAlignment.Center,
                TextAlign.Right => StringAlignment.Far,
                _ => StringAlignment.Near
            },
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        graphics.DrawString(text, font, brush, rectangle, format);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch (Exception)
        {
            return Color.Black;
        }
    }
}
