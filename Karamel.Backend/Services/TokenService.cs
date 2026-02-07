using System.Security.Cryptography;
using System.Text;

namespace Karamel.Backend.Services
{
    public class TokenService : ITokenService
    {
        private readonly byte[] _secret;

        public TokenService(string secret)
        {
            if (string.IsNullOrEmpty(secret)) throw new ArgumentNullException(nameof(secret));
            _secret = Encoding.UTF8.GetBytes(secret);
        }

        /// <summary>
        /// Generates a role-based link token.
        /// Token format: Base64({sessionId}|{role}|{hmac})
        /// </summary>
        public string GenerateLinkToken(Guid sessionId, string role = "admin")
        {
            // Create payload: sessionId|role
            var payload = $"{sessionId}|{role}";
            var hmac = ComputeHmac(payload);
            
            // Combine payload and HMAC
            var tokenData = $"{payload}|{hmac}";
            
            // Use URL-safe base64 encoding
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Validates a link token and extracts session ID and role.
        /// Returns (sessionId, role, isValid) tuple.
        /// </summary>
        public (Guid sessionId, string role, bool isValid) ValidateLinkToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return (Guid.Empty, "", false);

            try
            {
                // Decode from URL-safe base64
                var base64 = token.Replace('-', '+').Replace('_', '/');
                // Add padding if needed
                var padding = (4 - base64.Length % 4) % 4;
                base64 = base64.PadRight(base64.Length + padding, '=');
                
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var parts = decoded.Split('|');
                
                // Expected format: {sessionId}|{role}|{hmac}
                if (parts.Length != 3)
                    return (Guid.Empty, "", false);
                
                if (!Guid.TryParse(parts[0], out var sessionId))
                    return (Guid.Empty, "", false);
                
                var role = parts[1];
                var providedHmac = parts[2];
                
                // Verify HMAC
                var payload = $"{sessionId}|{role}";
                var expectedHmac = ComputeHmac(payload);
                
                if (!AreEqualConstantTime(expectedHmac, providedHmac))
                    return (Guid.Empty, "", false);
                
                return (sessionId, role, true);
            }
            catch
            {
                return (Guid.Empty, "", false);
            }
        }

        private string ComputeHmac(string payload)
        {
            using var hmac = new HMACSHA256(_secret);
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static bool AreEqualConstantTime(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a ?? string.Empty);
            var bBytes = Encoding.UTF8.GetBytes(b ?? string.Empty);
            if (aBytes.Length != bBytes.Length) return false;
            int diff = 0;
            for (int i = 0; i < aBytes.Length; i++) diff |= aBytes[i] ^ bBytes[i];
            return diff == 0;
        }
    }
}
