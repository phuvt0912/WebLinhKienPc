using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using WebLinhKienPc.ViewModels;

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
			var siteinfo = _context.SiteInfos
				.Include(x => x.Addresses)
				.Include(x => x.WorkHours)
				.FirstOrDefault();

			var vm = new ContactViewModel
			{
				SiteInfo = siteinfo,
				Contact = new Contact()
			};

			return View(vm);
		}

        // GET: Trang liên hệ
        public IActionResult Contact()
        {
			var siteinfo = _context.SiteInfos
				.Include(x => x.Addresses)
				.Include(x => x.WorkHours)
				.FirstOrDefault();

			var vm = new ContactViewModel
			{
				SiteInfo = siteinfo,
				Contact = new Contact()
			};

			return View(vm);
		}

        // POST: Xử lý form liên hệ
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

                TempData["Success"] = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi sớm nhất.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra, vui lòng thử lại sau.";
            }

            return RedirectToAction("Contact");
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