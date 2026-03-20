namespace WebLinhKienPc.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string? StaffId { get; set; }
        public string Content { get; set; }
        public bool IsFromUser { get; set; }
        public bool IsFromAI { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;

        // Thêm trường này để lưu products dạng JSON
        public string? ProductsJson { get; set; }
    }
}