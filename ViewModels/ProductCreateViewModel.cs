using System.ComponentModel.DataAnnotations;

public class ProductCreateViewModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; }

    [Required]
    [Display(Name = "Giá")]
    public string Price { get; set; } // Dùng string để nhận giá trị có dấu chấm

    [Required]
    [Display(Name = "Số lượng")]
    public int Stock { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Danh mục")]
    public int CategoryId { get; set; }

    public IFormFile? ImageFile { get; set; }
}