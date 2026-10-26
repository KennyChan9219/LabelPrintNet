namespace LabelPrint.Tests;

public class QueryTests
{
    [Fact]
    public void Member_lookup_keeps_the_value_in_a_parameter()
    {
        var query = LeagerQueries.FindByNumber(new LeagerTableOptions(), "A-100");

        Assert.Contains("[leager]", query.CommandText);
        Assert.Contains("[leag_no] = @leagNo", query.CommandText);
        Assert.DoesNotContain("A-100", query.CommandText);
        Assert.Equal("A-100", query.Parameters.Single().Value);
    }

    [Fact]
    public void Name_search_escapes_like_wildcards()
    {
        var query = LeagerQueries.SearchByName(new LeagerTableOptions(), "100%_[x]", 10);
        var pattern = Assert.IsType<string>(query.Parameters.Single(parameter => parameter.Name == "@name").Value);

        Assert.Contains("LIKE @name ESCAPE '\\'", query.CommandText);
        Assert.Equal(@"%100\%\_\[x]%", pattern);
        Assert.DoesNotContain("100%", query.CommandText);
    }

    [Fact]
    public void Optional_member_columns_stay_out_until_configured()
    {
        var sql = LeagerQueries.FindByNumber(new LeagerTableOptions(), "1").CommandText;

        Assert.Contains("[validdate] AS [ValidDate]", sql);
        Assert.DoesNotContain("MemberCode", sql);
        Assert.DoesNotContain("TempMember", sql);
    }

    [Fact]
    public void Configured_member_code_column_is_selected()
    {
        var options = new LeagerTableOptions { MemberCodeColumn = "member_code" };
        var sql = LeagerQueries.FindByNumber(options, "1").CommandText;

        Assert.Contains("[member_code] AS [MemberCode]", sql);
    }

    [Fact]
    public void Schema_qualified_table_is_bracketed()
    {
        var options = new LeagerTableOptions { Table = "dbo.leager" };
        var sql = LeagerQueries.FindByNumber(options, "1").CommandText;

        Assert.Contains("[dbo].[leager]", sql);
    }

    [Theory]
    [InlineData("leag_no; DROP TABLE leager")]
    [InlineData("leag_no]")]
    [InlineData("leag no")]
    [InlineData("")]
    public void Unsafe_identifiers_are_rejected(string column)
    {
        var options = new LeagerTableOptions { NumberColumn = column };

        Assert.Throws<InvalidOperationException>(() => LeagerQueries.FindByNumber(options, "1"));
    }

    [Fact]
    public void Employee_search_uses_both_name_columns_and_a_parameter()
    {
        var query = MtUserQueries.SearchByName(new MtUserTableOptions(), "O'Brien", 25);

        Assert.Contains("[MT_USER]", query.CommandText);
        Assert.Contains("[MU_Name] LIKE @name", query.CommandText);
        Assert.Contains("[MU_CHIName] LIKE @name", query.CommandText);
        Assert.DoesNotContain("O'Brien", query.CommandText);
        Assert.Equal("%O'Brien%", query.Parameters.Single(parameter => parameter.Name == "@name").Value);
    }

    [Fact]
    public void Placeholder_connection_string_is_not_usable()
    {
        var factory = new MisConnectionFactory(Options.Create(new MisConnectionOptions
        {
            Mis = "Server=YOUR_SQL_HOST;Database=MIS;User ID=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;"
        }));

        Assert.False(factory.IsConfigured);
        var error = Assert.Throws<MisNotConfiguredException>(() => factory.CreateConnection());
        Assert.Contains("placeholder", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_connection_string_is_not_usable()
    {
        var factory = new MisConnectionFactory(Options.Create(new MisConnectionOptions()));

        Assert.False(factory.IsConfigured);
        Assert.Throws<MisNotConfiguredException>(() => factory.CreateConnection());
    }

    [Fact]
    public void A_desk_connection_string_can_be_built_without_opening_it()
    {
        var factory = new MisConnectionFactory(Options.Create(new MisConnectionOptions
        {
            Mis = "Server=sql.example;Database=MIS;User ID=desk;Password=example-only;Encrypt=True;"
        }));

        Assert.True(factory.IsConfigured);
        using var connection = factory.CreateConnection();
        Assert.Contains("Database=MIS", connection.ConnectionString);
    }
}
