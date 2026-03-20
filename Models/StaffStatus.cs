using System.ComponentModel.DataAnnotations;

namespace WebLinhKienPc.Models
{
    public class StaffStatus
    {
        [Key]
        public string StaffId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}