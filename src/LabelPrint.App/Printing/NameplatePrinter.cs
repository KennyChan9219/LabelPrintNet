using System.Drawing.Printing;

namespace LabelPrint;

/// <summary>
/// Sends a rendered nameplate to the printer. Paper size comes from the template millimetres.
/// TODO(on-machine): label printers often ignore custom paper sizes. Confirm stock, margins, and driver
/// settings against a live print before turning this on at the desk.
/// </summary>
internal static class NameplatePrinter
{
    public static void Print(Bitmap image, TemplateDocument layout, string documentName, int copies, string? printerName)
    {
        using var document = Create(image, layout, documentName, copies, printerName);
        document.Print();
    }

    public static void Preview(IWin32Window owner, Bitmap image, TemplateDocument layout, string documentName, string? printerName)
    {
        using var document = Create(image, layout, documentName, 1, printerName);
        using var dialog = new PrintPreviewDialog
        {
            Document = document,
            Width = 900,
            Height = 700,
            StartPosition = FormStartPosition.CenterParent
        };
        dialog.ShowDialog(owner);
    }

    private static PrintDocument Create(
        Bitmap image,
        TemplateDocument layout,
        string documentName,
        int copies,
        string? printerName)
    {
        var document = new PrintDocument { DocumentName = documentName };
        document.PrinterSettings.Copies = (short)Math.Clamp(copies, 1, 20);
        if (!string.IsNullOrWhiteSpace(printerName))
            document.PrinterSettings.PrinterName = printerName;

        var paper = new PaperSize(
            "LabelPrint",
            HundredthsOfAnInch(layout.WidthMm),
            HundredthsOfAnInch(layout.HeightMm));
        document.DefaultPageSettings.PaperSize = paper;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.DefaultPageSettings.Landscape = layout.WidthMm > layout.HeightMm;
        document.PrintPage += (_, args) =>
        {
            args.Graphics?.DrawImage(image, args.PageBounds);
            args.HasMorePages = false;
        };
        return document;
    }

    private static int HundredthsOfAnInch(double millimetres) =>
        Math.Max(1, (int)Math.Round(millimetres / 25.4 * 100));
}
