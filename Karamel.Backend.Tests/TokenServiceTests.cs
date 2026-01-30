using Xunit;
using Karamel.Backend.Services;
using System;

namespace Karamel.Backend.Tests
{
    public class TokenServiceTests
    {
        private const string TestSecret = "test-secret-key-for-token-generation";

        [Fact]
        public void GenerateLinkToken_ProducesUrlSafeBase64()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();

            // Act
            var token = tokenService.GenerateLinkToken(sessionId);

            // Assert - token should only contain URL-safe characters (A-Z, a-z, 0-9, -, _)
            Assert.Matches(@"^[A-Za-z0-9\-_]+$", token);
            Assert.DoesNotContain("+", token);
            Assert.DoesNotContain("/", token);
            Assert.DoesNotContain("=", token);
        }

        [Fact]
        public void GenerateLinkToken_ProducesConsistentOutput()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.Parse("104906d0-f2ef-4bc7-a2f6-dacdf8a5b2d3");

            // Act
            var token1 = tokenService.GenerateLinkToken(sessionId);
            var token2 = tokenService.GenerateLinkToken(sessionId);

            // Assert
            Assert.Equal(token1, token2);
        }

        [Fact]
        public void ValidateLinkToken_AcceptsValidToken()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();
            var token = tokenService.GenerateLinkToken(sessionId);

            // Act
            var isValid = tokenService.ValidateLinkToken(sessionId, token);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateLinkToken_RejectsInvalidToken()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();

            // Act
            var isValid = tokenService.ValidateLinkToken(sessionId, "invalid-token");

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateLinkToken_RejectsNullOrEmptyToken()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();

            // Act & Assert
            Assert.False(tokenService.ValidateLinkToken(sessionId, null!));
            Assert.False(tokenService.ValidateLinkToken(sessionId, ""));
        }

        [Fact]
        public void ValidateLinkToken_RejectsTokenForDifferentSession()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId1 = Guid.NewGuid();
            var sessionId2 = Guid.NewGuid();
            var token = tokenService.GenerateLinkToken(sessionId1);

            // Act
            var isValid = tokenService.ValidateLinkToken(sessionId2, token);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateLinkToken_RejectsStandardBase64Token()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();
            var urlSafeToken = tokenService.GenerateLinkToken(sessionId);
            
            // Simulate a standard base64 token by reversing the URL-safe conversion
            var standardBase64Token = urlSafeToken.Replace('-', '+').Replace('_', '/');

            // Act
            var isValid = tokenService.ValidateLinkToken(sessionId, standardBase64Token);

            // Assert - if the token contained + or /, this should fail
            // (only passes if the original token happened to have no + or / characters)
            if (standardBase64Token != urlSafeToken)
            {
                Assert.False(isValid);
            }
        }

        [Fact]
        public void GenerateLinkToken_ProducesExpectedLength()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();

            // Act
            var token = tokenService.GenerateLinkToken(sessionId);

            // Assert - HMACSHA256 produces 32 bytes, base64 encoding produces 43 chars (with padding removed)
            Assert.Equal(43, token.Length);
        }

        [Fact]
        public void GenerateLinkToken_ThrowsOnNullSecret()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TokenService(null!));
        }

        [Fact]
        public void GenerateLinkToken_ThrowsOnEmptySecret()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TokenService(""));
        }
    }
}
