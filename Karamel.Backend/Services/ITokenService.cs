namespace Karamel.Backend.Services
{
    public interface ITokenService
    {
        // NEW: Role-based token generation (defaults to "admin" for backward compat)
        string GenerateLinkToken(Guid sessionId, string role = "admin");
        
        // DEPRECATED: Old signature for backward compatibility
        bool ValidateLinkToken(Guid sessionId, string token);
        
        // NEW: Returns (sessionId, role, isValid) tuple
        (Guid sessionId, string role, bool isValid) ValidateLinkToken(string token);
    }
}
