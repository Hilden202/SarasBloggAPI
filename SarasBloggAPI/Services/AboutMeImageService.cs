using Microsoft.AspNetCore.Http;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.Services
{
    public class AboutMeImageService : IAboutMeImageService
    {
        private readonly AboutMeManager _aboutManager;
        private readonly IFileHelper _files;
        private const string Folder = "about"; // uploads/about/

        public AboutMeImageService(AboutMeManager aboutManager, IFileHelper files)
        {
            _aboutManager = aboutManager;
            _files = files;
        }

        public async Task<string?> GetCurrentUrlAsync()
        {
            var about = await _aboutManager.GetAsync();
            return about?.Image;
        }

        public async Task<string?> UploadOrReplaceAsync(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return null;

            var newUrl = await _files.SaveImageAsync(file, Folder);
            if (string.IsNullOrWhiteSpace(newUrl))
                return null;

            var about = await _aboutManager.GetAsync();
            if (about is null)
            {
                about = new AboutMe
                {
                    Title = "",
                    Content = "",
                    Image = newUrl,
                    UserId = ""
                };
                await _aboutManager.CreateAsync(about);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(about.Image))
                {
                    try { await _files.DeleteImageAsync(about.Image!, Folder); } catch { /* ignore */ }
                }

                about.Image = newUrl;
                await _aboutManager.UpdateAsync(about);
            }

            return newUrl;
        }

        public async Task DeleteAsync()
        {
            var about = await _aboutManager.GetAsync();
            if (about == null || string.IsNullOrWhiteSpace(about.Image))
                return;

            try { await _files.DeleteImageAsync(about.Image!, Folder); } catch { /* ignore */ }

            about.Image = null;
            await _aboutManager.UpdateAsync(about);
        }
    }
}
