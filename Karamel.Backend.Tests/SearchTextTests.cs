using Karamel.Backend.Services;
using Xunit;

namespace Karamel.Backend.Tests;

public class SearchTextTests
{
    [Fact]
    public void Fold_HandlesDiacriticsAndSpecialCharacters()
    {
        Assert.Equal("die arzte", SearchText.Fold("Die Ärzte"));
        Assert.Equal("strasse", SearchText.Fold("Straße"));
        Assert.Equal("sigur ros", SearchText.Fold("Sigur Rós"));
        Assert.Equal("motley crue", SearchText.Fold("Mötley Crüe"));
        Assert.Equal("", SearchText.Fold(null));
        Assert.Equal("", SearchText.Fold(string.Empty));
        Assert.Equal(SearchText.Fold("ärzte"), SearchText.Fold("ÄRZTE"));
        Assert.Equal("beyonce", SearchText.Fold("Beyoncé"));
    }
}
