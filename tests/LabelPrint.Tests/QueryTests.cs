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
        Assert.Contains("佔位", error.Message, StringComparison.Ordinal);
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

    [Fact]
    public void Card_lookup_keeps_the_ic_id_in_a_parameter()
    {
        var count = LeagerQueries.CountActiveByCard(new LeagerTableOptions(), "AB'CD");
        var number = LeagerQueries.FindNumberByCard(new LeagerTableOptions(), "AB'CD");
        var move = LeagerQueries.RequestTransfer("AB'CD");

        Assert.Contains("[netflag] = 'Y'", count.CommandText);
        Assert.Contains("[CardNo] = @card", count.CommandText);
        Assert.DoesNotContain("AB'CD", count.CommandText);
        Assert.Contains("[CardNo] = @card", number.CommandText);
        Assert.Contains("CONVERT(varchar(10)", LeagerQueries.FindLabelByNumber(new LeagerTableOptions(), "1").CommandText);
        Assert.Contains("net_c_interface", move.CommandText);
        Assert.DoesNotContain("AB'CD", move.CommandText);
        Assert.Equal("Move by Card Print", move.Parameters.Single(parameter => parameter.Name == "@note").Value);
    }

    [Fact]
    public void Employee_card_lookup_uses_mu_id_and_a_parameter()
    {
        var query = MtUserQueries.FindByCardId(new MtUserTableOptions(), "10086");

        Assert.Contains("[MU_ID] = @id", query.CommandText);
        Assert.Contains("[mu_enable] = 1", query.CommandText);
        Assert.Contains("[mu_entryDate] DESC", query.CommandText);
        Assert.DoesNotContain("10086", query.CommandText);
        Assert.Equal("10086", query.Parameters.Single().Value);
    }

    [Fact]
    public void Name_length_chooses_the_vb6_template()
    {
        Assert.Equal(LabelTemplateSlot.MemberNameplate1, LabelChoice.ForMember(goodLuck: false, "陳大文"));
        Assert.Equal(LabelTemplateSlot.MemberNameplate2, LabelChoice.ForMember(goodLuck: false, new string('名', 15)));
        Assert.Equal(LabelTemplateSlot.MemberNameplate3, LabelChoice.ForMember(goodLuck: true, "短"));
        Assert.Equal(LabelTemplateSlot.EmployeeNameplate1, LabelChoice.ForEmployee("Chan"));
        Assert.Equal(LabelTemplateSlot.EmployeeNameplate2, LabelChoice.ForEmployee(new string('A', 15)));
        Assert.Equal("2026", LabelChoice.PrintedYear("2026/01/02"));
        Assert.Equal(LabelChoice.GoodLuckValidDate, LabelChoice.PrintedValidDate(goodLuck: true, "1999/01/01"));
        Assert.Equal("0102030405", CardCodec.FormatCardId([0x01, 0x02, 0x03, 0x04, 0x05]));
        Assert.True(CardCodec.IsBlankCardId("ffffffffff"));
    }
}
