using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SarasBloggAPI.Models
{
    public class BloggImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public int BloggId { get; set; }

        [ForeignKey("BloggId")]
        public Blogg? Blogg { get; set; }
        public int Order { get; set; }
    }

}
