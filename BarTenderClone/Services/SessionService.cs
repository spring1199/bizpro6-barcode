namespace BarTenderClone.Services
{
    public class SessionService : ISessionService
    {
        public string? AccessToken { get; set; }
        public int? TenantId { get; set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
    }
}
