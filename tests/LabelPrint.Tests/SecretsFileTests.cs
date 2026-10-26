namespace LabelPrint.Tests;

public class SecretsFileTests
{
    [Fact]
    public void Committed_appsettings_have_no_connection_string_or_pin()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Assert.Contains("\"Mis\": \"\"", text);
        Assert.Contains("\"PasswordHex\": \"\"", text);
        Assert.DoesNotContain("YOUR_PASSWORD", text);
        Assert.DoesNotContain("Server=", text);
    }

    [Fact]
    public void Local_sample_uses_placeholders_only()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.Local.sample.json"));

        Assert.Contains("YOUR_SQL_HOST", text);
        Assert.Contains("YOUR_USER", text);
        Assert.Contains("YOUR_PASSWORD", text);
        Assert.Contains("\"PasswordHex\": \"\"", text);
        Assert.DoesNotContain("Server=10.", text);
        Assert.DoesNotContain("Server=192.", text);
    }

    [Fact]
    public void Employee_binding_prefers_the_printed_name()
    {
        var binding = NameplateBinding.ForEmployee("88", "陳大文", "Chan Tai Man", "陳大文", "A123");

        Assert.Equal("陳大文", binding.Get(NameplateFields.EmpName));
        Assert.Equal("Chan Tai Man", binding.Get(NameplateFields.EmpEnglishName));
        Assert.Equal("88", binding.Get(NameplateFields.StaffNo));
        Assert.Equal("", binding.Get(NameplateFields.LeagName));
    }
}
