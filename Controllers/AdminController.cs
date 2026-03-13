using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using WebLinhKienPc.ViewModels;

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

	public IActionResult Index(string status)
	{
		// Query đơn hàng
		var orders = _context.Orders.AsQueryable();

		// Lọc theo trạng thái
		if (!string.IsNullOrEmpty(status))
		{
			orders = orders.Where(o => o.Status == status);
		}

		// Thống kê dashboard
		ViewBag.TotalRevenue = _context.Orders
			.Where(o => o.Status == "Completed")
			.Sum(o => (decimal?)o.TotalPrice) ?? 0;

		ViewBag.ProductCount = _context.Products.Count();

		ViewBag.OrderCount = _context.Orders.Count();

		ViewBag.UserCount = _userManager.Users.Count();

		// Lấy danh sách đơn hàng mới nhất
		var orderList = orders
			.OrderByDescending(o => o.OrderDate)
			.Take(10)
			.ToList();

		return View(orderList);
	}


	public IActionResult OrderDetail(int id)
	{
		var order = _context.Orders
			.Include(o => o.OrderDetails)
			.FirstOrDefault(o => o.OrderId == id);

		if (order == null)
			return NotFound();

		return View(order);
	}
}