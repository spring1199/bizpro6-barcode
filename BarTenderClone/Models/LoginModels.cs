namespace BarTenderClone.Models
{
    public class LoginRequest
    {
        public string UserNameOrEmailAddress { get; set; }
        public string Password { get; set; }
        public string TenancyName { get; set; }
        public bool RememberClient { get; set; } = true;
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; }
        public string EncryptedAccessToken { get; set; }
        public int ExpireInSeconds { get; set; }
        public long UserId { get; set; }
    }
}
