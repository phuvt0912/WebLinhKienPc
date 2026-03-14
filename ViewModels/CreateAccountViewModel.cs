using System.ComponentModel.DataAnnotations;

namespace WebLinhKienPc.ViewModels
{
	public class CreateUserViewModel
	{
		[Required]
		[EmailAddress]
		public string Email { get; set; }

		[Required]
		[DataType(DataType.Password)]
		public string Password { get; set; }

		[Required]
		[DataType(DataType.Password)]
		[Compare("Password", ErrorMessage = "Mật khẩu không khớp")]
		public string ConfirmPassword { get; set; }

		public string Role { get; set; }
	}
}