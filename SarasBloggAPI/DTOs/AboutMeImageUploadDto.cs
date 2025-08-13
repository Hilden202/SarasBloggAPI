using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SarasBloggAPI.DTOs
{
    public class AboutMeImageUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = default!;
    }
}