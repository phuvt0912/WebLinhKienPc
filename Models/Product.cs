using System.ComponentModel.DataAnnotations;

namespace WebLinhKienPc.Models
{
	public class Product
	{
		[Key]
		public int ProductId { get; set; }

		[Required]
		[StringLength(200)]
		public string Name { get; set; }

		[Required]
		public decimal Price { get; set; }

		public int Stock { get; set; }

		public string? Description { get; set; }

		public string? ImageUrl { get; set; }

		public DateTime CreatedDate { get; set; } = DateTime.Now;

		// Foreign Key
		public int CategoryId { get; set; }

		// Navigation
		public Category Category { get; set; }
		public ICollection<ProductImage>? Images { get; set; }
	}
}
