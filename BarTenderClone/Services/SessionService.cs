using System;

namespace BarTenderClone.Services
{
    public class SessionService : ISessionService
    {
        public string? AccessToken { get; set; }
        public int? TenantId { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string? TenancyName { get; set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
        public DateTime? TokenExpiresAt { get; set; }
        public bool IsTokenExpired => TokenExpiresAt.HasValue && DateTime.UtcNow >= TokenExpiresAt.Value;
    }
}
