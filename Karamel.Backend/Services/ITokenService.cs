namespace Karamel.Backend.Services
{
    public interface ITokenService
    {
        // Role-based token generation (defaults to "admin" for backward compat)
        string GenerateLinkToken(Guid sessionId, string role = "admin");
        
        /// <summary>
        /// Validates a link token using the provided sessionId (supplied by caller from URL).
        /// Returns (role, isValid) tuple. SessionId is not stored in the token.
        /// </summary>
        (string role, bool isValid) ValidateLinkToken(string token, Guid sessionId);
    }
}
