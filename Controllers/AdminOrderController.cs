using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.ViewModels;
using WebLinhKienPc.Models;
namespace WebLinhKienPc.Controllers
{
	public class AdminOrderController: Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<IdentityUser> _userManager;

		public AdminOrderController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
		{
			_context = context;
			_userManager = userManager;
		}

		public IActionResult Index()
		{
			var orders = _context.Orders.ToList();
			return View(orders);
		}

		public IActionResult OrderDetails(int id)
		{
			var orders = _context.Orders
				.Include(od => od.OrderDetails)
				.ThenInclude(p => p.Product)
				.FirstOrDefault(order => order.OrderId == id);
			return View(orders);
		}
	}
}
