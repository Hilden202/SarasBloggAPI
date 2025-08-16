using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SarasBloggAPI.Models
{
    [Table("Blogg")]
    public class Blogg
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Author { get; set; } = "";
        public ICollection<BloggImage>? Images { get; set; }
        public DateTime LaunchDate { get; set; }
        public bool IsArchived { get; set; } = false;
        public int ViewCount { get; set; }
        public bool Hidden { get; set; } = false;
        public string? UserId { get; set; }
    }
}