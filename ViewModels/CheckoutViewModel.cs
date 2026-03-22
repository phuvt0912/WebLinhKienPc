using System.ComponentModel.DataAnnotations;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.ViewModels
{
	public class CheckoutViewModel
	{
		[Required(ErrorMessage = "Vui lòng nhập tên")]
		public string Name { get; set; }

		[Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
		[RegularExpression(@"^(0|\+84)[3|5|7|8|9][0-9]{8}$",
			ErrorMessage = "Số điện thoại không hợp lệ")]
		public string Phone { get; set; }

		[Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
		public string Address { get; set; }

		public List<CartItem> CartItems { get; set; }
	}
}