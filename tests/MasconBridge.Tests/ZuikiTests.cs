using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// Guards the verified device data. These values come from the Train Controller
/// Database and ConToJREts; if a test here fails, the data was changed, not the code.
/// </summary>
public class ZuikiTests
{
    [Fact]
    public void Descriptor_is_94_bytes()
    {
        Assert.Equal(188, Zuiki.DescriptorHex.Length);
        Assert.Equal(94, Convert.FromHexString(Zuiki.DescriptorHex).Length);
    }

    [Fact]
    public void Descriptor_is_valid_hex()
    {
        Assert.All(Zuiki.DescriptorHex, c => Assert.Contains(char.ToUpperInvariant(c), "0123456789ABCDEF"));
    }

    [Fact]
    public void There_are_fifteen_notches()
    {
        Assert.Equal(15, Zuiki.Notches.Length);
    }

    [Theory]
    [InlineData(0, "EB", 0x00)]
    [InlineData(1, "B8", 0x05)]
    [InlineData(2, "B7", 0x13)]
    [InlineData(3, "B6", 0x20)]
    [InlineData(4, "B5", 0x2E)]
    [InlineData(5, "B4", 0x3C)]
    [InlineData(6, "B3", 0x49)]
    [InlineData(7, "B2", 0x57)]
    [InlineData(8, "B1", 0x65)]
    [InlineData(9, "N", 0x80)]
    [InlineData(10, "P1", 0x9F)]
    [InlineData(11, "P2", 0xB7)]
    [InlineData(12, "P3", 0xCE)]
    [InlineData(13, "P4", 0xE6)]
    [InlineData(14, "P5", 0xFF)]
    public void Notch_values_match_the_documented_table(int index, string name, int value)
    {
        Assert.Equal(name, Zuiki.Notches[index].Name);
        Assert.Equal((byte)value, Zuiki.Notches[index].Value);
    }

    [Fact]
    public void Notch_values_increase_from_full_brake_to_full_power()
    {
        for (int i = 1; i < Zuiki.Notches.Length; i++)
            Assert.True(Zuiki.Notches[i].Value > Zuiki.Notches[i - 1].Value,
                $"{Zuiki.Notches[i].Name} is not above {Zuiki.Notches[i - 1].Name}");
    }

    [Fact]
    public void Report_has_the_declared_length()
    {
        var report = Zuiki.BuildReport(0x80, 0, Zuiki.HatCentered);
        Assert.Equal(Zuiki.InputReportSize, report.Length);
        Assert.Equal(8, report.Length);
    }

    [Fact]
    public void Report_carries_the_notch_in_byte_four()
    {
        foreach (var (_, value) in Zuiki.Notches)
            Assert.Equal(value, Zuiki.BuildReport(value, 0, Zuiki.HatCentered)[4]);
    }

    [Fact]
    public void Report_leaves_the_unused_axes_centred()
    {
        var report = Zuiki.BuildReport(0x00, 0xFFFF, 0x0F);
        Assert.Equal(0x80, report[3]);   // X
        Assert.Equal(0x80, report[5]);   // Z
        Assert.Equal(0x80, report[6]);   // Rz
        Assert.Equal(0x00, report[7]);   // padding
    }

    [Fact]
    public void Report_splits_buttons_across_the_first_two_bytes()
    {
        var report = Zuiki.BuildReport(0x80, 0b11_0000_1010_1010, Zuiki.HatCentered);
        Assert.Equal(0b1010_1010, report[0]);
        Assert.Equal(0b11_0000, report[1]);
    }

    [Fact]
    public void Report_masks_the_button_high_byte_to_six_bits()
    {
        // Bits 14 and 15 do not exist on the device and must not leak out.
        var report = Zuiki.BuildReport(0x80, 0xFFFF, Zuiki.HatCentered);
        Assert.Equal(0xFF, report[0]);
        Assert.Equal(0x3F, report[1]);
    }

    [Fact]
    public void Report_masks_the_hat_to_the_low_nibble()
    {
        var report = Zuiki.BuildReport(0x80, 0, 0xF8);
        Assert.Equal(0x08, report[2]);
    }

    [Fact]
    public void Every_mascon_button_has_a_distinct_bit_in_range()
    {
        Assert.Equal(12, Zuiki.ButtonBits.Count);
        Assert.Equal(Zuiki.ButtonBits.Count, Zuiki.ButtonBits.Values.Distinct().Count());
        Assert.All(Zuiki.ButtonBits.Values, bit => Assert.InRange(bit, 0, 13));
    }

    [Fact]
    public void Button_names_are_matched_regardless_of_case()
    {
        Assert.True(Zuiki.ButtonBits.ContainsKey("zl"));
        Assert.True(Zuiki.ButtonBits.ContainsKey("MINUS"));
    }

    [Fact]
    public void Known_models_are_six_and_have_unique_identities()
    {
        Assert.Equal(6, Zuiki.KnownModels.Length);

        var ids = Zuiki.KnownModels.Select(m => (m.Vid, m.Pid)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        var names = Zuiki.KnownModels.Select(m => m.Model).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Hat_centred_is_the_documented_value()
    {
        Assert.Equal(0x08, Zuiki.HatCentered);
    }
}
