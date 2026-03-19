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

        // ================= USER =================

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
            if (!User.Identity.IsAuthenticated)
                return Json(new { success = false, requireLogin = true });

            if (string.IsNullOrWhiteSpace(req?.Content))
                return Json(new { success = false, message = "Nội dung không được trống." });

            var userId = _userManager.GetUserId(User);

            // lưu tin nhắn user
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

            // check staff online
            var hasStaffOnline = await _context.StaffStatuses.AnyAsync(s => s.IsOnline);

            if (hasStaffOnline)
            {
                return Json(new
                {
                    success = true,
                    waitingForStaff = true,
                    reply = "Nhân viên đang online, chờ xíu nha 😄",
                    isAI = false,
                    time = DateTime.Now.ToString("HH:mm")
                });
            }

            // AI trả lời
            var aiReply = await CallOpenRouterAI(req.Content);

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
                reply = aiReply,
                isAI = true,
                time = DateTime.Now.ToString("HH:mm")
            });
        }

        // ================= STAFF =================

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

            unread.ForEach(m => m.IsRead = true);
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

        // ================= AI =================

        private async Task<string> CallOpenRouterAI(string userMessage)
        {
            try
            {
                var apiKey = _config["OpenRouter:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return "Shop chưa bật AI nha 😢";

                // Lấy sản phẩm từ database
                var products = await _context.Products
                    .OrderByDescending(p => p.Price)
                    .Take(5)
                    .Select(p => $"- {p.Name}: {p.Price:N0}đ")
                    .ToListAsync();

                var productInfo = products.Any() ? string.Join("\n", products) : "- Chưa có sản phẩm";

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                client.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost:5000");
                client.DefaultRequestHeaders.Add("X-Title", "WebLinhKienPC");

                // Danh sách models ưu tiên
                string[] models = new[]
                {
                    "google/gemma-2b-it:free",
                    "microsoft/phi-3-mini-128k-instruct:free",
                    "meta-llama/llama-3-8b-instruct:free",
                    "mistralai/mistral-7b-instruct:free"
                };

                foreach (var model in models)
                {
                    try
                    {
                        Console.WriteLine($"Đang thử model: {model}");

                        var body = new
                        {
                            model = model,
                            messages = new[]
                            {
                                new {
                                    role = "system",
                                    content = $@"
Bạn là nhân viên tư vấn PC của shop Linh Kiện PC.

QUY TẮC:
- Trả lời NGẮN GỌN (1-2 câu)
- Tư vấn CỤ THỂ, không hỏi lại
- Dùng tiếng Việt, phong cách Gen Z
- Có thể dùng emoji 😄🔥
- KHÔNG nói mình là AI

SẢN PHẨM HIỆN CÓ:
{productInfo}

VÍ DỤ:
- Hỏi: 'RTX 3060 giá bao nhiêu?'
- Trả lời: 'RTX 3060 đang có giá từ 6.5 - 7.5tr tùy hãng nha bạn 🔥'
- Hỏi: 'Build PC 15tr'
- Trả lời: 'Bạn có thể tham khảo: i5-13400F + RTX 3060 + 16GB RAM, ok luôn 😄'
"
                                },
                                new { role = "user", content = userMessage }
                            },
                            temperature = 0.7,
                            max_tokens = 200
                        };

                        var json = JsonSerializer.Serialize(body);
                        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync("https://openrouter.ai/api/v1/chat/completions", httpContent);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();
                            var doc = JsonDocument.Parse(responseString);

                            var reply = doc.RootElement
                                .GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content")
                                .GetString();

                            if (!string.IsNullOrWhiteSpace(reply))
                            {
                                Console.WriteLine($"✅ Model {model} thành công");
                                return reply.Trim();
                            }
                        }
                        else
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"❌ Model {model} lỗi: {response.StatusCode} - {error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Model {model} exception: {ex.Message}");
                        continue;
                    }
                }

                // Nếu tất cả đều lỗi, dùng response mặc định
                return GetDefaultResponse(userMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Error: {ex.Message}");
                return GetDefaultResponse(userMessage);
            }
        }

        private string GetDefaultResponse(string userMessage)
        {
            userMessage = userMessage.ToLower();

            if (userMessage.Contains("xin chào") || userMessage.Contains("hello") || userMessage.Contains("hi") || userMessage.Contains("chào"))
                return "Chào bạn! Mình là nhân viên tư vấn shop Linh Kiện PC đây 😄 Bạn cần tìm linh kiện gì ạ?";

            if (userMessage.Contains("cảm ơn"))
                return "Không có gì đâu bạn ơi! Cần gì cứ hỏi nha 😄";

            if (userMessage.Contains("tạm biệt") || userMessage.Contains("bye"))
                return "Tạm biệt bạn! Nếu cần gì cứ quay lại shop nha 🔥";

            if (userMessage.Contains("giá") || userMessage.Contains("bao nhiêu"))
            {
                if (userMessage.Contains("rtx") || userMessage.Contains("card") || userMessage.Contains("vga"))
                    return "Card đồ họa đang có RTX 3060 (6.5tr), RTX 4060 (8.5tr), RTX 4070 (12tr) bạn nha! 😄";

                if (userMessage.Contains("cpu") || userMessage.Contains("i5") || userMessage.Contains("i7") || userMessage.Contains("ryzen"))
                    return "CPU Intel i5-13400F (5.2tr), i7-13700F (8.5tr), AMD Ryzen 5 7600 (5.8tr) bạn ơi! 🔥";

                if (userMessage.Contains("ram"))
                    return "RAM 16GB DDR4 (1.8tr), 32GB DDR5 (3.2tr) đang có sẵn nha bạn!";

                return "Bạn muốn xem giá linh kiện gì ạ? Mình có CPU, VGA, RAM, Mainboard đủ hết nè 😄";
            }

            if (userMessage.Contains("cấu hình") || userMessage.Contains("build") || userMessage.Contains("pc"))
                return "Bạn muốn build PC tầm giá nào ạ? Mình tư vấn cho! VD: 15tr, 20tr, 30tr... 🔥";

            return "Dạ, shop mình có đủ linh kiện PC bạn nha! Bạn muốn xem CPU, VGA hay RAM ạ? 😄";
        }
    }

    public class SendMessageRequest
    {
        public string Content { get; set; }
    }

    public class StaffReplyRequest
    {
        public string UserId { get; set; }
        public string Content { get; set; }
    }
}