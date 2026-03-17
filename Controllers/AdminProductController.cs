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
        public async Task<IActionResult> CreateProduct(Product product, IFormFile? imageFile)
        {
            // Xử lý upload file ảnh
            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                var savePath = Path.Combine("wwwroot/images/products", fileName);
                Directory.CreateDirectory("wwwroot/images/products");

                using var stream = new FileStream(savePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                product.ImageUrl = "/images/products/" + fileName;
            }

            if (ModelState.IsValid)
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }

            ViewBag.Categories = new SelectList(_context.Categories, "CategoryId", "Name");
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
        public async Task<IActionResult> EditProduct(Product product, IFormFile? imageFile)
        {
            // Nếu có upload file mới thì thay ảnh cũ
            if (imageFile != null && imageFile.Length > 0)
            {
                // Xóa ảnh cũ nếu là file local
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

            if (ModelState.IsValid)
            {
                _context.Products.Update(product);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }

            ViewBag.Categories = new SelectList(
                _context.Categories, "CategoryId", "Name", product.CategoryId);

            return View(product);
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

            return RedirectToAction("Index");
        }
    }
}