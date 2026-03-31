namespace BarTenderClone.Services
{
    public interface ISessionService
    {
        string? AccessToken { get; set; }
        int? TenantId { get; set; }
        string? ApiBaseUrl { get; set; }
        bool IsAuthenticated { get; }
    }
}
