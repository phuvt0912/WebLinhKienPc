using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using WebLinhKienPc.ViewModels;
namespace WebLinhKienPc.Controllers
{
	public class AdminProductController: Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<IdentityUser> _userManager;

		public AdminProductController(
			ApplicationDbContext context,
			UserManager<IdentityUser> userManager)
		{
			_context = context;
			_userManager = userManager;
		}

		public IActionResult Index(string keyword, int? categoryId)
		{
			var products = _context.Products
				.Include(p => p.Category)
				.AsQueryable();

			if (!string.IsNullOrEmpty(keyword))
				products = products.Where(p => p.Name.Contains(keyword));

			if (categoryId.HasValue)
				products = products.Where(p => p.CategoryId == categoryId);

			var vm = new ProductAdminViewModel
			{
				Products = products.ToList(),
				Keyword = keyword,
				CategoryId = categoryId,
				Categories = new SelectList(_context.Categories, "CategoryId", "Name")
			};

			return View(vm);
		}

		public IActionResult CreateProduct()
		{
			ViewBag.Categories =
				new SelectList(_context.Categories, "CategoryId", "Name");

			return View();
		}

		[HttpPost]
		public IActionResult CreateProduct(Product product)
		{
			_context.Products.Add(product);
			_context.SaveChanges();

			return RedirectToAction("Index");
		}

		public IActionResult EditProduct(int id)
		{
			var product = _context.Products.Find(id);

			ViewBag.Categories =
				new SelectList(_context.Categories, "CategoryId", "Name");

			return View(product);
		}

		[HttpPost]
		public IActionResult EditProduct(Product product)
		{
			_context.Products.Update(product);
			_context.SaveChanges();

			return RedirectToAction("Index");
		}

		public IActionResult DeleteProduct(int id)
		{
			var product = _context.Products.Find(id);

			_context.Products.Remove(product);

			_context.SaveChanges();

			return RedirectToAction("Index");
		}
	}
}
