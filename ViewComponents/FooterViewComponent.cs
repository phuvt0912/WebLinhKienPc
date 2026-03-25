using Microsoft.AspNetCore.Mvc;
using WebLinhKienPc.AppDbContext;
using Microsoft.EntityFrameworkCore;
namespace WebLinhKienPc.ViewComponents
{
	public class FooterViewComponent : ViewComponent
	{
		private readonly ApplicationDbContext _context;

		public FooterViewComponent(ApplicationDbContext context)
		{
			_context = context;
		}

		public IViewComponentResult Invoke()
		{
			var site = _context.SiteInfos
				.Include(x => x.Addresses)
				.Include(x => x.WorkHours)
				.FirstOrDefault();

			return View(site);
		}
	}
}
