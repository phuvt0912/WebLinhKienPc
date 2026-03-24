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

        [Required(ErrorMessage = "Vui lòng chọn phương thức vận chuyển")]
        public string ShippingMethod { get; set; } = "standard";

        public List<CartItem> CartItems { get; set; } = new();

        // Phí ship theo phương thức
        public static decimal GetShippingFee(string method) => method switch
        {
            "express" => 50_000,
            "sameday" => 100_000,
            _ => 30_000   // bình thường
        };

        public static string GetShippingLabel(string method) => method switch
        {
            "express" => "Giao nhanh (1-2 ngày)",
            "sameday" => "Hỏa tốc (trong ngày)",
            _ => "Bình thường (3-5 ngày)"
        };

        public decimal ShippingFee => GetShippingFee(ShippingMethod);
        public decimal SubTotal => CartItems?.Sum(i => (i.Product?.Price ?? 0) * i.Quantity) ?? 0;
        public decimal TotalPrice => SubTotal + ShippingFee;
    }
}