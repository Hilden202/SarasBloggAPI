using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SarasBloggAPI.DTOs
{
    public class BloggImageUploadDto
    {
        [Required]
        public IFormFile File { get; set; }

        [Required]
        public int BloggId { get; set; }
    }
}