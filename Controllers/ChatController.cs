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
    // Thêm các class mới ở đầu file
    public class AIResponse
    {
        public string Message { get; set; }
        public List<ProductCard> Products { get; set; }
        public bool HasProducts => Products != null && Products.Any();
    }

    public class ProductCard
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Price { get; set; }
        public string ImageUrl { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; }
        public string Url { get; set; }
    }

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

            // AI trả lời KÈM SẢN PHẨM
            var aiResponse = await CallGeminiAIWithProducts(req.Content);

            var aiMsg = new ChatMessage
            {
                UserId = userId,
                Content = aiResponse.Message,
                IsFromUser = false,
                IsFromAI = true,
                CreatedAt = DateTime.Now
            };

            _context.ChatMessages.Add(aiMsg);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                reply = aiResponse.Message,
                products = aiResponse.Products, // Gửi kèm sản phẩm
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
            var status = await _context.StaffStatuses.FirstOrDefaultAsync(s => s.StaffId == staffId);

            if (status == null)
            {
                _context.StaffStatuses.Add(new StaffStatus { StaffId = staffId, IsOnline = isOnline, UpdatedAt = DateTime.Now });
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
            var status = await _context.StaffStatuses.FirstOrDefaultAsync(s => s.StaffId == staffId);
            return Json(new { isOnline = status?.IsOnline ?? false });
        }

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

        // ================= AI GEMINI - GỬI CARD SẢN PHẨM =================
        private async Task<AIResponse> CallGeminiAIWithProducts(string userMessage)
        {
            try
            {
                var apiKey = _config["Gemini:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return new AIResponse
                    {
                        Message = "Shop chưa bật AI nha 😢",
                        Products = new List<ProductCard>()
                    };

                // BƯỚC 1: TÌM SẢN PHẨM LIÊN QUAN TỪ DATABASE
                var relevantProducts = await FindRelevantProducts(userMessage);

                // Tạo product cards
                var productCards = relevantProducts.Select(p => new ProductCard
                {
                    Id = p.ProductId,
                    Name = p.Name.Length > 50 ? p.Name.Substring(0, 47) + "..." : p.Name,
                    Price = p.Price.ToString("N0") + "đ",
                    ImageUrl = p.ImageUrl ?? "/images/products/default.jpg",
                    Stock = p.Stock,
                    Category = p.Category?.Name ?? "Linh kiện",
                    Url = $"/Product/Details/{p.ProductId}"
                }).ToList();

                // BƯỚC 2: TẠO PROMPT VỚI THÔNG TIN SẢN PHẨM
                var productInfo = string.Join("\n", relevantProducts.Select(p =>
                    $"- {p.Name}: {p.Price:N0}đ (Còn {p.Stock} cái)"));

                var prompt = $@"
                                Bạn là nhân viên tư vấn PC của shop Linh Kiện PC.

                                SẢN PHẨM PHÙ HỢP:
                                {productInfo}

                                KHÁCH HỎI: {userMessage}

                                YÊU CẦU TRẢ LỜI:
                                - 2-3 câu ngắn gọn
                                - Giới thiệu sản phẩm phù hợp
                                - KHÔNG liệt kê giá (vì đã có card sản phẩm)
                                - Dùng emoji 😄🔥
                                - Kết thúc bằng câu hỏi để tương tác tiếp

                                VÍ DỤ TỐT:
                                'Bạn ơi, shop có mấy em này hợp nhu cầu của bạn nè 😄 Bạn xem qua rồi cho mình biết thích em nào không ạ?'

                                'Đây là các card màn hình tầm giá bạn cần nè 🔥 Còn hàng hết, bạn muốn tư vấn thêm về em nào không?'
                                ";

                // Gọi Gemini
                var geminiMessage = await CallGeminiWithPrompt(prompt);

                return new AIResponse
                {
                    Message = geminiMessage ?? GetSmartDefaultResponse(userMessage),
                    Products = productCards
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Error: {ex.Message}");
                return new AIResponse
                {
                    Message = GetSmartDefaultResponse(userMessage),
                    Products = new List<ProductCard>()
                };
            }
        }

        // Giữ lại hàm CallGeminiAI cũ để tương thích
        private async Task<string> CallGeminiAI(string userMessage)
        {
            var response = await CallGeminiAIWithProducts(userMessage);
            return response.Message;
        }

        // ===== TÌM SẢN PHẨM LIÊN QUAN (CẢI TIẾN) =====
        private async Task<List<Product>> FindRelevantProducts(string userMessage)
        {
            userMessage = userMessage.ToLower();
            var keywords = ExtractKeywords(userMessage);

            // Xác định category từ keywords
            var categoryMap = new Dictionary<string, string>
            {
                ["vga"] = "VGA",
                ["card"] = "VGA",
                ["đồ họa"] = "VGA",
                ["cpu"] = "CPU",
                ["vi xử lý"] = "CPU",
                ["chip"] = "CPU",
                ["ram"] = "RAM",
                ["bộ nhớ"] = "RAM",
                ["main"] = "Mainboard",
                ["bo mạch"] = "Mainboard",
                ["ssd"] = "SSD",
                ["hdd"] = "HDD",
                ["ổ cứng"] = "SSD",
                ["nguồn"] = "PSU",
                ["psu"] = "PSU",
                ["case"] = "Case",
                ["vỏ"] = "Case"
            };

            string targetCategory = null;
            foreach (var kw in keywords)
            {
                if (categoryMap.ContainsKey(kw))
                {
                    targetCategory = categoryMap[kw];
                    break;
                }
            }

            // Query sản phẩm
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.Stock > 0) // Chỉ lấy còn hàng
                .AsQueryable();

            // Lọc theo category nếu có
            if (!string.IsNullOrEmpty(targetCategory))
            {
                query = query.Where(p => p.Category.Name == targetCategory);
            }
            else
            {
                // Tìm theo tên sản phẩm
                query = query.Where(p => keywords.Any(k => p.Name.ToLower().Contains(k)));
            }

            // Lấy tối đa 6 sản phẩm
            var products = await query
                .OrderBy(p => p.Price)
                .Take(6)
                .ToListAsync();

            // Nếu không tìm thấy, trả về sản phẩm nổi bật
            if (!products.Any())
            {
                products = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.Stock > 0)
                    .OrderByDescending(p => p.Price)
                    .Take(4)
                    .ToListAsync();
            }

            return products;
        }

        // ===== CÁC HÀM TIỆN ÍCH (giữ nguyên từ code cũ) =====
        private List<string> ExtractKeywords(string message)
        {
            var stopWords = new[] { "cho", "tôi", "mình", "xin", "giá", "bao", "nhiêu", "có", "không",
                                     "muốn", "mua", "tư", "vấn", "giúp", "với", "ạ", "à", "nha", "nhe" };

            return message.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Select(w => w.ToLower())
                .Distinct()
                .ToList();
        }

        private (decimal Min, decimal Max)? ExtractPriceRange(string message)
        {
            if (message.Contains("dưới") || message.Contains("dưới"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, @"(\d+(\.?\d*))?\s*(tr|triệu)");
                if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal max))
                {
                    return (0, max * 1000000);
                }
            }
            else if (message.Contains("từ") && message.Contains("-"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)\s*-\s*(\d+)\s*(tr|triệu)");
                if (match.Success &&
                    decimal.TryParse(match.Groups[1].Value, out decimal min) &&
                    decimal.TryParse(match.Groups[2].Value, out decimal max))
                {
                    return (min * 1000000, max * 1000000);
                }
            }
            return null;
        }

        private async Task<string> CallGeminiWithPrompt(string prompt)
        {
            var apiKey = _config["Gemini:ApiKey"];
            string[] models = { "gemini-2.5-flash-lite", "gemini-2.5-flash", "gemini-2.0-flash" };

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            foreach (var model in models)
            {
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                    var body = new
                    {
                        contents = new[] { new { parts = new[] { new { text = prompt } } } },
                        generationConfig = new { temperature = 0.7, maxOutputTokens = 500 }
                    };

                    var response = await client.PostAsync(url,
                        new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var doc = JsonDocument.Parse(json);
                        return doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();
                    }
                }
                catch { continue; }
            }
            return null;
        }

        private string GetSmartDefaultResponse(string userMessage)
        {
            userMessage = userMessage.ToLower();

            if (userMessage.Contains("xin chào") || userMessage.Contains("hello"))
                return "Chào bạn! Mình là AI tư vấn của Linh Kiện PC. Bạn cần tìm linh kiện gì hay muốn build PC tầm giá nào ạ? 😄";

            if (userMessage.Contains("cảm ơn"))
                return "Không có gì đâu ạ! Cần tư vấn thêm gì bạn cứ hỏi nha 😄";

            if (userMessage.Contains("giá") || userMessage.Contains("bao nhiêu"))
            {
                if (userMessage.Contains("rtx") || userMessage.Contains("card"))
                    return "Card đồ họa đang có: RTX 3060 (6.5tr), RTX 4060 (8.5tr), RTX 4070 (12tr). Bạn muốn mình tư vấn thêm không? 😄";
                if (userMessage.Contains("cpu") || userMessage.Contains("i5") || userMessage.Contains("i7"))
                    return "CPU Intel i5-13400F (5.2tr), i7-13700F (8.5tr), AMD Ryzen 5 7600 (5.8tr). Bạn build PC tầm nào để mình gợi ý combo ạ? 🔥";
            }

            if (userMessage.Contains("build") || userMessage.Contains("cấu hình") || userMessage.Contains("pc"))
            {
                if (userMessage.Contains("15") || userMessage.Contains("15tr"))
                    return "Bạn muốn build PC 15tr? Mình gợi ý: i5-13400F + RTX 3060 + 16GB RAM. Có mấy em này trong shop nè bạn xem qua ạ! 😄";
                if (userMessage.Contains("20") || userMessage.Contains("20tr"))
                    return "PC 20tr thì ngon rồi: i7-13700F + RTX 4060 + 32GB RAM. Đây là các linh kiện phù hợp nè bạn! 🔥";
                if (userMessage.Contains("25") || userMessage.Contains("25tr"))
                    return "Build 25tr mượt game 4K luôn: i7-13700K + RTX 4070 + 32GB DDR5. Xem qua các em này nha! 💯";
            }

            return "Shop mình có đủ linh kiện PC: CPU, VGA, RAM, Mainboard, Ổ cứng... Bạn cần tư vấn sản phẩm gì hay build PC tầm giá nào để mình hỗ trợ chi tiết nha! 😄";
        }
    }

    public class SendMessageRequest { public string Content { get; set; } }
    public class StaffReplyRequest { public string UserId { get; set; } public string Content { get; set; } }
}