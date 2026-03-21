using System.ComponentModel.DataAnnotations;
using WebLinhKienPc.Models;

public class Order
{
	[Key]
	public int OrderId { get; set; }

	public string UserId { get; set; }

	public DateTime OrderDate { get; set; } = DateTime.Now;

	public decimal TotalPrice { get; set; }

	public OrderStatus Status { get; set; } = OrderStatus.Pending;

	public string Name { get; set; }

	public string Phone { get; set; }

	public string Address { get; set; }

	public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

public enum OrderStatus
{
	[Display(Name = "Chờ xử lý")]
	Pending,

	[Display(Name = "Đang giao")]
	Shipping,

	[Display(Name = "Hoàn thành")]
	Completed,

	[Display(Name = "Đã hủy")]
	Cancelled
}