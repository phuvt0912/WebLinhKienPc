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
                var welcomeMsg = "Chào bạn! Mình là Thắng từ PTH TECH 😄 Shop mình có đầy đủ linh kiện: CPU, VGA, RAM, SSD, Màn hình... Bạn đang cần tư vấn gì hoặc muốn build PC tầm giá nào không ạ?";

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

            // Phân loại sản phẩm theo budget
            string relevantText;
            if (intent.Budget.HasValue && relevantProducts.Any())
            {
                decimal budget = intent.Budget.Value;
                var cheaper = relevantProducts.Where(p => p.Price < budget * 0.85m).ToList();
                var inBudget = relevantProducts.Where(p => p.Price >= budget * 0.85m && p.Price <= budget * 1.15m).ToList();
                var pricier = relevantProducts.Where(p => p.Price > budget * 1.15m).ToList();

                var sb = new System.Text.StringBuilder();
                if (cheaper.Any())
                {
                    sb.AppendLine("【TIẾT KIỆM - rẻ hơn budget】");
                    foreach (var p in cheaper)
                    {
                        decimal saved = budget - p.Price;
                        sb.AppendLine($"- ID:{p.ProductId} | {p.Name} | {p.Price:N0}đ | Tiết kiệm: {saved:N0}đ | Còn: {p.Stock}");
                    }
                }
                if (inBudget.Any())
                {
                    sb.AppendLine("【SÁT GIÁ - trong tầm budget】");
                    foreach (var p in inBudget)
                        sb.AppendLine($"- ID:{p.ProductId} | {p.Name} | {p.Price:N0}đ | Còn: {p.Stock}");
                }
                if (pricier.Any())
                {
                    sb.AppendLine("【NÂNG CẤP - đắt hơn budget】");
                    foreach (var p in pricier)
                    {
                        decimal extra = p.Price - budget;
                        sb.AppendLine($"- ID:{p.ProductId} | {p.Name} | {p.Price:N0}đ | Cần thêm: {extra:N0}đ | Còn: {p.Stock}");
                    }
                }
                relevantText = sb.Length > 0 ? sb.ToString() : "KHÔNG CÓ sản phẩm phù hợp trong kho.";
            }
            else
            {
                relevantText = relevantProducts.Any()
                    ? string.Join("\n", relevantProducts.Select(p =>
                        $"- ID:{p.ProductId} | [{p.Category?.Name}] {p.Name} | {p.Price:N0}đ | Còn: {p.Stock}"))
                    : "KHÔNG CÓ sản phẩm phù hợp trong kho hiện tại.";
            }

            var budgetText = intent.Budget.HasValue
                ? $"{intent.Budget.Value:N0}đ (~{intent.Budget.Value / 1_000_000:N0} triệu)"
                : "Chưa rõ";

            return $@"
Bạn là **Thắng** - nhân viên tư vấn sale của shop linh kiện máy tính **PTH TECH**.
Phong cách: Thân thiện như bạn bè, nhiệt tình, am hiểu kỹ thuật.

━━━ THÔNG TIN SHOP ━━━
Danh mục: {categoriesText}

━━━ HÀNG TRONG KHO (tham khảo) ━━━
{featuredText}

━━━ SẢN PHẨM PHÂN LOẠI THEO BUDGET ━━━
{relevantText}

━━━ LỊCH SỬ HỘI THOẠI ━━━
{(string.IsNullOrEmpty(historyText) ? "(Tin nhắn đầu tiên)" : historyText)}

━━━ TIN NHẮN HIỆN TẠI ━━━
Khách: {userMessage}
Budget: {budgetText}
Loại linh kiện: {intent.Category ?? "Chưa xác định"}

━━━ CÁCH TƯ VẤN ━━━

1. KHI CÓ BUDGET + SẢN PHẨM PHÂN LOẠI:
   - Đề xuất theo thứ tự: SÁT GIÁ trước → TIẾT KIỆM → NÂNG CẤP
   - Sản phẩm TIẾT KIỆM: nói ""Nếu muốn tiết kiệm, [tên] giá [X]đ, bạn còn dư [Y]đ để mua thêm RAM/SSD""
   - Sản phẩm SÁT GIÁ: giới thiệu ngắn điểm mạnh chính
   - Sản phẩm NÂNG CẤP: nói ""Nếu ráng thêm [chênh lệch]đ nữa, [tên] xịn hơn hẳn vì [lý do ngắn]""
   - Luôn nêu số tiền chênh lệch CỤ THỂ

2. KHI KHÁCH HỎI SẢN PHẨM CỤ THỂ (có tên brand/series):
   - Chỉ giới thiệu sản phẩm CÓ LIÊN QUAN đến tên đó
   - Nếu không có trong kho: nói thẳng ""Shop mình hiện chưa có [tên] bạn ơi""
   - Gợi ý thay thế tương đương nếu có

3. KHI KHÁCH CHƯA RÕ NHU CẦU:
   - Hỏi: mục đích (gaming/làm việc/đồ họa) và budget

4. KHI BUILD PC:
   - Hỏi mục đích + budget nếu chưa rõ
   - Gợi ý combo từ danh sách

5. TUYỆT ĐỐI KHÔNG:
   - Bịa sản phẩm không có trong danh sách
   - Đưa ra sản phẩm không liên quan đến câu hỏi
   - Trả lời dang dở, cắt ngang giữa chừng — phải nói TRỌN VẸN ý
   - Dùng nhiều bullet point — viết tự nhiên như chat

6. PHONG CÁCH:
   - Xưng ""mình"", gọi ""bạn""
   - Emoji vừa phải 😄 🔥 ✅
   - Câu HOÀN CHỈNH, không bị đứt giữa chừng
   - Cuối tin có 1 câu hỏi ngắn để duy trì hội thoại
   - Tiếng Việt tự nhiên, tối đa 150 từ

