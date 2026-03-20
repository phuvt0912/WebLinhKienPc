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

    // Thêm class Intent để phân tích
    public class UserIntent
    {
        public string Action { get; set; } // greeting, thank, goodbye, product_inquiry, build_pc, check_stock, chat
        public decimal? Budget { get; set; }
        public bool NeedProducts { get; set; }
        public string Category { get; set; }
        public string ProductName { get; set; }
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

            // AI trả lời KÈM SẢN PHẨM (thông minh hơn)
            var aiResponse = await CallGeminiAIWithProducts(req.Content);

            // Lưu products dưới dạng JSON (nếu có)
            string productsJson = null;
            if (aiResponse.Products != null && aiResponse.Products.Any())
            {
                productsJson = JsonSerializer.Serialize(aiResponse.Products);
            }

            var aiMsg = new ChatMessage
            {
                UserId = userId,
                Content = aiResponse.Message,
                ProductsJson = productsJson,
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

        // ================= AI GEMINI - THÔNG MINH HƠN =================
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

                // Phân tích intent của user
                var intent = AnalyzeUserIntent(userMessage);

                // CHỈ tìm sản phẩm khi user hỏi về sản phẩm
                List<Product> relevantProducts = new List<Product>();

                if (intent.NeedProducts)
                {
                    relevantProducts = await FindRelevantProducts(userMessage, intent);
                }

                // Tạo product cards nếu có
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

                // Tạo prompt thông minh dựa trên intent
                var prompt = GenerateSmartPrompt(userMessage, intent, relevantProducts);

                // Gọi Gemini
                var geminiMessage = await CallGeminiWithPrompt(prompt);

                return new AIResponse
                {
                    Message = geminiMessage ?? GetSmartDefaultResponse(userMessage, intent),
                    Products = productCards
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Error: {ex.Message}");
                return new AIResponse
                {
                    Message = "Xin lỗi, AI đang bận. Bạn có thể chat trực tiếp với nhân viên ạ! 😊",
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

        // ===== PHÂN TÍCH INTENT =====
        private UserIntent AnalyzeUserIntent(string message)
        {
            message = message.ToLower().Trim();
            var intent = new UserIntent
            {
                Action = "chat",
                NeedProducts = false
            };

            // 1. Kiểm tra chào hỏi
            if (message.Contains("xin chào") || message.Contains("hello") || message.Contains("hi") ||
                message.Contains("chào") || message.Contains("helo") || message.Contains("chao"))
            {
                intent.Action = "greeting";
                intent.NeedProducts = false;
                return intent;
            }

            // 2. Kiểm tra cảm ơn
            if (message.Contains("cảm ơn") || message.Contains("thank") || message.Contains("thanks") ||
                message.Contains("cam on"))
            {
                intent.Action = "thank";
                intent.NeedProducts = false;
                return intent;
            }

            // 3. Kiểm tra tạm biệt
            if (message.Contains("tạm biệt") || message.Contains("bye") || message.Contains("goodbye") ||
                message.Contains("tam biet"))
            {
                intent.Action = "goodbye";
                intent.NeedProducts = false;
                return intent;
            }

            // 4. Kiểm tra hỏi về build PC
            if (message.Contains("build") || message.Contains("cấu hình") || message.Contains(" ráp") ||
                message.Contains("cau hinh") || message.Contains("bo may") || message.Contains("bộ máy"))
            {
                intent.Action = "build_pc";
                intent.NeedProducts = true;
                intent.Budget = ExtractBudget(message);

                // Xác định category là PC
                intent.Category = "PC";

                return intent;
            }

            // 5. Kiểm tra hỏi về sản phẩm cụ thể
            if (message.Contains("giá") || message.Contains("bao nhiêu") || message.Contains("còn hàng") ||
                message.Contains("mua") || message.Contains("bán") || message.Contains("có") ||
                message.Contains("sản phẩm") || message.Contains("linh kiện"))
            {
                intent.Action = "product_inquiry";
                intent.NeedProducts = true;

                // Xác định category từ keywords
                var categoryMap = new Dictionary<string, string>
                {
                    ["vga"] = "VGA",
                    ["card"] = "VGA",
                    ["đồ họa"] = "VGA",
                    ["do hoa"] = "VGA",
                    ["cpu"] = "CPU",
                    ["vi xử lý"] = "CPU",
                    ["vi xu ly"] = "CPU",
                    ["chip"] = "CPU",
                    ["ram"] = "RAM",
                    ["bộ nhớ"] = "RAM",
                    ["bo nho"] = "RAM",
                    ["main"] = "Mainboard",
                    ["bo mạch"] = "Mainboard",
                    ["bo mach"] = "Mainboard",
                    ["ssd"] = "SSD",
                    ["hdd"] = "HDD",
                    ["ổ cứng"] = "SSD",
                    ["o cung"] = "SSD",
                    ["nguồn"] = "PSU",
                    ["nguon"] = "PSU",
                    ["psu"] = "PSU",
                    ["case"] = "Case",
                    ["vỏ"] = "Case",
                    ["vo"] = "Case"
                };

                foreach (var kv in categoryMap)
                {
                    if (message.Contains(kv.Key))
                    {
                        intent.Category = kv.Value;
                        break;
                    }
                }

                // Trích xuất budget
                intent.Budget = ExtractBudget(message);

                return intent;
            }

            // 6. Kiểm tra hỏi về tình trạng kho
            if (message.Contains("còn") || message.Contains("hết") || message.Contains("con") ||
                message.Contains("het") || message.Contains("stock"))
            {
                intent.Action = "check_stock";
                intent.NeedProducts = true;
                return intent;
            }

            return intent;
        }

        // ===== TÌM SẢN PHẨM LIÊN QUAN THÔNG MINH =====
        private async Task<List<Product>> FindRelevantProducts(string userMessage, UserIntent intent)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.Stock > 0) // Chỉ lấy còn hàng
                .AsQueryable();

            // Lọc theo category nếu có
            if (!string.IsNullOrEmpty(intent.Category))
            {
                if (intent.Category == "PC")
                {
                    // Nếu là PC, tìm theo tên có chứa "PC"
                    query = query.Where(p => p.Name.Contains("PC") || p.Name.Contains("GAMING"));
                }
                else
                {
                    query = query.Where(p => p.Category.Name == intent.Category);
                }
            }

            // Lọc theo budget nếu có
            if (intent.Budget.HasValue)
            {
                decimal budget = intent.Budget.Value;
                decimal min = budget * 0.7m; // 70% budget
                decimal max = budget * 1.3m; // 130% budget

                // Ưu tiên sản phẩm trong khoảng budget
                var inRangeProducts = await query
                    .Where(p => p.Price >= min && p.Price <= max)
                    .ToListAsync();

                if (inRangeProducts.Any())
                {
                    // Sắp xếp theo độ gần với budget
                    return inRangeProducts
                        .OrderBy(p => Math.Abs((double)(p.Price - budget)))
                        .Take(4)
                        .ToList();
                }

                // Nếu không có sản phẩm trong khoảng, lấy sản phẩm gần nhất
                var allProducts = await query.ToListAsync();
                return allProducts
                    .OrderBy(p => Math.Abs((double)(p.Price - budget)))
                    .Take(4)
                    .ToList();
            }

            // Nếu không có budget, lấy sản phẩm nổi bật
            var products = await query
                .OrderByDescending(p => p.Price)
                .Take(4)
                .ToListAsync();

            // Nếu không tìm thấy, trả về sản phẩm nổi bật chung
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

        // ===== TẠO PROMPT THÔNG MINH =====
        private string GenerateSmartPrompt(string userMessage, UserIntent intent, List<Product> products)
        {
            string productInfo = products.Any()
                ? string.Join("\n", products.Select(p => $"- {p.Name}: {p.Price:N0}đ (Còn {p.Stock} cái)"))
                : "";

            string basePrompt = $@"
Bạn là nhân viên tư vấn PC chuyên nghiệp của shop Linh Kiện PC.
Khách hàng đang cần tư vấn. Hãy trả lời một cách tự nhiên, thân thiện.

KHÁCH HỎI: {userMessage}

";

            switch (intent.Action)
            {
                case "greeting":
                    return basePrompt + @"
YÊU CẦU TRẢ LỜI:
- Chào hỏi thân thiện, vui vẻ
- Giới thiệu ngắn gọn shop (bán linh kiện PC, build PC)
- Hỏi khách cần tư vấn gì
- Dùng emoji 😄
- KHÔNG gửi sản phẩm

VÍ DỤ: 'Chào bạn! Mình là nhân viên tư vấn của Linh Kiện PC. Bạn cần tìm linh kiện gì hay muốn build PC tầm giá nào ạ? 😄'";

                case "thank":
                    return basePrompt + @"
YÊU CẦU TRẢ LỜI:
- Đáp lại lời cảm ơn một cách thân thiện
- Mời hỏi thêm nếu cần
- KHÔNG gửi sản phẩm

VÍ DỤ: 'Không có gì đâu bạn ơi! Rất vui được hỗ trợ bạn. Có gì cần thêm cứ hỏi mình nha 😄'";

                case "goodbye":
                    return basePrompt + @"
YÊU CẦU TRẢ LỜI:
- Chào tạm biệt thân thiện
- Mời quay lại khi cần
- KHÔNG gửi sản phẩm

VÍ DỤ: 'Tạm biệt bạn! Nếu cần tư vấn thêm cứ quay lại shop nha 🔥 Chúc bạn một ngày tốt lành!'";

                case "build_pc":
                    if (intent.Budget.HasValue)
                    {
                        decimal budgetM = intent.Budget.Value / 1000000;
                        return basePrompt + $@"
YÊU CẦU TRẢ LỜI:
- Xác nhận lại budget {budgetM:N0}tr của khách
- Đưa ra 1 gợi ý build PC phù hợp với budget (liệt kê CPU, VGA, RAM, Main)
- Hỏi khách có muốn xem chi tiết linh kiện không
- Dùng emoji 🔥

SẢN PHẨM PHÙ HỢP TRONG SHOP:
{productInfo}

VÍ DỤ: 'Với budget {budgetM:N0}tr, bạn có thể build: i5-13400F + RTX 3060 + 16GB RAM. Mấy em này đang có sẵn trong shop nè bạn xem qua! 🔥'";
                    }
                    else
                    {
                        return basePrompt + @"
YÊU CẦU TRẢ LỜI:
- Hỏi rõ budget của khách
- Gợi ý các mức giá phổ biến (15tr, 20tr, 25tr, 30tr)
- KHÔNG gửi sản phẩm

VÍ DỤ: 'Bạn muốn build PC tầm giá nào ạ? Shop có các mức 15tr, 20tr, 25tr, 30tr đều build được nè 😄'";
                    }

                case "product_inquiry":
                    if (products.Any())
                    {
                        return basePrompt + $@"
YÊU CẦU TRẢ LỜI:
- Giới thiệu ngắn gọn các sản phẩm phù hợp
- Hỏi khách thích sản phẩm nào để tư vấn thêm
- Dùng emoji 😄
- CHỈ gửi card sản phẩm bên dưới

SẢN PHẨM TRONG SHOP:
{productInfo}

VÍ DỤ: 'Đây là các sản phẩm phù hợp với nhu cầu của bạn nè! Bạn thích em nào để mình tư vấn thêm không? 😄'";
                    }
                    else
                    {
                        return basePrompt + @"
YÊU CẦU TRẢ LỜI:
- Thông báo không tìm thấy sản phẩm phù hợp
- Hỏi khách muốn xem sản phẩm khác không
- KHÔNG gửi sản phẩm

VÍ DỤ: 'Hiện shop không có sản phẩm phù hợp với yêu cầu của bạn. Bạn muốn xem sản phẩm khác không ạ? 😄'";
                    }

                case "check_stock":
                    if (products.Any())
                    {
                        return basePrompt + $@"
YÊU CẦU TRẢ LỜI:
- Xác nhận sản phẩm còn hàng
- Giới thiệu sản phẩm
- Hỏi khách cần tư vấn thêm không

SẢN PHẨM CÒN HÀNG:
{productInfo}";
                    }
                    else
                    {
                        return basePrompt + @"
YÊU CẦU TRẢ LỜI:
- Thông báo sản phẩm đang hết hàng
- Xin lỗi khách
- Hỏi khách muốn xem sản phẩm tương tự không

VÍ DỤ: 'Rất tiếc, sản phẩm bạn hỏi đang hết hàng bạn ơi 😢 Bạn muốn xem sản phẩm tương tự không ạ?'";
                    }

                default:
                    return basePrompt + @"
YÊU CẦU TRẢ LỜI:
- Trả lời tự nhiên, thân thiện
- Hỏi khách cần tư vấn gì
- KHÔNG gửi sản phẩm nếu không cần thiết

VÍ DỤ: 'Dạ, shop mình có đủ linh kiện PC bạn nha! Bạn cần tư vấn gì ạ? 😄'";
            }
        }

        // ===== TRÍCH XUẤT BUDGET CHÍNH XÁC =====
        private decimal? ExtractBudget(string message)
        {
            // Pattern: 15tr, 20 triệu, 15-20tr, khoảng 15tr, dưới 15tr, trên 15tr
            var match = System.Text.RegularExpressions.Regex.Match(message, @"(\d+(?:\.?\d*)?)\s*(tr|triệu|trieu)");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal budget))
            {
                return budget * 1000000;
            }

            // Pattern: 15000000, 20.000.000
            match = System.Text.RegularExpressions.Regex.Match(message, @"(\d{1,2}(?:\.?\d{3})*)");
            if (match.Success)
            {
                string numStr = match.Groups[1].Value.Replace(".", "");
                if (decimal.TryParse(numStr, out decimal num) && num > 1000000)
                {
                    return num;
                }
            }

            return null;
        }

        // ===== CÁC HÀM TIỆN ÍCH =====
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

        private string GetSmartDefaultResponse(string userMessage, UserIntent intent)
        {
            switch (intent.Action)
            {
                case "greeting":
                    return "Chào bạn! Mình là nhân viên tư vấn của Linh Kiện PC. Bạn cần tìm linh kiện gì hay muốn build PC tầm giá nào ạ? 😄";

                case "thank":
                    return "Không có gì đâu ạ! Cần tư vấn thêm gì bạn cứ hỏi nha 😄";

                case "goodbye":
                    return "Tạm biệt bạn! Nếu cần gì cứ quay lại shop nha 🔥";

                case "build_pc":
                    if (intent.Budget.HasValue)
                    {
                        decimal budget = intent.Budget.Value / 1000000;
                        return $"PC tầm {budget:N0}tr thì đây là các linh kiện phù hợp nè bạn! Bạn muốn xem chi tiết build nào không ạ? 😄";
                    }
                    else
                    {
                        return "Bạn muốn build PC tầm giá nào ạ? Shop có các mức 15tr, 20tr, 25tr, 30tr đều build được nè 😄";
                    }

                case "product_inquiry":
                    return "Đây là các sản phẩm phù hợp với nhu cầu của bạn nè! Bạn thích em nào để mình tư vấn thêm không? 😄";

                case "check_stock":
                    return "Để mình kiểm tra tồn kho giúp bạn nha! Bạn quan tâm sản phẩm cụ thể nào ạ? 😊";

                default:
                    return "Shop mình có đủ linh kiện PC: CPU, VGA, RAM, Mainboard... Bạn cần tư vấn gì ạ? 😄";
            }
        }
    }

    public class SendMessageRequest { public string Content { get; set; } }
    public class StaffReplyRequest { public string UserId { get; set; } public string Content { get; set; } }
}