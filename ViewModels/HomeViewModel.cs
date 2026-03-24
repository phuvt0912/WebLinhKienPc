using WebLinhKienPc.Models;

namespace WebLinhKienPc.ViewModels
{
	public class HomeViewModel
	{
		public List<Product> HotProducts { get; set; }
		public List<Product> NewProducts { get; set; }
		public List<Banner> Banners { get; set; }
		public List<CategoryWithProducts> Categories { get; set; }
	}

	public class CategoryWithProducts
	{
		public string CategoryName { get; set; }
		public List<Product> Products { get; set; }
	}
}
