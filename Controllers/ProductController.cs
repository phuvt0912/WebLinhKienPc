using Microsoft.AspNetCore.Mvc;
using WebLinhKienPc.AppDbContext;
using Microsoft.EntityFrameworkCore;

namespace WebLinhKienPc.Controllers
{
	public class ProductController: Controller
	{
		ApplicationDbContext _context;

		public ProductController(ApplicationDbContext context) { _context = context; }
		public IActionResult Index()
		{
			var products = _context.Products
			.Include(p => p.Category)
			.OrderByDescending(p => p.CreatedDate)
			.Take(8)
			.ToList();

			return View(products);
		}

		public async Task<IActionResult> Detail(int id)
		{
			var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.ProductId == id);
			if (product == null)
			{
				return NotFound();
			}
			return View(product);
		}
	}
}
