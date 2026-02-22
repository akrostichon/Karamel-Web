using Xunit;
using Karamel.Backend.Contracts;
using Karamel.Backend.Models;

namespace Karamel.Backend.Tests
{
    public class SessionConfigDtoTests
    {
        [Fact]
        public void FromModel_ToModel_Roundtrip_PreservesValues()
        {
            var original = new SessionConfig
            {
                RequireSingerName = true,
                AllowSingersToReorder = false,
                PauseBetweenSongsSeconds = 15,
                Theme = "foo"
            };

            var dto = SessionConfigDto.FromModel(original);
            var roundtripped = dto.ToModel();

            Assert.Equal(original.RequireSingerName, roundtripped.RequireSingerName);
            Assert.Equal(original.AllowSingersToReorder, roundtripped.AllowSingersToReorder);
            Assert.Equal(original.PauseBetweenSongsSeconds, roundtripped.PauseBetweenSongsSeconds);
            Assert.Equal(original.Theme, roundtripped.Theme);
        }
    }
}
