using Microsoft.AspNetCore.Mvc;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using WebLinhKienPc.ViewModels;
namespace WebLinhKienPc.Controllers
{
	public class BannerController: Controller
	{
		private readonly ApplicationDbContext _context;

		public BannerController(ApplicationDbContext context)
		{
			_context = context;
		}

		// ================= INDEX =================
		public IActionResult Index(string keyword)
		{
			var banners = _context.Banners.AsQueryable();

			if (!string.IsNullOrEmpty(keyword))
			{
				banners = banners.Where(b =>
					b.Title.Contains(keyword) ||
					b.Link.Contains(keyword));
			}

			var model = new BannerViewModel
			{
				Banners = banners.ToList(),
				Keyword = keyword
			};

			return View(model);
		}

		// ================= CREATE =================
		public IActionResult CreateBanner()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateBanner(Banner banner, IFormFile? imageFile)
		{
			if (!ModelState.IsValid)
				return View(banner);

			// ===== XỬ LÝ ẢNH =====
			if (imageFile != null && imageFile.Length > 0)
			{
				// Validate size
				if (imageFile.Length > 5 * 1024 * 1024)
				{
					ModelState.AddModelError("imageFile", "Ảnh tối đa 5MB");
					return View(banner);
				}

				// Validate type
				var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
				if (!allowedTypes.Contains(imageFile.ContentType))
				{
					ModelState.AddModelError("imageFile", "Chỉ chấp nhận JPG, PNG, WEBP, GIF");
					return View(banner);
				}

				// Tạo folder
				var root = Directory.GetCurrentDirectory();
				var folder = Path.Combine(root, "wwwroot/images/banners");

				if (!Directory.Exists(folder))
					Directory.CreateDirectory(folder);

				// Lưu file
				var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
				var path = Path.Combine(folder, fileName);

				using (var stream = new FileStream(path, FileMode.Create))
				{
					await imageFile.CopyToAsync(stream);
				}

				banner.ImageUrl = "/images/banners/" + fileName;
			}

			// Nếu không có ảnh → default
			if (string.IsNullOrEmpty(banner.ImageUrl))
			{
				banner.ImageUrl = "/images/default-banner.png";
			}

			_context.Banners.Add(banner);
			await _context.SaveChangesAsync();
			TempData["Success"] = "Thêm banner mới thành công!";
			return RedirectToAction("Index");
		}

		// ================= EDIT =================
		public IActionResult EditBanner(int id)
		{
			var banner = _context.Banners.Find(id);
			if (banner == null) return NotFound();

			return View(banner);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditBanner(int id, Banner banner, IFormFile? imageFile)
		{
			var existing = await _context.Banners.FindAsync(id);
			if (existing == null) return NotFound();

			if (!ModelState.IsValid)
				return View(banner);

			// ===== XỬ LÝ ẢNH =====
			if (imageFile != null && imageFile.Length > 0)
			{
				// Validate
				if (imageFile.Length > 5 * 1024 * 1024)
				{
					ModelState.AddModelError("imageFile", "Ảnh tối đa 5MB");
					return View(banner);
				}

				var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
				if (!allowedTypes.Contains(imageFile.ContentType))
				{
					ModelState.AddModelError("imageFile", "File không hợp lệ");
					return View(banner);
				}

				// Xoá ảnh cũ nếu là local
				if (!string.IsNullOrEmpty(existing.ImageUrl) &&
					existing.ImageUrl.StartsWith("/images/banners/"))
				{
					var oldPath = Path.Combine(
						Directory.GetCurrentDirectory(),
						"wwwroot",
						existing.ImageUrl.TrimStart('/')
					);

					if (System.IO.File.Exists(oldPath))
						System.IO.File.Delete(oldPath);
				}

				// Lưu ảnh mới
				var root = Directory.GetCurrentDirectory();
				var folder = Path.Combine(root, "wwwroot/images/banners");

				if (!Directory.Exists(folder))
					Directory.CreateDirectory(folder);

				var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
				var path = Path.Combine(folder, fileName);

				using (var stream = new FileStream(path, FileMode.Create))
				{
					await imageFile.CopyToAsync(stream);
				}

				existing.ImageUrl = "/images/banners/" + fileName;
			}
			else
			{
				// Nếu nhập URL mới thì update
				if (!string.IsNullOrEmpty(banner.ImageUrl))
				{
					existing.ImageUrl = banner.ImageUrl;
				}
				// Không thì giữ nguyên ảnh cũ
			}

			// Update dữ liệu
			existing.Title = banner.Title;
			existing.Link = banner.Link;

			await _context.SaveChangesAsync();
			TempData["Success"] = "Cập nhật banner thành công!";
			return RedirectToAction("Index");
		}

		// ================= DELETE =================
		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult DeleteBanner(int id)
		{
			var banner = _context.Banners.Find(id);
			if (banner == null) return NotFound();

			_context.Banners.Remove(banner);
			_context.SaveChanges();
			TempData["Success"] = "Xóa banner thành công!";
			return RedirectToAction("Index");
		}
	}
}
