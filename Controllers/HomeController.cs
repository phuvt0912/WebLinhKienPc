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
            var vm = new HomeViewModel
            {
                HotProducts = _context.Products
         .OrderByDescending(p => p.Sold)
         .Take(7)
         .ToList(),

                NewProducts = _context.Products
         .OrderByDescending(p => p.ProductId)
         .Take(5)
         .ToList(),

                LapsProducts = _context.Products
         .Where(p => p.Category.Name == "Laptop")
         .Take(5)
         .ToList(),

                Accsessories = _context.Products
         .Where(p => p.Category.Name == "Phụ kiện")
         .Take(5)
         .ToList(),

                Phones = _context.Products
         .Where(p => p.Category.Name == "Điện thoại")
         .Take(5)
         .ToList()
            };

            return View(vm);
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
