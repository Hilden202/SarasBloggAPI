namespace SarasBloggAPI.Services
{
    public interface IFileHelper
    {
        Task<string> SaveImageAsync(IFormFile file, string folder);
        Task DeleteImageAsync(string imageUrl, string folder);
    }
}