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
			return View();
		}
		public IActionResult Contact()
		{
			return View();
		}
        [HttpPost]
        public IActionResult Contact(string Name, string Email, string Phone, string Message)
        {
	
			TempData["msg"] = "Gửi thành công!";
            return RedirectToAction("Contact");
        }
    }
}
