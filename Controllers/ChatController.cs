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
        private static readonly Dictionary<string, DateTime> _lastUserRequest = new();
        private static readonly object _lock = new object();
        private static int _apiFailureCount = 0;
        private static DateTime _lastFailureTime = DateTime.Now;

        public ChatController(ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IConfiguration config)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
        }

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

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                    return Json(new { success = false, requireLogin = true });

                if (string.IsNullOrWhiteSpace(req?.Content))
                    return Json(new { success = false, message = "Nội dung không được trống." });

                var userId = _userManager.GetUserId(User);

                // KIỂM TRA SPAM
                lock (_lock)
                {
                    if (_lastUserRequest.ContainsKey(userId))
                    {
                        var timeSinceLastRequest = DateTime.Now - _lastUserRequest[userId];
                        if (timeSinceLastRequest.TotalSeconds < 3)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "Vui lòng chờ 3 giây giữa các tin nhắn."
                            });
                        }
                    }
                    _lastUserRequest[userId] = DateTime.Now;
                }

                // Lưu tin nhắn user
                var userMsg = new ChatMessage
                {
                    UserId = userId,
                    Content = req.Content.Trim(),
                    IsFromUser = true,
                    IsFromAI = false,
                    CreatedAt = DateTime.Now
                };
                _context.ChatMessages.Add(userMsg);
                await _context.SaveChangesAsync();

                // Check nhân viên online
                var hasStaffOnline = await _context.StaffStatuses.AnyAsync(s => s.IsOnline);

                if (hasStaffOnline)
                {
                    return Json(new
                    {
                        success = true,
                        waitingForStaff = true,
                        message = "Đã gửi tin nhắn, nhân viên sẽ phản hồi sớm!"
                    });
                }

                // KHÔNG CÓ NHÂN VIÊN - GỌI AI
                var aiReply = await CallGeminiAI(req.Content);

                // Lưu tin nhắn AI
                var aiMsg = new ChatMessage
                {
                    UserId = userId,
                    Content = aiReply,
                    IsFromUser = false,
                    IsFromAI = true,
                    CreatedAt = DateTime.Now
                };
                _context.ChatMessages.Add(aiMsg);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    reply = aiMsg.Content,
                    isAI = true,
                    time = DateTime.Now.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendMessage Error: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "Có lỗi xảy ra, vui lòng thử lại."
                });
            }
        }

        // ===== STAFF: Bật/tắt online =====
        [Authorize(Roles = "Admin,NhanVien")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetOnlineStatus([FromBody] bool isOnline)
        {
            var staffId = _userManager.GetUserId(User);
            var status = await _context.StaffStatuses
                .FirstOrDefaultAsync(s => s.StaffId == staffId);

            if (status == null)
            {
                _context.StaffStatuses.Add(new StaffStatus
                {
                    StaffId = staffId,
                    IsOnline = isOnline,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                status.IsOnline = isOnline;
                status.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isOnline });
        }

        // ===== STAFF: Lấy trạng thái online của mình =====
        [Authorize(Roles = "Admin,NhanVien")]
        [HttpGet]
        public async Task<IActionResult> GetMyStatus()
        {
            var staffId = _userManager.GetUserId(User);
            var status = await _context.StaffStatuses
                .FirstOrDefaultAsync(s => s.StaffId == staffId);

            return Json(new { isOnline = status?.IsOnline ?? false });
        }

        [Authorize(Roles = "Admin,NhanVien")]
        [HttpGet]
        public async Task<IActionResult> StaffGetUsers()
        {
            var userIds = await _context.ChatMessages
                .Where(m => m.IsFromUser)
                .GroupBy(m => m.UserId)
                .Select(g => new
                {
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

        [Authorize(Roles = "Admin,NhanVien")]
        [HttpGet]
        public async Task<IActionResult> StaffGetMessages(string userId)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.IsFromUser,
                    m.IsFromAI,
                    time = m.CreatedAt.ToString("HH:mm")
                })
                .ToListAsync();

            var unread = await _context.ChatMessages
                .Where(m => m.UserId == userId && m.IsFromUser && !m.IsRead)
                .ToListAsync();

            foreach (var msg in unread)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();

            return Json(messages);
        }

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
                IsFromAI = false,
                CreatedAt = DateTime.Now
            };
            _context.ChatMessages.Add(msg);
            await _context.SaveChangesAsync();

            return Json(new { success = true, time = DateTime.Now.ToString("HH:mm") });
        }

        [Authorize(Roles = "Admin,NhanVien")]
        public IActionResult StaffChat() => View();

        private async Task<string> CallGeminiAI(string userMessage)
        {
            try
            {
                // Kiểm tra nếu API đang lỗi liên tục thì dùng mock luôn
                if (_apiFailureCount > 5 && (DateTime.Now - _lastFailureTime).TotalMinutes < 30)
                {
                    return GetMockResponse(userMessage);
                }

                var apiKey = _config["Gemini:ApiKey"];

                if (string.IsNullOrEmpty(apiKey))
                {
                    return GetMockResponse(userMessage);
                }

                // Danh sách models để thử
                string[] models = new[]
                {
                    "gemini-1.5-flash",
                    "gemini-1.5-flash-8b",
                    "gemini-2.0-flash-exp",
                    "gemini-pro"
                };

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                foreach (var model in models)
                {
                    try
                    {
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                        var body = new
                        {
                            contents = new[]
                            {
                                new
                                {
                                    parts = new[]
                                    {
                                        new { text = userMessage }
                                    }
                                }
                            }
                        };

                        var json = JsonSerializer.Serialize(body);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(url, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseJson = await response.Content.ReadAsStringAsync();
                            var parsed = JsonDocument.Parse(responseJson);

                            var text = parsed.RootElement
                                .GetProperty("candidates")[0]
                                .GetProperty("content")
                                .GetProperty("parts")[0]
                                .GetProperty("text")
                                .GetString();

                            // Reset failure count khi thành công
                            _apiFailureCount = 0;
                            return text;
                        }
                        else if ((int)response.StatusCode == 429 || (int)response.StatusCode == 403 || (int)response.StatusCode == 404)
                        {
                            // Lỗi quota hoặc không có quyền - thử model tiếp
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Nếu tất cả đều lỗi
                _apiFailureCount++;
                _lastFailureTime = DateTime.Now;

                return GetMockResponse(userMessage);
            }
            catch
            {
                _apiFailureCount++;
                _lastFailureTime = DateTime.Now;

                return GetMockResponse(userMessage);
            }
        }

        private string GetMockResponse(string userMessage)
        {
            var random = new Random();
            userMessage = userMessage.ToLower();

            // Phản hồi theo từ khóa
            if (userMessage.Contains("xin chào") || userMessage.Contains("hello") || userMessage.Contains("hi") || userMessage.Contains("chào"))
            {
                string[] greetings = new[]
                {
                    "Xin chào! Tôi là trợ lý ảo của Linh Kiện PC. Bạn cần tư vấn gì ạ?",
                    "Chào bạn! Rất vui được hỗ trợ bạn. Bạn quan tâm đến linh kiện nào?",
                    "Xin chào! Bạn cần tìm hiểu thông tin về sản phẩm gì?",
                    "Chào bạn! Tôi có thể giúp gì cho bạn hôm nay?"
                };
                return greetings[random.Next(greetings.Length)];
            }

            if (userMessage.Contains("giá") || userMessage.Contains("bao nhiêu") || userMessage.Contains("giá cả"))
            {
                string[] priceResponses = new[]
                {
                    "Vui lòng cho tôi biết tên sản phẩm bạn quan tâm để tôi kiểm tra giá nhé!",
                    "Bạn có thể xem giá chi tiết tại danh mục sản phẩm trên website.",
                    "Hiện tại có nhiều sản phẩm đang được giảm giá, bạn muốn tham khảo dòng nào?",
                    "Giá sản phẩm thay đổi tùy theo cấu hình. Bạn quan tâm đến sản phẩm cụ thể nào không?"
                };
                return priceResponses[random.Next(priceResponses.Length)];
            }

            if (userMessage.Contains("cấu hình") || userMessage.Contains("main") || userMessage.Contains("ram") ||
                userMessage.Contains("cpu") || userMessage.Contains("card") || userMessage.Contains("vga") ||
                userMessage.Contains("bộ nhớ") || userMessage.Contains("ổ cứng"))
            {
                string[] configResponses = new[]
                {
                    "Bạn muốn tư vấn cấu hình máy tính cho mục đích gì? (Văn phòng, Gaming, Đồ họa)",
                    "Bạn có ngân sách khoảng bao nhiêu cho bộ cấu hình này?",
                    "Chúng tôi có nhiều lựa chọn linh kiện phù hợp với nhu cầu của bạn.",
                    "Bạn có thể tham khảo các bộ cấu hình có sẵn trên website hoặc để lại thông tin để được tư vấn chi tiết."
                };
                return configResponses[random.Next(configResponses.Length)];
            }

            if (userMessage.Contains("cảm ơn"))
            {
                string[] thankResponses = new[]
                {
                    "Không có gì ạ! Nếu cần thêm thông tin, bạn cứ hỏi nhé.",
                    "Rất vui được hỗ trợ bạn! Chúc bạn một ngày tốt lành.",
                    "Cảm ơn bạn đã quan tâm đến Linh Kiện PC!",
                    "Có gì cần hỗ trợ thêm, bạn đừng ngần ngại nhắn tin nhé."
                };
                return thankResponses[random.Next(thankResponses.Length)];
            }

            if (userMessage.Contains("tạm biệt") || userMessage.Contains("bye") || userMessage.Contains("goodbye"))
            {
                string[] goodbyeResponses = new[]
                {
                    "Tạm biệt! Chúc bạn một ngày tốt lành.",
                    "Hẹn gặp lại bạn! Cần hỗ trợ gì thêm cứ nhắn tin nhé.",
                    "Tạm biệt! Cảm ơn bạn đã ghé thăm Linh Kiện PC."
                };
                return goodbyeResponses[random.Next(goodbyeResponses.Length)];
            }

            // Phản hồi mặc định ngẫu nhiên
            string[] defaultResponses = new[]
            {
                "Cảm ơn bạn đã quan tâm! Hiện tại nhân viên đang bận, bạn có thể để lại tin nhắn và họ sẽ phản hồi sớm nhất.",
                "Dạ, Linh Kiện PC có nhiều sản phẩm phù hợp với nhu cầu của bạn. Bạn có thể xem thêm tại danh mục trên website.",
                "Hiện tại đang có chương trình giảm giá cho nhiều linh kiện gaming. Bạn quan tâm đến dòng nào?",
                "Bạn cần tư vấn thêm thông tin gì về sản phẩm không ạ?",
                "Nếu cần hỗ trợ thêm, bạn có thể chat với nhân viên tư vấn hoặc để lại số điện thoại."
            };

            return defaultResponses[random.Next(defaultResponses.Length)];
        }
    }

    public class SendMessageRequest { public string Content { get; set; } }
    public class StaffReplyRequest { public string UserId { get; set; } public string Content { get; set; } }
}