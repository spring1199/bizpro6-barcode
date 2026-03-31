using BarTenderClone.Models;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public interface IAuthenticationService
    {
        Task<bool> LoginAsync(string tenancyName, string username, string password);
        string AccessToken { get; }
        bool IsAuthenticated { get; }
    }
}
