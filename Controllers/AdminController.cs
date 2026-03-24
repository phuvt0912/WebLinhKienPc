using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
namespace WebLinhKienPc.Controllers
{
	[Authorize(Roles = "Admin")]
	public class AdminController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<IdentityUser> _userManager;

		public AdminController(
			ApplicationDbContext context,
			UserManager<IdentityUser> userManager)
		{
			_context = context;
			_userManager = userManager;
		}

        public async Task<IActionResult> Index(string status = "")
        {
            // Stats cards
            ViewBag.TotalRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.Completed)
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

            ViewBag.ProductCount = await _context.Products.CountAsync();
            ViewBag.OrderCount = await _context.Orders.CountAsync();
            ViewBag.UserCount = await _userManager.Users.CountAsync();

            // Đơn hàng chờ xử lý
            ViewBag.PendingCount = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Pending);

            // Sản phẩm sắp hết hàng (stock <= 5)
            ViewBag.LowStockCount = await _context.Products
                .CountAsync(p => p.Stock <= 5 && p.Stock > 0);

            ViewBag.LowStockProducts = await _context.Products
                .Where(p => p.Stock <= 5 && p.Stock > 0)
                .OrderBy(p => p.Stock)
                .Take(5)
                .Select(p => new { p.Name, p.Stock, p.ImageUrl })
                .ToListAsync();

            // Top 10 sản phẩm bán chạy - CHỈ tính đơn Completed
            ViewBag.TopProducts = await _context.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.Order.Status == OrderStatus.Completed) // ← thêm dòng này
                .GroupBy(od => new { od.ProductId, od.Product.Name, od.Product.ImageUrl })
                .Select(g => new {
                    g.Key.ProductId,
                    g.Key.Name,
                    g.Key.ImageUrl,
                    TotalSold = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Quantity * x.Price)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(10)
                .ToListAsync();

            // Doanh thu 12 tháng
            var now = DateTime.Now;
            var revenueByMonth = new List<object>();
            for (int i = 11; i >= 0; i--)
            {
                var month = now.AddMonths(-i);
                var revenue = await _context.Orders
                    .Where(o => o.Status == OrderStatus.Completed
                             && o.OrderDate.Year == month.Year
                             && o.OrderDate.Month == month.Month)
                    .SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

                revenueByMonth.Add(new
                {
                    label = month.ToString("MM/yyyy"),
                    revenue = revenue
                });
            }
            ViewBag.RevenueByMonth = revenueByMonth;

            return View();
        }
    }
}
