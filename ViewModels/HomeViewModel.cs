using WebLinhKienPc.Models;

namespace WebLinhKienPc.ViewModels
{
	public class HomeViewModel
	{
		public List<HotProductItem> HotProducts { get; set; }
		public List<Product> NewProducts { get; set; }
		public List<Banner> Banners { get; set; }
		public List<CategoryWithProducts> Categories { get; set; }
	}

	public class HotProductItem
	{
		public Product Product { get; set; }
		public int SoldCount { get; set; }
	}

	public class CategoryWithProducts
	{
		public int CategoryId { get; set; }
		public string CategoryName { get; set; }
		public List<Product> Products { get; set; }
	}
}
