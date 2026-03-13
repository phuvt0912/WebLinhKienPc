using System.ComponentModel.DataAnnotations;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.Models
{
	public class Order
	{
		[Key]
		public int OrderId { get; set; }

		public string UserId { get; set; }

		public DateTime OrderDate { get; set; } = DateTime.Now;

		public decimal TotalPrice { get; set; }

		public string Status { get; set; }

		public string Name { get; set; }

		public string Phone { get; set; }

		public string Address { get; set; }

		public ICollection<OrderDetail>? OrderDetails { get; set; }
	}
}
