using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.Controllers
{
	public class SiteInfoController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IWebHostEnvironment _env;

		public SiteInfoController(ApplicationDbContext context, IWebHostEnvironment env)
		{
			_context = context;
			_env = env;
		}

		// GET: Hiển thị form
		public IActionResult CreateOrEdit()
		{
			var model = _context.SiteInfos
				.Include(x => x.Addresses)
				.Include(x => x.WorkHours)
				.FirstOrDefault();

			if (model == null)
			{
				model = new SiteInfo
				{
					Addresses = new List<SiteAddress> { new SiteAddress() },
					WorkHours = new List<WorkHour> { new WorkHour() }
				};
			}
			else
			{
				model.Addresses ??= new List<SiteAddress>();
				model.WorkHours ??= new List<WorkHour>();

				if (!model.Addresses.Any()) model.Addresses.Add(new SiteAddress());
				if (!model.WorkHours.Any()) model.WorkHours.Add(new WorkHour());
			}

			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateOrEdit(SiteInfo model, IFormFile? imageFile)
		{
			// ===== VALIDATE LIST =====
			model.Addresses = model.Addresses?
				.Where(x => !string.IsNullOrWhiteSpace(x.Street))
				.ToList();

			model.WorkHours = model.WorkHours?
				.Where(x => x.OpenHour < x.CloseHour)
				.ToList();

			// ===== CHECK LOGO (1 trong 2 là đủ) =====
			if ((imageFile == null || imageFile.Length == 0) && string.IsNullOrWhiteSpace(model.LogoUrl))
			{
				ModelState.AddModelError("LogoUrl", "Vui lòng chọn ảnh hoặc nhập URL");
			}

			if (!ModelState.IsValid)
			{
				model.Addresses ??= new List<SiteAddress>();
				model.WorkHours ??= new List<WorkHour>();
				return View(model);
			}

			var existing = _context.SiteInfos
				.Include(x => x.Addresses)
				.Include(x => x.WorkHours)
				.FirstOrDefault();

			string logoPath = existing?.LogoUrl;

			// ===== 1️⃣ XỬ LÝ FILE UPLOAD =====
			if (imageFile != null && imageFile.Length > 0)
			{
				var ext = Path.GetExtension(imageFile.FileName).ToLower();
				var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
				if (!allowed.Contains(ext))
				{
					ModelState.AddModelError("LogoUrl", "File không hợp lệ");
					return View(model);
				}
				if (imageFile.Length > 5 * 1024 * 1024)
				{
					ModelState.AddModelError("LogoUrl", "Ảnh tối đa 5MB");
					return View(model);
				}

				var uploadPath = Path.Combine(_env.WebRootPath, "uploads");
				if (!Directory.Exists(uploadPath))
					Directory.CreateDirectory(uploadPath);

				var fileName = Guid.NewGuid() + ext;
				var filePath = Path.Combine(uploadPath, fileName);

				using var stream = new FileStream(filePath, FileMode.Create);
				await imageFile.CopyToAsync(stream);

				logoPath = "/uploads/" + fileName;

				// XÓA FILE CŨ nếu là file local
				if (existing != null && !string.IsNullOrEmpty(existing.LogoUrl) && !existing.LogoUrl.StartsWith("http"))
				{
					var oldPath = Path.Combine(_env.WebRootPath, existing.LogoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
					if (System.IO.File.Exists(oldPath))
						System.IO.File.Delete(oldPath);
				}
			}
			// ===== 2️⃣ XỬ LÝ URL =====
			else if (!string.IsNullOrWhiteSpace(model.LogoUrl))
			{
				logoPath = model.LogoUrl;
			}

			// ===== SAVE =====
			if (existing == null)
			{
				model.LogoUrl = logoPath;
				_context.SiteInfos.Add(model);
			}
			else
			{
				existing.Name = model.Name;
				existing.Phone = model.Phone;
				existing.Email = model.Email;
				existing.SiteURL = model.SiteURL;
				existing.Slogan = model.Slogan;
				existing.LogoUrl = logoPath;

				// ===== ADDRESS =====
				_context.Addresses.RemoveRange(existing.Addresses);
				foreach (var addr in model.Addresses ?? new List<SiteAddress>())
					addr.SiteInfoId = existing.Id;
				existing.Addresses = model.Addresses ?? new List<SiteAddress>();

				// ===== WORK HOURS =====
				_context.WorkHours.RemoveRange(existing.WorkHours);
				foreach (var wh in model.WorkHours ?? new List<WorkHour>())
					wh.SiteInfoId = existing.Id;
				existing.WorkHours = model.WorkHours ?? new List<WorkHour>();
			}

			await _context.SaveChangesAsync();
			return RedirectToAction("Index", "Product");
		}
	}
}