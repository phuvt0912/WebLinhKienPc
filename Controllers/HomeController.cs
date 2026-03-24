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

			var model = new HomeViewModel
			{
				HotProducts = _context.Products
					.OrderByDescending(p => p.ProductId)
					.Take(10)
					.ToList(),

				NewProducts = _context.Products
					.OrderByDescending(p => p.CreatedDate)
					.Take(10)
					.ToList(),

				Categories = categories.Select(c => new CategoryWithProducts
				{
					CategoryName = c.Name,
					Products = c.Products.ToList()
				}).ToList(),

				Banners = _context.Banners.ToList() // 👈 lấy banner
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
