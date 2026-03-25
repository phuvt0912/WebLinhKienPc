using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using WebLinhKienPc.ViewModels;
using MimeKit;
using MailKit.Net.Smtp;

namespace WebLinhKienPc.Controllers
{
    public class HomeController : Controller
    {
        ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context) { _context = context; }

        public IActionResult Index()
        {
            var categories = _context.Categories
                .Include(c => c.Products)
                .ToList();

            // Lấy top 10 sản phẩm bán chạy nhất từ OrderDetail (chỉ tính đơn Completed)
            var soldStats = _context.OrderDetails
                .Where(od => od.Order.Status == OrderStatus.Completed)
                .GroupBy(od => od.ProductId)
                .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .Take(10)
                .ToList();

            List<HotProductItem> hotProducts;

            if (soldStats.Any())
            {
                var productIds = soldStats.Select(x => x.ProductId).ToList();
                var products = _context.Products
                    .Where(p => productIds.Contains(p.ProductId))
                    .Include(p => p.Category)
                    .ToList();

                hotProducts = soldStats
                    .Join(products, s => s.ProductId, p => p.ProductId, (s, p) => new HotProductItem
                    {
                        Product = p,
                        SoldCount = s.TotalSold
                    })
                    .ToList();
            }
            else
            {
                // Fallback: lấy sản phẩm mới nhất nếu chưa có đơn hàng
                hotProducts = _context.Products
                    .Include(p => p.Category)
                    .OrderByDescending(p => p.ProductId)
                    .Take(10)
                    .ToList()
                    .Select(p => new HotProductItem { Product = p, SoldCount = 0 })
                    .ToList();
            }

            var model = new HomeViewModel
            {
                HotProducts = hotProducts,
                NewProducts = _context.Products
                    .OrderByDescending(p => p.CreatedDate)
                    .Take(10)
                    .ToList(),
                Categories = categories.Select(c => new CategoryWithProducts
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.Name,
                    Products = c.Products.ToList()
                }).ToList(),
                Banners = _context.Banners.ToList()
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult About()
        {
            return View();
        }

        // GET: Trang liên hệ
        public IActionResult Contact()
        {
            return View();
        }

        // POST: Xử lý form liên hệ + gửi email xác nhận
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(string Name, string Email, string Phone, string Message)
        {
            // Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Message))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin bắt buộc (Họ tên, Email, Nội dung).";
                return RedirectToAction("Contact");
            }

            // Kiểm tra email hợp lệ
            if (!IsValidEmail(Email))
            {
                TempData["Error"] = "Email không hợp lệ. Vui lòng nhập đúng định dạng email.";
                return RedirectToAction("Contact");
            }

            try
            {
                // 1. Lưu vào database
                var contact = new Contact
                {
                    Name = Name,
                    Email = Email,
                    Phone = Phone ?? string.Empty,
                    Message = Message,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };

                _context.Contacts.Add(contact);
                await _context.SaveChangesAsync();

                // 2. Gửi email xác nhận cho khách hàng
                await SendConfirmationEmail(Email, Name, Message);

                TempData["Success"] = "Cảm ơn bạn đã liên hệ! Chúng tôi đã gửi email xác nhận đến hộp thư của bạn.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra, vui lòng thử lại sau.";
                // Ghi log lỗi nếu cần
                Console.WriteLine($"Lỗi: {ex.Message}");
            }

            return RedirectToAction("Contact");
        }

        // Hàm gửi email xác nhận cho khách hàng
        private async Task SendConfirmationEmail(string toEmail, string name, string message)
        {
            try
            {
                // Tạo email
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Web Linh Kiện PC", "dominhtoan9467@gmail.com")); // THAY EMAIL CỦA BẠN
                email.To.Add(new MailboxAddress(name, toEmail));
                email.Subject = "Xác nhận liên hệ - Web Linh Kiện PC";

                // Nội dung email HTML
                var body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background: #0a0a1a; color: #c0c0e0;'>
                        <div style='text-align: center; border-bottom: 2px solid #a259ff; padding-bottom: 20px; margin-bottom: 20px;'>
                            <h2 style='color: #c490ff;'>📧 XÁC NHẬN LIÊN HỆ</h2>
                        </div>
                        
                        <p>Xin chào <strong style='color: #a259ff;'>{name}</strong>,</p>
                        
                        <p>Cảm ơn bạn đã liên hệ với <strong>Web Linh Kiện PC</strong>.</p>
                        
                        <p>Chúng tôi đã nhận được tin nhắn của bạn:</p>
                        
                        <div style='background: #12122a; padding: 15px; border-radius: 8px; border-left: 3px solid #a259ff; margin: 15px 0;'>
                            <p style='margin: 0; color: #e8e8ff;'>{message}</p>
                        </div>
                        
                        <p>Đội ngũ hỗ trợ sẽ phản hồi lại bạn trong thời gian sớm nhất (thường trong vòng 24 giờ).</p>
                        
                        <div style='margin-top: 20px; padding: 15px; background: #12122a; border-radius: 8px; text-align: center;'>
                            <p style='margin: 0; color: #8a8aba; font-size: 12px;'>
                                📞 Hotline: 0123 456 789<br>
                                📧 Email: support@linhkienpc.com<br>
                                🌐 Website: www.linhkienpc.com
                            </p>
                        </div>
                        
                        <p style='margin-top: 20px; font-size: 12px; color: #6a6a9a; text-align: center;'>
                            Đây là email tự động, vui lòng không phản hồi email này.
                        </p>
                    </div>
                ";

                email.Body = new TextPart("html") { Text = body };

                // Gửi email
                using (var smtp = new SmtpClient())
                {
                    await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                    await smtp.AuthenticateAsync("dominhtoan9467@gmail.com", "eliz jtwx abtj ojfd"); // THAY EMAIL VÀ MẬT KHẨU
                    await smtp.SendAsync(email);
                    await smtp.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi gửi email nhưng không ảnh hưởng đến việc lưu database
                Console.WriteLine($"Lỗi gửi email: {ex.Message}");
            }
        }

        // Helper: Kiểm tra email hợp lệ
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}