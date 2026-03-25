using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
namespace WebLinhKienPc.Controllers
{
	public class SiteInfoController: Controller
	{
		ApplicationDbContext _context;

		public SiteInfoController(ApplicationDbContext context)
		{
			_context = context;
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
		public IActionResult CreateOrEdit(SiteInfo model)
		{
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

			if (existing == null)
			{
				_context.SiteInfos.Add(model);
			}
			else
			{
				existing.Name = model.Name;
				existing.Phone = model.Phone;
				existing.Email = model.Email;
				existing.SiteURL = model.SiteURL;
				existing.Slogan = model.Slogan;

				_context.Addresses.RemoveRange(existing.Addresses);
				_context.WorkHours.RemoveRange(existing.WorkHours);

				existing.Addresses = model.Addresses ?? new List<SiteAddress>();
				existing.WorkHours = model.WorkHours ?? new List<WorkHour>();
			}

			_context.SaveChanges();

			return RedirectToAction("Index", "Product");
		}
	}
}
