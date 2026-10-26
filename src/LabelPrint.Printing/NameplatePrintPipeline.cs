namespace LabelPrint.Printing;

public sealed class NameplatePrintPipeline : INameplatePrintPipeline
{
    private readonly ILabelTemplateCatalog _catalog;

    public NameplatePrintPipeline(ILabelTemplateCatalog catalog)
    {
        _catalog = catalog;
    }

    public NameplateJob Create(LabelTemplateSlot slot, NameplateBinding binding)
    {
        var template = _catalog.Load(slot);
        var layout = template.Status == TemplateLoadStatus.Loaded && template.Document is not null
            ? template.Document
            : BuiltInLayouts.For(slot);

        return new NameplateJob
        {
            Template = template,
            Binding = binding,
            Layout = layout,
            DocumentName = string.IsNullOrWhiteSpace(layout.Name) ? slot.FileName() : layout.Name,
            Copies = Math.Clamp(layout.Copies, 1, 20)
        };
    }
}
