using System;

namespace BarTenderClone.Services
{
    public interface ISessionService
    {
        string? AccessToken { get; set; }
        int? TenantId { get; set; }
        string? ApiBaseUrl { get; set; }
        string? TenancyName { get; set; }
        bool IsAuthenticated { get; }
        DateTime? TokenExpiresAt { get; set; }
        bool IsTokenExpired { get; }
    }
}
