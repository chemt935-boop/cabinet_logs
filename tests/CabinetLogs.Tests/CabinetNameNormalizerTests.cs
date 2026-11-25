using CabinetLogs.Domain.Common;

namespace CabinetLogs.Tests;

public sealed class CabinetNameNormalizerTests
{
    [Theory]
    [InlineData("LAF-01", "LAF-01", "laf-01")]
    [InlineData("  laf-01  ", "laf-01", "laf-01")]
    [InlineData("  Fume   Hood  A  ", "Fume Hood A", "fume hood a")]
    [InlineData("BSC\t-\n02", "BSC - 02", "bsc - 02")]
    public void Normalizer_CleansAndNormalizesConsistently(string raw, string expectedDisplay, string expectedNormalized)
    {
        Assert.Equal(expectedDisplay, CabinetNameNormalizer.CleanDisplayName(raw));
        Assert.Equal(expectedNormalized, CabinetNameNormalizer.Normalize(raw));
    }
}
