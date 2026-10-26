namespace LabelPrint.Tests;

public class TemplateTests
{
    [Theory]
        [InlineData("Empnameplate1.lbl", "MemID")]
        [InlineData("Empnameplate2.lbl", "MemName")]
        [InlineData("nameplate1.lbl", "MemName")]
        [InlineData("nameplate2.lbl", "ValidDate")]
        [InlineData("nameplate3.lbl", "MemID")]
    public void Shipped_templates_parse(string fileName, string source)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "templates", fileName);
        var document = TextLayoutParser.Parse(File.ReadAllText(path), path);

        Assert.Equal(TextLayoutParser.FormatId, document.Format);
        Assert.Contains(document.Fields, field => field.Source == source);
    }

    [Fact]
    public void Unknown_source_is_rejected()
    {
        const string text = """
            Format=LabelPrint.TextLayout.v1
            Name=Bad
            WidthMm=10
            HeightMm=10
            [Field.Name]
            Source=NotAField
            Xmm=1
            Ymm=1
            WidthMm=2
            HeightMm=2
            """;

        var error = Assert.Throws<FormatException>(() => TextLayoutParser.Parse(text));
        Assert.Contains("NotAField", error.Message);
    }

    [Fact]
    public void Exe_directory_wins_over_the_templates_folder()
    {
        var root = Directory.CreateTempSubdirectory("lbl-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "templates"));
            File.WriteAllText(Path.Combine(root.FullName, "templates", "nameplate1.lbl"), Layout("from-templates"));
            File.WriteAllText(Path.Combine(root.FullName, "nameplate1.lbl"), Layout("from-exe"));
            var catalog = new LabelTemplateCatalog(root.FullName, configuredDirectory: null);

            var result = catalog.Load(LabelTemplateSlot.MemberNameplate1);

            Assert.Equal(TemplateLoadStatus.Loaded, result.Status);
            Assert.Equal("from-exe", result.Document?.Name);
            Assert.EndsWith("nameplate1.lbl", result.ResolvedPath);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Missing_and_binary_templates_are_reported()
    {
        var root = Directory.CreateTempSubdirectory("lbl-missing-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "Empnameplate1.lbl"), [0x00, 0x01, 0x02, 0x03, 0xFF]);
            var catalog = new LabelTemplateCatalog(root.FullName, configuredDirectory: null);

            var binary = catalog.Load(LabelTemplateSlot.EmployeeNameplate1);
            var missing = catalog.Load(LabelTemplateSlot.MemberNameplate3);

            Assert.Equal(TemplateLoadStatus.UnrecognizedBinary, binary.Status);
            Assert.Contains("TODO(on-machine)", binary.Message);
            Assert.Equal(TemplateLoadStatus.Missing, missing.Status);
            Assert.Contains("nameplate3.lbl", missing.Message);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Image_template_keeps_a_built_in_text_overlay()
    {
        var root = Directory.CreateTempSubdirectory("lbl-image-");
        try
        {
            var png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            File.WriteAllBytes(Path.Combine(root.FullName, "nameplate2.lbl"), png);
            var catalog = new LabelTemplateCatalog(root.FullName, configuredDirectory: null);

            var result = catalog.Load(LabelTemplateSlot.MemberNameplate2);

            Assert.Equal(TemplateLoadStatus.Loaded, result.Status);
            Assert.Equal(Path.Combine(root.FullName, "nameplate2.lbl"), result.Document?.BackgroundPath);
            Assert.Contains(result.Document!.Fields, field => field.Source == NameplateFields.MemName);
            Assert.Contains("TODO(on-machine)", result.Message);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Pipeline_uses_the_text_layout_and_falls_back_when_the_file_is_missing()
    {
        var root = Directory.CreateTempSubdirectory("lbl-pipe-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "Empnameplate1.lbl"), Layout("Desk badge"));
            var pipeline = new NameplatePrintPipeline(new LabelTemplateCatalog(root.FullName, null));
            var binding = NameplateBinding.ForEmployee("42", "Chan", "Chan", "", "");

            var loaded = pipeline.Create(LabelTemplateSlot.EmployeeNameplate1, binding);
            var missing = pipeline.Create(LabelTemplateSlot.EmployeeNameplate2, binding);

            Assert.False(loaded.UsedFallbackLayout);
            Assert.Equal("Desk badge", loaded.Layout.Name);
            Assert.Equal("Chan", loaded.Binding.Get(NameplateFields.EmpName));
            Assert.True(missing.UsedFallbackLayout);
            Assert.Equal("員工名牌 2", missing.Layout.Name);
            Assert.Contains(missing.Layout.Fields, field => field.Source == NameplateFields.MemName);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static string Layout(string name) => $$"""
        Format=LabelPrint.TextLayout.v1
        Name={{name}}
        WidthMm=75
        HeightMm=40
        [Field.Name]
        Source=EmpName
        Xmm=4
        Ymm=8
        WidthMm=67
        HeightMm=14
        """;
}
