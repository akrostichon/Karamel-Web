namespace Karamel.Backend.Services
{
    public interface ITokenService
    {
        // Role-based token generation (defaults to "admin" for backward compat)
        string GenerateLinkToken(Guid sessionId, string role = "admin");
        
        // Returns (sessionId, role, isValid) tuple
        (Guid sessionId, string role, bool isValid) ValidateLinkToken(string token);
    }
}
