using Microsoft.AspNetCore.Mvc.Rendering;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.ViewModels
{
	public class ProductAdminViewModel
	{
		public IEnumerable<Product> Products { get; set; }

		public string Keyword { get; set; }

		public int? CategoryId { get; set; }

		public SelectList Categories { get; set; }
		public string? StockStatus { get; set; }
	}
}
