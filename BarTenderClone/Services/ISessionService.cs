namespace BarTenderClone.Services
{
    public interface ISessionService
    {
        string? AccessToken { get; set; }
        bool IsAuthenticated { get; }
    }
}
