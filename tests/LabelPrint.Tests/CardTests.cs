namespace LabelPrint.Tests;

public class CardTests
{
    [Fact]
    public void Member_number_is_ascii_and_nul_padded()
    {
        var encoded = CardCodec.EncodeMemberNumber("1234", 8);

        Assert.Equal(new byte[] { 0x31, 0x32, 0x33, 0x34, 0, 0, 0, 0 }, encoded);
        Assert.Equal("1234", CardCodec.DecodeMemberNumber(encoded));
    }

    [Fact]
    public void Non_ascii_member_numbers_are_rejected_before_a_card_write()
    {
        Assert.Throws<ArgumentException>(() => CardCodec.EncodeMemberNumber("陳", 16));
    }

    [Fact]
    public void Password_hex_accepts_separators_and_checks_length()
    {
        Assert.Null(CardCodec.ParsePassword(CardType.Sle4442, "  "));
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF }, CardCodec.ParsePassword(CardType.Sle4442, "FF FF FF"));
        Assert.Equal(new byte[] { 0x12, 0xAB }, CardCodec.ParsePassword(CardType.Sle4428, "12ab"));
        Assert.Throws<FormatException>(() => CardCodec.ParsePassword(CardType.Sle4442, "FFFF"));
        Assert.Throws<FormatException>(() => CardCodec.ParsePassword(CardType.Sle4442, "FFF"));
    }

    [Fact]
    public void Windows_outside_the_card_are_rejected()
    {
        CardCodec.ValidateWindow(CardType.Sle4442, 0, 256);
        Assert.Throws<ArgumentOutOfRangeException>(() => CardCodec.ValidateWindow(CardType.Sle4442, 250, 16));
        CardCodec.ValidateWindow(CardType.Sle4428, 1000, 24);
        Assert.Throws<ArgumentOutOfRangeException>(() => CardCodec.ValidateWindow(CardType.Sle4428, 1000, 25));
    }

    [Fact]
    public void Reader_config_round_trips_without_a_password()
    {
        var directory = Directory.CreateTempSubdirectory("reader-");
        try
        {
            var path = Path.Combine(directory.FullName, "CardReader.config");
            var settings = new CardReaderSettings
            {
                Model = "ACS ACR39U-CCID",
                Port = 100,
                CardType = CardType.Sle4428,
                InitType = 0
            };

            CardReaderConfigStore.Save(path, settings);
            var text = File.ReadAllText(path);
            var loaded = CardReaderConfigStore.Load(path);

            Assert.Equal("ACS ACR39U-CCID", loaded.Model);
            Assert.Equal(100, loaded.Port);
            Assert.Equal(CardType.Sle4428, loaded.CardType);
            Assert.Equal(0, loaded.InitType);
            Assert.DoesNotContain("Password=", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Reader_config_refuses_a_password_entry()
    {
        var error = Assert.Throws<InvalidOperationException>(() => CardReaderConfigStore.Parse("""
            [Reader]
            Model=ACS CCID USB Reader
            Port=100
            CardType=SLE4442
            Password=super-secret
            """));

        Assert.Contains("must not contain a password", error.Message);
        Assert.DoesNotContain("super-secret", error.Message);
    }

    [Fact]
    public void Shipped_reader_config_has_no_secret()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CardReader.config"));

        Assert.Contains("ACS CCID USB Reader", text);
        Assert.DoesNotContain("Password=", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Service_does_not_touch_native_code_without_windows_and_the_dll()
    {
        var directory = Directory.CreateTempSubdirectory("reader-svc-");
        try
        {
            var reader = new CardReaderService(
                Options.Create(new LabelPrintOptions()),
                directory.FullName,
                Path.Combine(directory.FullName, "override"));

            Assert.False(reader.NativeLibraryPresent);
            Assert.EndsWith("IC_D600.dll", reader.NativeLibraryPath);
            var probe = reader.Probe();
            Assert.False(probe.Success);

            var error = Assert.Throws<CardReaderException>(() => reader.ReadMemberArea());
            if (OperatingSystem.IsWindows())
                Assert.Contains("IC_D600.dll", error.Message);
            else
                Assert.Contains("Windows", error.Message);

            if (!OperatingSystem.IsWindows())
                Assert.Empty(reader.ListConnectedReaders());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Saved_reader_settings_reload()
    {
        var directory = Directory.CreateTempSubdirectory("reader-save-");
        try
        {
            var reader = new CardReaderService(
                Options.Create(new LabelPrintOptions()),
                directory.FullName,
                Path.Combine(directory.FullName, "override"));
            reader.SaveSettings(new CardReaderSettings
            {
                Model = "ACS ACR38U-CCID",
                Port = 100,
                CardType = CardType.Sle4442
            });

            reader.Reload();

            Assert.Equal("ACS ACR38U-CCID", reader.Settings.Model);
            Assert.Equal(CardType.Sle4442, reader.Settings.CardType);
            Assert.Contains("CardReader.config", reader.SettingsPath);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
