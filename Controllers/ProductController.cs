using Microsoft.AspNetCore.Mvc;
using WebLinhKienPc.AppDbContext;
using Microsoft.EntityFrameworkCore;

namespace WebLinhKienPc.Controllers
{
	public class ProductController: Controller
	{
		ApplicationDbContext _context;

		public ProductController(ApplicationDbContext context) { _context = context; }
		public IActionResult Index(string keyword, int? categoryId, decimal? minPrice, decimal? maxPrice)
		{
			var query = _context.Products
				.Include(p => p.Category)
				.AsQueryable();

			// tìm kiếm theo tên
			if (!string.IsNullOrEmpty(keyword))
			{
				query = query.Where(p => p.Name.Contains(keyword));
			}

			// lọc theo danh mục
			if (categoryId.HasValue)
			{
				query = query.Where(p => p.CategoryId == categoryId);
			}

			// lọc theo giá
			if (minPrice.HasValue)
			{
				query = query.Where(p => p.Price >= minPrice);
			}

			if (maxPrice.HasValue)
			{
				query = query.Where(p => p.Price <= maxPrice);
			}

			var products = query
				.OrderByDescending(p => p.CreatedDate)
				.ToList();

			ViewBag.Categories = _context.Categories.ToList();

			return View(products);
		}

		public async Task<IActionResult> Details(int id)
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
