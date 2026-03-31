namespace BarTenderClone.Services
{
    public interface ISessionService
    {
        string? AccessToken { get; set; }
        int? TenantId { get; set; }
        bool IsAuthenticated { get; }
    }
}
