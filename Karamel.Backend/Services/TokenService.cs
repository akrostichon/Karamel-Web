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
        /// Token format: Base64url({role}|{hmac})
        /// HMAC is computed over "{sessionId}:{role}" so the token is bound to the sessionId
        /// without embedding it in the token payload.
        /// </summary>
        public string GenerateLinkToken(Guid sessionId, string role = "admin")
        {
            // HMAC input binds token to sessionId using ':' separator (distinct from '|' in payload)
            var hmac = ComputeHmac($"{sessionId}:{role}");
            
            // Token payload: role|hmac (no sessionId — caller already has it in the URL)
            var tokenData = $"{role}|{hmac}";
            
            // Use URL-safe base64 encoding
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Validates a link token using the provided sessionId (supplied by caller from URL).
        /// Token format: Base64url({role}|{hmac})
        /// HMAC is verified over "{sessionId}:{role}" to bind the token to the session.
        /// Returns (role, isValid) tuple.
        /// </summary>
        public (string role, bool isValid) ValidateLinkToken(string token, Guid sessionId)
        {
            if (string.IsNullOrEmpty(token))
                return ("", false);

            try
            {
                // Decode from URL-safe base64
                var base64 = token.Replace('-', '+').Replace('_', '/');
                // Add padding if needed
                var padding = (4 - base64.Length % 4) % 4;
                base64 = base64.PadRight(base64.Length + padding, '=');
                
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var parts = decoded.Split('|');
                
                // Expected format: {role}|{hmac}
                if (parts.Length != 2)
                    return ("", false);
                
                var role = parts[0];
                var providedHmac = parts[1];
                
                // Verify HMAC: bound to sessionId via ':' separator
                var expectedHmac = ComputeHmac($"{sessionId}:{role}");
                
                if (!AreEqualConstantTime(expectedHmac, providedHmac))
                    return ("", false);
                
                return (role, true);
            }
            catch
            {
                return ("", false);
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
