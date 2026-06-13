using System.Globalization;
using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

public sealed class DecimalInputScenarioTests
{
    [Theory]
    [InlineData("3.5", 3.5)]
    [InlineData("3,5", 3.5)]
    [InlineData("0.25", 0.25)]
    [InlineData("0,25", 0.25)]
    [InlineData("  2  ", 2)]
    public void TryParse_accepts_both_decimal_separators(string input, double expected)
    {
        Assert.True(DecimalInput.TryParse(input, out double value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void TryParse_rejects_invalid_input(string input)
    {
        Assert.False(DecimalInput.TryParse(input, out _));
    }

    [Fact]
    public void TryParse_accepts_comma_when_current_culture_uses_comma()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.True(DecimalInput.TryParse("3,5", out double commaValue));
            Assert.Equal(3.5, commaValue);
            Assert.True(DecimalInput.TryParse("3.5", out double dotValue));
            Assert.Equal(3.5, dotValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
