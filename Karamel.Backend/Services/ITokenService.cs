namespace Karamel.Backend.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// Generates an admin token for the given session (grants full permissions).
        /// </summary>
        string GenerateAdminToken(Guid sessionId);

        /// <summary>
        /// Generates a singer token for the given session (grants limited read/add permissions).
        /// </summary>
        string GenerateSingerToken(Guid sessionId);

        /// <summary>
        /// Validates a token using the provided sessionId.
        /// Returns (role, isValid) tuple where role is "admin" or "singer".
        /// </summary>
        (string role, bool isValid) ValidateToken(string token, Guid sessionId);
    }
}
