using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.Controllers
{
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

    public class UserIntent
    {
        public string Action { get; set; }
        public decimal? Budget { get; set; }
        public bool NeedProducts { get; set; }
        public string Category { get; set; }
        public List<string> Keywords { get; set; } = new();
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
                    Products = m.ProductsJson != null
                        ? JsonSerializer.Deserialize<List<ProductCard>>(m.ProductsJson)
                        : null,
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

            // ===== TIN NHẮN CHÀO MỪng TỰ ĐỘNG =====
            if (req.Content == "__welcome__")
            {
                var welcomeMsg = "Chào bạn! Mình là Thắng từ LinhKienPC 😄 Shop mình có đầy đủ linh kiện: CPU, VGA, RAM, SSD, Màn hình... Bạn đang cần tư vấn gì hoặc muốn build PC tầm giá nào không ạ?";

                // Lưu tin chào vào DB luôn để lịch sử không bị reset
                _context.ChatMessages.Add(new ChatMessage
                {
                    UserId = userId,
                    Content = welcomeMsg,
                    IsFromUser = false,
                    IsFromAI = true,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    reply = welcomeMsg,
                    isAI = true,
                    time = DateTime.Now.ToString("HH:mm")
                });
            }

            // ===== TIN NHẮN THƯỜNG =====
            _context.ChatMessages.Add(new ChatMessage
            {
                UserId = userId,
                Content = req.Content.Trim(),
                IsFromUser = true,
                IsFromAI = false,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            // Check nhân viên online
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
            var aiResponse = await CallGeminiAIWithProducts(req.Content, userId);

            string productsJson = aiResponse.Products?.Any() == true
                ? JsonSerializer.Serialize(aiResponse.Products)
                : null;

            _context.ChatMessages.Add(new ChatMessage
            {
                UserId = userId,
                Content = aiResponse.Message,
                ProductsJson = productsJson,
                IsFromUser = false,
                IsFromAI = true,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                reply = aiResponse.Message,
                products = aiResponse.Products,
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
                _context.StaffStatuses.Add(new StaffStatus { StaffId = staffId, IsOnline = isOnline, UpdatedAt = DateTime.Now });
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
                    Products = m.ProductsJson != null
                        ? JsonSerializer.Deserialize<List<ProductCard>>(m.ProductsJson)
                        : null,
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
            _context.ChatMessages.Add(new ChatMessage
            {
                UserId = req.UserId,
                StaffId = staffId,
                Content = req.Content.Trim(),
                IsFromUser = false,
                IsFromAI = false,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return Json(new { success = true, time = DateTime.Now.ToString("HH:mm") });
        }

        [Authorize(Roles = "Admin,NhanVien")]
        public IActionResult StaffChat() => View();

        // ================= AI ENGINE =================

        private async Task<AIResponse> CallGeminiAIWithProducts(string userMessage, string userId)
        {
            try
            {
                var apiKey = _config["Gemini:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return new AIResponse { Message = "AI chưa được cấu hình. Vui lòng chờ nhân viên ạ!", Products = new() };

                // Phân tích intent
                var intent = AnalyzeUserIntent(userMessage);

                // Lấy lịch sử chat để AI hiểu ngữ cảnh
                var history = await _context.ChatMessages
                    .Where(m => m.UserId == userId)
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(6)
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new { m.Content, m.IsFromUser })
                    .ToListAsync();

                // Lấy sản phẩm từ DB
                List<Product> products = new();
                if (intent.NeedProducts)
                    products = await FindRelevantProducts(userMessage, intent);

                // Lấy TẤT CẢ danh mục và 1 số sản phẩm để AI biết shop có gì
                var allCategories = await _context.Categories.Select(c => c.Name).ToListAsync();
                var featuredProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.Stock > 0)
                    .OrderByDescending(p => p.ProductId)
                    .Take(10)
                    .ToListAsync();

                // Tạo product cards
                var productCards = products.Select(p => new ProductCard
                {
                    Id = p.ProductId,
                    Name = p.Name.Length > 60 ? p.Name[..57] + "..." : p.Name,
                    Price = p.Price.ToString("N0") + "đ",
                    ImageUrl = p.ImageUrl ?? "",
                    Stock = p.Stock,
                    Category = p.Category?.Name ?? "",
                    Url = $"/Product/Details/{p.ProductId}"
                }).ToList();

                // Build prompt
                var prompt = BuildPrompt(userMessage, intent, products, allCategories, featuredProducts, history.Select(h => (h.Content, h.IsFromUser)).ToList());

                // Gọi Gemini
                var message = await CallGemini(prompt);

                return new AIResponse
                {
                    Message = message ?? GetFallbackResponse(intent),
                    Products = productCards
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Error: {ex.Message}");
                return new AIResponse
                {
                    Message = "AI đang bận xíu, bạn thử lại hoặc chờ nhân viên hỗ trợ nha! 😊",
                    Products = new()
                };
            }
        }

        private string BuildPrompt(
    string userMessage,
    UserIntent intent,
    List<Product> relevantProducts,
    List<string> allCategories,
    List<Product> featuredProducts,
    List<(string Content, bool IsFromUser)> history)
        {
            var historyText = history.Count > 0
                ? string.Join("\n", history.Select(h => $"{(h.IsFromUser ? "Khách" : "Bạn")}: {h.Content}"))
                : "";

            var categoriesText = string.Join(", ", allCategories);

            var featuredText = string.Join("\n", featuredProducts.Select(p =>
                $"- [{p.Category?.Name}] {p.Name} | {p.Price:N0}đ | Còn: {p.Stock}"));

            var relevantText = relevantProducts.Any()
                ? string.Join("\n", relevantProducts.Select(p =>
                    $"- ID:{p.ProductId} | [{p.Category?.Name}] {p.Name} | {p.Price:N0}đ | Còn: {p.Stock}"))
                : "KHÔNG CÓ sản phẩm phù hợp trong kho hiện tại.";

            var budgetText = intent.Budget.HasValue
                ? $"{intent.Budget.Value:N0}đ (~{intent.Budget.Value / 1_000_000:N0} triệu)"
                : "Chưa rõ";

            return $@"
Bạn là **Thắng** - nhân viên tư vấn sale của shop linh kiện máy tính **LinhKienPC**.
Phong cách: Thân thiện như bạn bè, nhiệt tình, am hiểu kỹ thuật, biết cách thuyết phục khách mua hàng.

━━━ THÔNG TIN SHOP ━━━
Danh mục: {categoriesText}

━━━ HÀNG TRONG KHO (tham khảo) ━━━
{featuredText}

━━━ SẢN PHẨM GẦN NHẤT VỚI YÊU CẦU KHÁCH ━━━
{relevantText}

━━━ LỊCH SỬ HỘI THOẠI ━━━
{(string.IsNullOrEmpty(historyText) ? "(Tin nhắn đầu tiên)" : historyText)}

━━━ TIN NHẮN HIỆN TẠI ━━━
Khách: {userMessage}
Budget: {budgetText}
Loại linh kiện: {intent.Category ?? "Chưa xác định"}

━━━ HƯỚNG DẪN TRẢ LỜI ━━━

1. KHI KHÁCH CÓ BUDGET:
   - CHỈ giới thiệu sản phẩm từ danh sách SẢN PHẨM GẦN NHẤT VỚI YÊU CẦU
   - Nếu sản phẩm RẺ HƠN budget: nói rõ ""Với [budget] bạn dư thêm [X], có thể mua thêm phụ kiện hoặc nâng cấp RAM/SSD""
   - Nếu sản phẩm ĐẮT HƠN budget: nói rõ chênh lệch cụ thể ""Con này [giá], chỉ cần thêm [chênh lệch] nữa là lấy được, xịn hơn hẳn đó bạn""
   - Nếu không có sản phẩm phù hợp: thành thật nói và gợi ý tăng/giảm budget bao nhiêu để có đồ

2. KHI KHÁCH CHƯA RÕ NHU CẦU:
   - Hỏi mục đích: gaming, làm việc, đồ họa
   - Hỏi budget
   - Hỏi 1 câu để thu hẹp thêm

3. KHI BUILD PC:
   - Hỏi mục đích và budget nếu chưa rõ
   - Gợi ý combo từ danh sách, giải thích ngắn gọn tại sao

4. KHI SO SÁNH / DO DỰ:
   - Phân tích ngắn ưu/nhược
   - Đưa ra lời khuyên rõ ràng: ""Theo mình bạn nên chọn A vì...""
   - Tạo urgency nếu hàng ít: ""Con này chỉ còn [X] bộ thôi đó""

5. TUYỆT ĐỐI KHÔNG:
   - Bịa sản phẩm không có trong danh sách
   - Nói sai giá
   - Show sản phẩm lệch quá xa so với budget (>20%)
   - Trả lời quá dài (tối đa 100 từ)
   - Bullet point nhiều quá - nói tự nhiên như chat

6. PHONG CÁCH:
   - Xưng ""mình"", gọi ""bạn""
   - Emoji vừa phải 😄 🔥 ✅
   - Cuối tin luôn có 1 câu hỏi để duy trì hội thoại
   - Tiếng Việt tự nhiên, thân thiện

Trả lời NGAY, không nhắc lại câu hỏi, vào thẳng vấn đề:
";
        }

        // ================= PHÂN TÍCH INTENT =================

        private UserIntent AnalyzeUserIntent(string message)
        {
            var msg = message.ToLower().Trim();
            var intent = new UserIntent { Action = "chat", NeedProducts = false };

            // Chào hỏi - nhưng vẫn có thể kèm câu hỏi
            if (Regex.IsMatch(msg, @"^(xin chào|hello|hi|chào|helo|chao|hey)\s*[!.]*$"))
            {
                intent.Action = "greeting";
                return intent;
            }

            // Cảm ơn
            if (Regex.IsMatch(msg, @"\b(cảm ơn|thank|thanks|cam on|tks)\b") && msg.Length < 30)
            {
                intent.Action = "thank";
                return intent;
            }

            // Tạm biệt
            if (Regex.IsMatch(msg, @"\b(tạm biệt|bye|goodbye|tam biet)\b") && msg.Length < 20)
            {
                intent.Action = "goodbye";
                return intent;
            }

            // Budget
            intent.Budget = ExtractBudget(msg);

            // So sánh
            if (Regex.IsMatch(msg, @"\b(so sánh|khác nhau|tốt hơn|nên chọn|hay là|hoặc|vs)\b"))
            {
                intent.Action = "compare";
                intent.NeedProducts = true;
            }

            // Category map - giữ nguyên như cũ
            var categoryMap = new Dictionary<string, string>
            {
                ["vga"] = "VGA",
                ["card màn hình"] = "VGA",
                ["card đồ họa"] = "VGA",
                ["card do hoa"] = "VGA",
                ["rtx"] = "VGA",
                ["gtx"] = "VGA",
                ["rx "] = "VGA",
                ["radeon"] = "VGA",
                ["nvidia"] = "VGA",
                ["cpu"] = "CPU",
                ["vi xử lý"] = "CPU",
                ["bộ xử lý"] = "CPU",
                ["intel"] = "CPU",
                ["amd"] = "CPU",
                ["ryzen"] = "CPU",
                ["core i"] = "CPU",
                ["chip"] = "CPU",
                ["ram"] = "RAM",
                ["bộ nhớ"] = "RAM",
                ["ddr"] = "RAM",
                ["main"] = "Mainboard",
                ["mainboard"] = "Mainboard",
                ["bo mạch"] = "Mainboard",
                ["motherboard"] = "Mainboard",
                ["ssd"] = "SSD",
                ["ổ cứng ssd"] = "SSD",
                ["nvme"] = "SSD",
                ["hdd"] = "HDD",
                ["ổ cứng hdd"] = "HDD",
                ["ổ cứng"] = "SSD",
                ["nguồn"] = "PSU",
                ["psu"] = "PSU",
                ["power"] = "PSU",
                ["case"] = "Case",
                ["vỏ case"] = "Case",
                ["thùng máy"] = "Case",
                ["tản nhiệt"] = "Tản nhiệt",
                ["tan nhiet"] = "Tản nhiệt",
                ["cooler"] = "Tản nhiệt",
                ["aio"] = "Tản nhiệt",
                ["màn hình"] = "Màn hình",
                ["monitor"] = "Màn hình",
                ["bàn phím"] = "Bàn phím",
                ["keyboard"] = "Bàn phím",
                ["chuột"] = "Chuột",
                ["mouse"] = "Chuột",
                ["tai nghe"] = "Tai nghe",
                ["headset"] = "Tai nghe",
                ["build"] = "PC",
                ["cấu hình"] = "PC",
                ["bộ máy"] = "PC",
                ["ráp máy"] = "PC",
                ["máy tính"] = "PC",
                ["dàn máy"] = "PC"
            };

            foreach (var kv in categoryMap)
            {
                if (msg.Contains(kv.Key))
                {
                    intent.Category = kv.Value;
                    break;
                }
            }

            // Xác định action và NeedProducts
            bool isProductQuery = Regex.IsMatch(msg,
                @"(giá|bao nhiêu|còn hàng|mua|bán|có không|sản phẩm|linh kiện|tư vấn|gợi ý|nên mua|chọn|loại nào|nâng cấp|upgrade|combo)");

            if (intent.Category != null || isProductQuery || intent.Budget.HasValue)
            {
                if (intent.Action == "chat") // không ghi đè "compare"
                    intent.Action = intent.Category == "PC" ? "build_pc" : "product_inquiry";
                intent.NeedProducts = true;
            }

            return intent;
        }

        // ================= TÌM SẢN PHẨM =================

        private async Task<List<Product>> FindRelevantProducts(string userMessage, UserIntent intent)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.Stock > 0)
                .AsQueryable();

            // Lọc theo category
            if (!string.IsNullOrEmpty(intent.Category) && intent.Category != "PC")
            {
                query = query.Where(p =>
                    p.Category.Name.ToLower().Contains(intent.Category.ToLower()) ||
                    p.Name.ToLower().Contains(intent.Category.ToLower()));
            }

            // Lọc theo từ khóa model số
            var keywords = ExtractProductKeywords(userMessage);
            if (keywords.Any())
            {
                foreach (var kw in keywords)
                {
                    var kw2 = kw;
                    query = query.Where(p => p.Name.ToLower().Contains(kw2));
                }
            }

            // Lọc theo budget - CHỈ LẤY SẢN PHẨM GẦN VỚI BUDGET
            if (intent.Budget.HasValue)
            {
                decimal budget = intent.Budget.Value;

                // Lấy sản phẩm trong 80%-110% budget
                decimal min = budget * 0.80m;
                decimal max = budget * 1.10m;

                var inBudget = await query
                    .Where(p => p.Price >= min && p.Price <= max)
                    .OrderBy(p => p.Price)
                    .Take(3)
                    .ToListAsync();

                if (inBudget.Any()) return inBudget;

                // Không có → lấy 1 rẻ hơn gần nhất + 2 đắt hơn gần nhất
                var cheaper = await query
                    .Where(p => p.Price < budget)
                    .OrderByDescending(p => p.Price)
                    .Take(1)
                    .ToListAsync();

                var pricier = await query
                    .Where(p => p.Price > budget)
                    .OrderBy(p => p.Price)
                    .Take(2)
                    .ToListAsync();

                return cheaper.Concat(pricier).ToList();
            }

            return await query
                .OrderByDescending(p => p.ProductId)
                .Take(4)
                .ToListAsync();
        }

        // ================= GỌI GEMINI =================

        private async Task<string> CallGemini(string prompt)
        {
            var apiKey = _config["Gemini:ApiKey"];
            var models = new[] { "gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-2.0-flash" };

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            foreach (var model in models)
            {
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                    var body = new
                    {
                        contents = new[] { new { parts = new[] { new { text = prompt } } } },
                        generationConfig = new { temperature = 0.7, maxOutputTokens = 1024 }
                    };

                    var res = await client.PostAsync(url,
                        new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

                    Console.WriteLine($"Gemini [{model}]: {res.StatusCode}");

                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        var doc = JsonDocument.Parse(json);
                        return doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();
                    }

                    var err = await res.Content.ReadAsStringAsync();
                    Console.WriteLine($"Gemini error: {err}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Gemini [{model}] exception: {ex.Message}");
                }
            }

            return null;
        }

        // ================= TIỆN ÍCH =================

        private decimal? ExtractBudget(string message)
        {
            // 15tr, 20 triệu, 15.5tr
            var m = Regex.Match(message, @"(\d+(?:[.,]\d+)?)\s*(tr|triệu|trieu|m)");
            if (m.Success && decimal.TryParse(
                m.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal val))
            {
                return val * 1_000_000;
            }

            // 15000000
            m = Regex.Match(message, @"(\d{7,9})");
            if (m.Success && decimal.TryParse(m.Groups[1].Value, out decimal raw))
                return raw;

            return null;
        }

        private List<string> ExtractProductKeywords(string message)
        {
            // Trích model number: RTX 4070, RX 7600, i5-14600K, B650, DDR5...
            var patterns = new[]
            {
                @"\b(rtx|gtx|rx)\s*\d{3,4}\b",
                @"\bi[3579]-\d{4,5}\w*\b",
                @"\bryzen\s*[3579]\s*\d{4}\w*\b",
                @"\b(b|x|z)\d{3}\b",
                @"\bddr[45]\b",
                @"\b\d{3,4}[gm]\b"
            };

            var keywords = new List<string>();
            foreach (var p in patterns)
            {
                var m = Regex.Match(message.ToLower(), p, RegexOptions.IgnoreCase);
                if (m.Success)
                    keywords.Add(m.Value.Trim().ToLower());
            }

            return keywords;
        }

        private string GetFallbackResponse(UserIntent intent) => intent.Action switch
        {
            "greeting" => "Chào bạn! Mình là Thắng từ LinhKienPC 😄 Bạn cần tư vấn linh kiện hay build PC không ạ?",
            "thank" => "Không có gì bạn ơi! Cần gì cứ hỏi mình nha 😄",
            "goodbye" => "Tạm biệt bạn! Ghé shop lần sau nha 🔥 Nếu cần tư vấn thêm cứ nhắn mình!",
            "build_pc" => "Bạn muốn build PC để làm gì ạ? Gaming, làm việc hay đồ họa? Và budget tầm bao nhiêu để mình tư vấn combo phù hợp nha! 😄",
            "compare" => "Bạn đang so sánh sản phẩm nào ạ? Cho mình biết thêm để tư vấn chính xác hơn 😄",
            "product_inquiry" => "Đây là các sản phẩm phù hợp nè bạn! Bạn muốn mình tư vấn thêm về sản phẩm nào không? 😄",
            _ => "Shop LinhKienPC có đầy đủ linh kiện: CPU, VGA, RAM, SSD, Màn hình... Bạn cần tư vấn gì ạ? 😄"
        };
    }

    public class SendMessageRequest { public string Content { get; set; } }
    public class StaffReplyRequest { public string UserId { get; set; } public string Content { get; set; } }
}