using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace SarasBloggAPI.Services
{
    public interface IAboutMeImageService
    {
        Task<string?> GetCurrentUrlAsync();
        Task<string?> UploadOrReplaceAsync(IFormFile file);
        Task DeleteAsync();
    }
}