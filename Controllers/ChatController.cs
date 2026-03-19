using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.Controllers
{
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _config;

        public ChatController(ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IConfiguration config)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
        }

        // ===== USER: Lấy lịch sử chat =====
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMessages()
        {
            var userId = _userManager.GetUserId(User);
            var messages = await _context.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new {
                    m.Id,
                    m.Content,
                    m.IsFromUser,
                    m.IsFromAI,
                    time = m.CreatedAt.ToString("HH:mm")
                })
                .ToListAsync();

            return Json(messages);
        }

        // ===== USER: Gửi tin nhắn =====
        [HttpPost]
        [IgnoreAntiforgeryToken] // ← fetch JSON không gửi form token
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { success = false, requireLogin = true });

            if (string.IsNullOrWhiteSpace(req?.Content))
                return Json(new { success = false, message = "Nội dung không được trống." });

            var userId = _userManager.GetUserId(User);

            // Lưu tin nhắn user
            var userMsg = new ChatMessage
            {
                UserId = userId,
                Content = req.Content.Trim(),
                IsFromUser = true,
                IsFromAI = false
            };
            _context.ChatMessages.Add(userMsg);
            await _context.SaveChangesAsync();

            // Kiểm tra nhân viên có online không
            // (nhân viên đã trả lời trong 10 phút gần đây)
            var hasStaffOnline = await _context.ChatMessages
                .AnyAsync(m => m.UserId == userId
                            && !m.IsFromUser
                            && !m.IsFromAI
                            && m.CreatedAt >= DateTime.Now.AddMinutes(-10));

            if (hasStaffOnline)
            {
                return Json(new { success = true, waitingForStaff = true });
            }

            // Không có nhân viên → Gemini AI trả lời
            var aiReply = await CallGeminiAI(req.Content, userId);

            var aiMsg = new ChatMessage
            {
                UserId = userId,
                Content = aiReply,
                IsFromUser = false,
                IsFromAI = true
            };
            _context.ChatMessages.Add(aiMsg);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                reply = aiReply,
                isAI = true,
                time = DateTime.Now.ToString("HH:mm")
            });
        }

        // ===== STAFF: Danh sách user đang chat =====
        [Authorize(Roles = "Admin,NhanVien")]
        [HttpGet]
        public async Task<IActionResult> StaffGetUsers()
        {
            var userIds = await _context.ChatMessages
                .Where(m => m.IsFromUser)
                .GroupBy(m => m.UserId)
                .Select(g => new {
                    userId = g.Key,
                    lastMessage = g.OrderByDescending(m => m.CreatedAt).First().Content,
                    lastTime = g.OrderByDescending(m => m.CreatedAt).First().CreatedAt.ToString("HH:mm dd/MM"),
                    unread = g.Count(m => m.IsFromUser && !m.IsRead)
                })
                .ToListAsync();

            var result = new List<object>();
            foreach (var u in userIds)
            {
                var user = await _userManager.FindByIdAsync(u.userId);
                result.Add(new
                {
                    u.userId,
                    email = user?.Email ?? "Unknown",
                    username = user?.UserName ?? "Unknown",
                    u.lastMessage,
                    u.lastTime,
                    u.unread
                });
            }

            return Json(result);
        }

        // ===== STAFF: Tin nhắn của 1 user =====
        [Authorize(Roles = "Admin,NhanVien")]
        [HttpGet]
        public async Task<IActionResult> StaffGetMessages(string userId)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new {
                    m.Id,
                    m.Content,
                    m.IsFromUser,
                    m.IsFromAI,
                    time = m.CreatedAt.ToString("HH:mm")
                })
                .ToListAsync();

            // Đánh dấu đã đọc
            var unread = await _context.ChatMessages
                .Where(m => m.UserId == userId && m.IsFromUser && !m.IsRead)
                .ToListAsync();

            unread.ForEach(m => m.IsRead = true);
            await _context.SaveChangesAsync();

            return Json(messages);
        }

        // ===== STAFF: Trả lời user =====
        [Authorize(Roles = "Admin,NhanVien")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StaffReply([FromBody] StaffReplyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Content) || string.IsNullOrWhiteSpace(req?.UserId))
                return BadRequest();

            var staffId = _userManager.GetUserId(User);

            var msg = new ChatMessage
            {
                UserId = req.UserId,
                StaffId = staffId,
                Content = req.Content.Trim(),
                IsFromUser = false,
                IsFromAI = false
            };
            _context.ChatMessages.Add(msg);
            await _context.SaveChangesAsync();

            return Json(new { success = true, time = DateTime.Now.ToString("HH:mm") });
        }

        // ===== View Staff Chat =====
        [Authorize(Roles = "Admin,NhanVien")]
        public IActionResult StaffChat() => View();

        // ===== Gọi Gemini AI =====
        private async Task<string> CallGeminiAI(string userMessage, string userId)
        {
            try
            {
                // Lấy 10 tin nhắn gần nhất làm context
                var history = await _context.ChatMessages
                    .Where(m => m.UserId == userId)
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(10)
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync();

                var contents = new List<object>();
                foreach (var h in history)
                {
                    contents.Add(new
                    {
                        role = h.IsFromUser ? "user" : "model",
                        parts = new[] { new { text = h.Content } }
                    });
                }

                // Đảm bảo contents không rỗng và bắt đầu bằng role "user"
                if (contents.Count == 0)
                {
                    contents.Add(new
                    {
                        role = "user",
                        parts = new[] { new { text = userMessage } }
                    });
                }

                var apiKey = _config["Gemini:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return "Xin lỗi, hệ thống AI chưa được cấu hình. Vui lòng chờ nhân viên hỗ trợ.";

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var body = new
                {
                    system_instruction = new
                    {
                        parts = new[] { new {
                            text = "Bạn là trợ lý hỗ trợ khách hàng của cửa hàng linh kiện máy tính LinhKienPC. Trả lời ngắn gọn, thân thiện bằng tiếng Việt. Chỉ tư vấn về linh kiện máy tính, phần cứng, đơn hàng. Nếu không biết thì nói khách chờ nhân viên hỗ trợ."
                        }}
                    },
                    contents = contents,
                    generationConfig = new
                    {
                        maxOutputTokens = 400,
                        temperature = 0.7
                    }
                };

                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync(url, content);
                var resJson = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                    return "Xin lỗi, AI tạm thời không khả dụng. Vui lòng chờ nhân viên hỗ trợ.";

                var parsed = JsonDocument.Parse(resJson);
                return parsed.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString()
                    ?? "Xin lỗi, tôi không hiểu yêu cầu của bạn.";
            }
            catch (TaskCanceledException)
            {
                return "AI phản hồi quá chậm. Vui lòng chờ nhân viên hỗ trợ.";
            }
            catch
            {
                return "Xin lỗi, hệ thống AI tạm thời không khả dụng. Vui lòng chờ nhân viên hỗ trợ.";
            }
        }
    }

    public class SendMessageRequest { public string Content { get; set; } }
    public class StaffReplyRequest { public string UserId { get; set; } public string Content { get; set; } }
}