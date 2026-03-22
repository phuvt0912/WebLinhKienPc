using System.ComponentModel.DataAnnotations;

namespace WebLinhKienPc.Models
{
    public class UserProfile
    {
        [Key]
        public string UserId { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}