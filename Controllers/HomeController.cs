using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.Controllers
{
	public class HomeController : Controller
	{

      public IActionResult Index()
		{
			return View(); 
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
