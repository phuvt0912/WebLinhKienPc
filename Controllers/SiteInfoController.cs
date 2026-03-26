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

				if (!model.Addresses.Any())
					model.Addresses.Add(new SiteAddress());

				if (!model.WorkHours.Any())
					model.WorkHours.Add(new WorkHour());
			}

			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateOrEdit(SiteInfo model, IFormFile imageFile)
		{
			// ===== VALIDATE LIST =====
			model.Addresses = model.Addresses?
				.Where(x => !string.IsNullOrWhiteSpace(x.Street))
				.ToList();

			model.WorkHours = model.WorkHours?
				.Where(x => x.OpenHour < x.CloseHour)
				.ToList();

			if (!ModelState.IsValid)
			{
				model.Addresses ??= new List<SiteAddress>();
				model.WorkHours ??= new List<WorkHour>();
				return View("CreateOrEdit", model);
			}

			var existing = _context.SiteInfos
				.Include(x => x.Addresses)
				.Include(x => x.WorkHours)
				.FirstOrDefault();

			string logoPath = null;

			// ===== 1. UPLOAD FILE =====
			if (imageFile != null && imageFile.Length > 0)
			{
				var ext = Path.GetExtension(imageFile.FileName).ToLower();

				var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
				if (!allowed.Contains(ext))
				{
					ModelState.AddModelError("", "File không hợp lệ");
					return View("CreateOrEdit", model);
				}

				if (imageFile.Length > 5 * 1024 * 1024)
				{
					ModelState.AddModelError("", "Ảnh tối đa 5MB");
					return View("CreateOrEdit", model);
				}

				var fileName = Guid.NewGuid() + ext;
				var uploadPath = Path.Combine(_env.WebRootPath, "uploads");

				if (!Directory.Exists(uploadPath))
					Directory.CreateDirectory(uploadPath);

				var filePath = Path.Combine(uploadPath, fileName);

				using (var stream = new FileStream(filePath, FileMode.Create))
				{
					await imageFile.CopyToAsync(stream);
				}

				logoPath = "/uploads/" + fileName;
			}

			// ===== 2. URL =====
			else if (!string.IsNullOrWhiteSpace(model.LogoUrl))
			{
				logoPath = model.LogoUrl;
			}

			// ===== SAVE =====
			if (existing == null)
			{
				if (logoPath != null)
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

				// ===== LOGO (QUAN TRỌNG) =====
				if (logoPath != null)
				{
					existing.LogoUrl = logoPath;
				}
				// nếu không upload + không nhập URL → giữ nguyên ảnh cũ

				// ===== ADDRESS =====
				_context.Addresses.RemoveRange(existing.Addresses);
				existing.Addresses = model.Addresses ?? new List<SiteAddress>();

				// ===== WORK HOURS =====
				_context.WorkHours.RemoveRange(existing.WorkHours);
				existing.WorkHours = model.WorkHours ?? new List<WorkHour>();
			}

			await _context.SaveChangesAsync();

			return RedirectToAction("Index", "Product");
		}
	}
}