using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using WebLinhKienPc.ViewModels;

namespace WebLinhKienPc.Controllers
{
    [Authorize(Roles = "Admin,NhanVien")]
    public class AdminProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminProductController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index(string keyword, int? categoryId)
        {
            var products = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
                products = products.Where(p => p.Name.Contains(keyword));

            if (categoryId.HasValue)
                products = products.Where(p => p.CategoryId == categoryId);

            var vm = new ProductAdminViewModel
            {
                Products = products.ToList(),
                Keyword = keyword,
                CategoryId = categoryId,
                Categories = new SelectList(_context.Categories, "CategoryId", "Name")
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult CreateProduct()
        {
            ViewBag.Categories = new SelectList(_context.Categories, "CategoryId", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(IFormFile? imageFile)
        {
            // Tạo mới product
            var product = new Product();

            // Thủ công lấy dữ liệu từ form
            product.Name = Request.Form["Name"];
            product.Description = Request.Form["Description"];
            product.CreatedDate = DateTime.Now;

            // Xử lý CategoryId
            if (int.TryParse(Request.Form["CategoryId"], out int categoryId))
            {
                product.CategoryId = categoryId;
            }
            else
            {
                ModelState.AddModelError("CategoryId", "Vui lòng chọn danh mục");
            }

            // Xử lý Price - loại bỏ dấu chấm trước khi parse
            string priceString = Request.Form["Price"].ToString().Replace(".", "");
            if (decimal.TryParse(priceString, out decimal price))
            {
                product.Price = price;
            }
            else
            {
                ModelState.AddModelError("Price", "Giá không hợp lệ");
            }

            // Xử lý Stock
            string stockString = Request.Form["Stock"].ToString();
            if (int.TryParse(stockString, out int stock))
            {
                product.Stock = stock;
            }
            else
            {
                ModelState.AddModelError("Stock", "Số lượng không hợp lệ");
            }

            // XỬ LÝ ẢNH - QUAN TRỌNG: Lấy link từ tab URL
            string imageUrl = Request.Form["ImageUrl"].ToString();

            // Ưu tiên xử lý file upload trước
            if (imageFile != null && imageFile.Length > 0)
            {
                // Kiểm tra định dạng file
                string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                string fileExtension = Path.GetExtension(imageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("imageFile", "Chỉ chấp nhận file ảnh (jpg, jpeg, png, gif, webp)");
                }
                else if (imageFile.Length > 5 * 1024 * 1024) // 5MB
                {
                    ModelState.AddModelError("imageFile", "File ảnh không được vượt quá 5MB");
                }
                else
                {
                    var fileName = Guid.NewGuid() + fileExtension;
                    var savePath = Path.Combine("wwwroot/images/products", fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));

                    using var stream = new FileStream(savePath, FileMode.Create);
                    await imageFile.CopyToAsync(stream);

                    product.ImageUrl = "/images/products/" + fileName;
                }
            }
            else if (!string.IsNullOrEmpty(imageUrl))
            {
                // Nếu không có file upload, dùng link ảnh từ tab URL
                product.ImageUrl = imageUrl;
            }
            // Nếu không có cả file và link thì product.ImageUrl = null (sẽ hiện camera)

            // Kiểm tra ModelState
            if (ModelState.IsValid)
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm sản phẩm thành công!";
                return RedirectToAction("Index");
            }

            // Nếu có lỗi, load lại danh mục và trả về view với dữ liệu đã nhập
            ViewBag.Categories = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);

            // Log lỗi để debug (xem trong Output window)
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            foreach (var error in errors)
            {
                Console.WriteLine($"Lỗi: {error.ErrorMessage}");
            }

            return View(product);
        }

        [HttpGet]
        public IActionResult EditProduct(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();

            ViewBag.Categories = new SelectList(
                _context.Categories, "CategoryId", "Name", product.CategoryId);

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, IFormFile? imageFile)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Name = Request.Form["Name"];
            product.Description = Request.Form["Description"];

            if (int.TryParse(Request.Form["CategoryId"], out int categoryId))
                product.CategoryId = categoryId;

            string priceString = Request.Form["Price"].ToString().Replace(".", "").Replace(",", "");
            if (decimal.TryParse(priceString, out decimal price))
                product.Price = price;

            if (int.TryParse(Request.Form["Stock"], out int stock))
                product.Stock = stock;

            // Xử lý ảnh
            if (imageFile != null && imageFile.Length > 0)
            {
                // Có file mới → upload
                if (!string.IsNullOrEmpty(product.ImageUrl)
                    && product.ImageUrl.StartsWith("/images/products/"))
                {
                    var oldPath = Path.Combine("wwwroot", product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                var savePath = Path.Combine("wwwroot/images/products", fileName);
                Directory.CreateDirectory("wwwroot/images/products");
                using var stream = new FileStream(savePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);
                product.ImageUrl = "/images/products/" + fileName;
            }
            else
            {
                string newImageUrl = Request.Form["ImageUrl"].ToString().Trim();
                string originalImageUrl = Request.Form["OriginalImageUrl"].ToString().Trim();

                // Chỉ thay đổi nếu URL mới khác URL gốc
                if (!string.IsNullOrEmpty(newImageUrl) && newImageUrl != originalImageUrl)
                {
                    product.ImageUrl = newImageUrl;
                }
                // Ngược lại giữ nguyên product.ImageUrl từ DB
            }

            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật sản phẩm thành công!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            // Xóa file ảnh local nếu có
            if (!string.IsNullOrEmpty(product.ImageUrl)
                && product.ImageUrl.StartsWith("/images/products/"))
            {
                var imgPath = Path.Combine("wwwroot", product.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(imgPath))
                    System.IO.File.Delete(imgPath);
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa sản phẩm thành công!";
            return RedirectToAction("Index");
        }
    }
}