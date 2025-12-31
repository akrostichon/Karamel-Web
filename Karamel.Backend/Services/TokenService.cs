using System;
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

        public string GenerateLinkToken(Guid sessionId)
        {
            using var hmac = new HMACSHA256(_secret);
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(sessionId.ToString()));
            return Convert.ToBase64String(bytes).TrimEnd('=');
        }

        public bool ValidateLinkToken(Guid sessionId, string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            var expected = GenerateLinkToken(sessionId);
            // constant time compare
            return AreEqualConstantTime(expected, token);
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
