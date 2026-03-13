using System.ComponentModel.DataAnnotations;

namespace WebLinhKienPc.Models
{
	public class ProductImage
	{
		[Key]
		public int ImageId { get; set; }

		public string ImageUrl { get; set; }

		public int ProductId { get; set; }

		public Product Product { get; set; }
	}
}
