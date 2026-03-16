using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using Microsoft.AspNetCore.Authorization;
namespace WebLinhKienPc.Controllers
{
	[Authorize(Roles = "Admin, NhanVien")]
	public class AdminCategoryController : Controller
	{
		private readonly ApplicationDbContext _context;

		public AdminCategoryController(ApplicationDbContext context)
		{
			_context = context;
		}

		// Danh sách
		public async Task<IActionResult> Index()
		{
			return View(await _context.Categories.ToListAsync());
		}

		// GET: Create
		public IActionResult CreateCategory()
		{
			return View();
		}

		// POST: Create
		[HttpPost]
		public async Task<IActionResult> CreateCategory(Category category)
		{
			if (ModelState.IsValid)
			{
				_context.Add(category);
				await _context.SaveChangesAsync();
				return RedirectToAction(nameof(Index));
			}

			return View(category);
		}

		// GET: Edit
		public async Task<IActionResult> EditCategory(int id)
		{
			var category = await _context.Categories.FindAsync(id);

			if (category == null)
				return NotFound();

			return View(category);
		}

		// POST: Edit
		[HttpPost]
		public async Task<IActionResult> EditCategory(int id, Category category)
		{
			if (id != category.CategoryId)
				return NotFound();

			if (ModelState.IsValid)
			{
				_context.Update(category);
				await _context.SaveChangesAsync();
				return RedirectToAction(nameof(Index));
			}

			return View(category);
		}
	}
}