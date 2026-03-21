using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

		public IActionResult Index(OrderStatus? status)
		{
			var orders = _context.Orders.AsQueryable();

			if (status.HasValue)
			{
				orders = orders.Where(o => o.Status == status.Value);
			}

			ViewBag.TotalRevenue = _context.Orders
				.Where(o => o.Status == OrderStatus.Completed)
				.Sum(o => (decimal?)o.TotalPrice) ?? 0;

			ViewBag.ProductCount = _context.Products.Count();

			ViewBag.OrderCount = _context.Orders.Count();

			ViewBag.UserCount = _userManager.Users.Count();

			var orderList = orders
				.OrderByDescending(o => o.OrderDate)
				.Take(10)
				.ToList();

			return View(orderList);
		}
	}
}
