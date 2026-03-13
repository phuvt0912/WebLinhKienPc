using System.ComponentModel.DataAnnotations;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.Models
{
	public class OrderDetail
	{
		[Key]
		public int OrderDetailId { get; set; }

		public int OrderId { get; set; }

		public int ProductId { get; set; }

		public int Quantity { get; set; }

		public decimal Price { get; set; }

		public Order Order { get; set; }

		public Product Product { get; set; }
	}
}