Trả lời NGAY, vào thẳng vấn đề:
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

            // Lọc theo từ khóa model số — NHƯNG chỉ áp dụng nếu tìm thấy đủ sản phẩm
            var keywords = ExtractProductKeywords(userMessage);

            // Thêm: trích từ khóa tên thương hiệu/series từ message
            var nameKeywords = ExtractNameKeywords(userMessage);

            if (keywords.Any() || nameKeywords.Any())
            {
                var keywordQuery = query;
                foreach (var kw in keywords.Concat(nameKeywords))
                {
                    var kw2 = kw;
                    keywordQuery = keywordQuery.Where(p => p.Name.ToLower().Contains(kw2));
                }

                var keywordResults = await keywordQuery.OrderBy(p => p.Price).Take(6).ToListAsync();
                // Nếu tìm đủ sản phẩm liên quan → dùng, không thì bỏ filter keyword
                if (keywordResults.Count >= 2)
                    query = keywordQuery;
            }

            // Lọc theo budget — lấy 1 rẻ hơn + sát giá + đắt hơn
            if (intent.Budget.HasValue)
            {
                decimal budget = intent.Budget.Value;

                // Sản phẩm sát giá (80%-110%)
                var inBudget = await query
                    .Where(p => p.Price >= budget * 0.80m && p.Price <= budget * 1.10m)
                    .OrderBy(p => p.Price)
                    .Take(2)
                    .ToListAsync();

                // Sản phẩm rẻ hơn gần nhất (tiết kiệm)
                var cheaper = await query
                    .Where(p => p.Price < budget * 0.80m)
                    .OrderByDescending(p => p.Price)
                    .Take(1)
                    .ToListAsync();

                // Sản phẩm đắt hơn gần nhất (upsell)
                var pricier = await query
                    .Where(p => p.Price > budget * 1.10m)
                    .OrderBy(p => p.Price)
                    .Take(1)
                    .ToListAsync();

                var combined = cheaper.Concat(inBudget).Concat(pricier)
                    .DistinctBy(p => p.ProductId)
                    .OrderBy(p => p.Price)
                    .ToList();

                if (combined.Any()) return combined;
            }

            return await query
                .OrderByDescending(p => p.ProductId)
                .Take(4)
                .ToListAsync();
        }

        // Thêm hàm trích từ khóa tên thương hiệu/series
        private List<string> ExtractNameKeywords(string message)
        {
            var msg = message.ToLower();
            var keywords = new List<string>();

            // Các pattern tên thương hiệu/series phổ biến
            var patterns = new[]
            {
        @"\b(miku|hatsune)\b",
        @"\b(asus|msi|gigabyte|asrock|evga|zotac|sapphire|powercolor|xfx)\b",
        @"\b(corsair|kingston|crucial|gskill|teamgroup)\b",
        @"\b(samsung|seagate|western digital|wd|toshiba)\b",
        @"\b(noctua|be quiet|arctic|cooler master|deepcool)\b",
        @"\b(fractal|lian li|phanteks|nzxt)\b",
        @"\b(seasonic|evga|be quiet|corsair|antec)\b",
        @"\b(rog|tuf|prime|pro art|strix)\b",
        @"\b(gaming x|ventus|suprim|trio|eagle)\b",
    };

            foreach (var pattern in patterns)
            {
                var m = Regex.Match(msg, pattern, RegexOptions.IgnoreCase);
                if (m.Success)
                    keywords.Add(m.Value.Trim().ToLower());
            }

            // Lấy các từ có độ dài > 3 không phải stop word
            var stopWords = new HashSet<string> {
        "còn", "muốn", "mình", "bạn", "nào", "gì", "tôi",
        "có", "cho", "với", "được", "này", "đó", "một", "các",
        "và", "hay", "hoặc", "nhưng", "mà", "thì", "của", "về",
        "tư", "vấn", "liên", "quan", "đến", "sản", "phẩm"
    };

            var words = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !stopWords.Contains(w))
                .ToList();

            // Chỉ thêm từ khóa nếu không phải từ thông thường
            foreach (var word in words)
            {
                if (Regex.IsMatch(word, @"[a-z0-9]") && !keywords.Contains(word))
                    keywords.Add(word);
            }

            return keywords.Distinct().Take(3).ToList();
        }

        // ================= GỌI GEMINI =================

        private async Task<string> CallGemini(string prompt)
        {
            var apiKey = _config["Gemini:ApiKey"];
            var models = new[] { "gemini-2.0-flash", "gemini-2.5-flash-lite", "gemini-2.5-flash" };

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30); // tăng timeout

            foreach (var model in models)
            {
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                    var body = new
                    {
                        contents = new[] { new { parts = new[] { new { text = prompt } } } },
                        generationConfig = new
                        {
                            temperature = 0.7,
                            maxOutputTokens = 600,  // tăng từ 1024 — đủ cho 150 từ tiếng Việt
                            stopSequences = new string[] { }  // không stop sớm
                        }
                    };

                    var res = await client.PostAsync(url,
                        new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

                    Console.WriteLine($"Gemini [{model}]: {res.StatusCode}");

                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        var doc = JsonDocument.Parse(json);

                        var text = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();

                        // Kiểm tra finishReason — nếu MAX_TOKENS thì text bị cắt
                        var finishReason = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("finishReason")
                            .GetString();

                        if (finishReason == "MAX_TOKENS")
                            Console.WriteLine($"⚠️ Response bị cắt do MAX_TOKENS — tăng maxOutputTokens");

                        return text;
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