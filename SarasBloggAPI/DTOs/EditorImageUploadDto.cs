using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SarasBloggAPI.DTOs
{
    public class EditorImageUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = default!;
    }
}
