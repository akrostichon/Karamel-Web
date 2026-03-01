using Xunit;
using Karamel.Backend.Services;
using System;
using System.Text;

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
            var (_, isValid) = tokenService.ValidateLinkToken(token, sessionId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateLinkToken_RejectsInvalidToken()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);

            // Act
            var (_, isValid) = tokenService.ValidateLinkToken("invalid-token", Guid.NewGuid());

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateLinkToken_RejectsNullOrEmptyToken()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);

            // Act & Assert
            var (_, isValid1) = tokenService.ValidateLinkToken(null!, Guid.NewGuid());
            var (_, isValid2) = tokenService.ValidateLinkToken("", Guid.NewGuid());
            Assert.False(isValid1);
            Assert.False(isValid2);
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
            var (_, isValid) = tokenService.ValidateLinkToken(token, sessionId2);

            // Assert - token was generated for sessionId1 but validated with sessionId2
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
            var (_, isValid) = tokenService.ValidateLinkToken(standardBase64Token, Guid.NewGuid());

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

            // Assert - New format: {role}|{hmac} base64 encoded (no sessionId in payload)
            // "admin" (5) + "|" (1) + HMAC_base64url (43) = ~49 chars → base64 ≈ 68 chars
            Assert.True(token.Length > 60, $"Token length {token.Length} should be > 60");
            Assert.True(token.Length < 120, $"Token length {token.Length} should be < 120");
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

        // NEW: Role-based token tests
        [Fact]
        public void GenerateLinkToken_WithAdminRole_CreatesValidToken()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();

            // Act
            var token = tokenService.GenerateLinkToken(sessionId, "admin");

            // Assert - token should be URL-safe base64
            Assert.Matches(@"^[A-Za-z0-9\-_]+$", token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public void GenerateLinkToken_WithSingerRole_CreatesValidToken()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();

            // Act
            var token = tokenService.GenerateLinkToken(sessionId, "singer");

            // Assert - token should be URL-safe base64
            Assert.Matches(@"^[A-Za-z0-9\-_]+$", token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public void GenerateLinkToken_DifferentRoles_ProduceDifferentTokens()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();

            // Act
            var adminToken = tokenService.GenerateLinkToken(sessionId, "admin");
            var singerToken = tokenService.GenerateLinkToken(sessionId, "singer");

            // Assert - same session, different roles = different tokens
            Assert.NotEqual(adminToken, singerToken);
        }

        [Fact]
        public void ValidateLinkToken_WithValidAdminToken_ReturnsAdminRole()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();
            var token = tokenService.GenerateLinkToken(sessionId, "admin");

            // Act
            var (role, isValid) = tokenService.ValidateLinkToken(token, sessionId);

            // Assert
            Assert.True(isValid);
            Assert.Equal("admin", role);
        }

        [Fact]
        public void ValidateLinkToken_WithValidSingerToken_ReturnsSingerRole()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();
            var token = tokenService.GenerateLinkToken(sessionId, "singer");

            // Act
            var (role, isValid) = tokenService.ValidateLinkToken(token, sessionId);

            // Assert
            Assert.True(isValid);
            Assert.Equal("singer", role);
        }

        [Fact]
        public void ValidateLinkToken_WithTamperedRole_ReturnsFalse()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();
            var adminToken = tokenService.GenerateLinkToken(sessionId, "admin");
            
            // Decode the token and tamper with the role
            // New format is 2-part: {role}|{hmac}
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(
                adminToken.Replace('-', '+').Replace('_', '/').PadRight(adminToken.Length + (4 - adminToken.Length % 4) % 4, '=')));
            var parts = decoded.Split('|');
            
            // Tamper: change "admin" to "singer" but keep the original HMAC
            var tamperedPayload = $"singer|{parts[1]}";
            var tamperedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(tamperedPayload))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Act
            var (role, isValid) = tokenService.ValidateLinkToken(tamperedToken, sessionId);

            // Assert - HMAC should not match because role was changed
            Assert.False(isValid);
            Assert.Equal("", role);
        }

        [Fact]
        public void ValidateLinkToken_WithInvalidToken_ReturnsFalse()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);

            // Act
            var (role, isValid) = tokenService.ValidateLinkToken("invalid-token", Guid.NewGuid());

            // Assert
            Assert.False(isValid);
            Assert.Equal("", role);
        }

        [Fact]
        public void ValidateLinkToken_WithNullToken_ReturnsFalse()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);

            // Act
            var (role, isValid) = tokenService.ValidateLinkToken(null!, Guid.NewGuid());

            // Assert
            Assert.False(isValid);
            Assert.Equal("", role);
        }

        [Fact]
        public void GenerateLinkToken_DefaultRole_IsAdmin()
        {
            // Arrange
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();

            // Act - call without role parameter
            var token = tokenService.GenerateLinkToken(sessionId);
            var (role, isValid) = tokenService.ValidateLinkToken(token, sessionId);

            // Assert - should default to "admin" for backward compatibility
            Assert.True(isValid);
            Assert.Equal("admin", role);
        }

        [Fact]
        public void ValidateLinkToken_WithWrongSessionId_ReturnsFalse()
        {
            // Arrange - generate token for sessionId1 but validate with sessionId2
            var tokenService = new TokenService(TestSecret);
            var sessionId1 = Guid.NewGuid();
            var sessionId2 = Guid.NewGuid();
            var token = tokenService.GenerateLinkToken(sessionId1, "admin");

            // Act
            var (role, isValid) = tokenService.ValidateLinkToken(token, sessionId2);

            // Assert - HMAC is bound to sessionId1 so sessionId2 must be rejected
            Assert.False(isValid);
            Assert.Equal("", role);
        }

        [Fact]
        public void ValidateLinkToken_OldThreePartFormat_IsRejected()
        {
            // Arrange - manually construct a token in the OLD 3-part format: {sessionId}|{role}|{hmac}
            var tokenService = new TokenService(TestSecret);
            var sessionId = Guid.NewGuid();
            var fakeHmac = "fakehmacsignaturepadding0000000000000000000";
            var oldPayload = $"{sessionId}|admin|{fakeHmac}";
            var oldToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(oldPayload))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Act
            var (role, isValid) = tokenService.ValidateLinkToken(oldToken, sessionId);

            // Assert - 3-part format has parts.Length == 3, not 2, so it must be rejected
            Assert.False(isValid);
            Assert.Equal("", role);
        }
    }
}
