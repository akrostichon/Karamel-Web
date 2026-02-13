namespace Karamel.Backend.Models
{
    /// <summary>
    /// Constants related to session management and lifecycle.
    /// </summary>
    public static class SessionConstants
    {
        /// <summary>
        /// Default session TTL (Time To Live) in minutes. 
        /// Sessions with explicit ExpiresAt are cleaned up when expired.
        /// Sessions without ExpiresAt are cleaned up after this duration from CreatedAt.
        /// </summary>
        public const int DefaultTtlMinutes = 30;
    }
}
