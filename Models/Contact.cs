using System.ComponentModel.DataAnnotations;

namespace WebLinhKienPc.Models
{
    public class Contact
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Họ và tên")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2-100 ký tự")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [RegularExpression(@"^(0|\+84)[3|5|7|8|9][0-9]{8}$", ErrorMessage = "Số điện thoại không hợp lệ")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung")]
        [Display(Name = "Nội dung")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Nội dung phải từ 10-1000 ký tự")]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;
    }
}