namespace Karamel.Backend.Services
{
    public interface ITokenService
    {
        string GenerateLinkToken(Guid sessionId);
        bool ValidateLinkToken(Guid sessionId, string token);
    }
}
