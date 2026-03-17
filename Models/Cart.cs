using System.ComponentModel.DataAnnotations;

namespace WebLinhKienPc.Models
{
	public class Cart
	{
		[Key]
		public int CartId { get; set; }

		public string? UserId { get; set; }

		public DateTime CreatedDate { get; set; } = DateTime.Now;

		public ICollection<CartItem>? CartItems { get; set; }
	}
}
