namespace SarasBloggAPI.Services
{
    public interface IFileHelper
    {
        Task<string?> SaveImageAsync(IFormFile file, string folderName); // bakåt-compat
        Task<string?> SaveImageAsync(IFormFile file, int bloggId, string folderName = "blogg");
        Task DeleteImageAsync(string imageUrl, string folder);
        Task DeleteBlogFolderAsync(int bloggId, string folderName = "blogg");
    }
}