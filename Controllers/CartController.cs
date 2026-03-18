using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using Microsoft.AspNetCore.Authorization;

namespace WebLinhKienPc.Controllers
{
	[Authorize]
	public class CartController: Controller
	{
		ApplicationDbContext _context;
		private readonly UserManager<IdentityUser> _userManager;
		private readonly SignInManager<IdentityUser> _signInManager;

		public CartController(ApplicationDbContext context,
			UserManager<IdentityUser> userManager,
			SignInManager<IdentityUser> signInManager
			)
		{
			_context = context;
			_userManager = userManager;
			_signInManager = signInManager;
		}

		public IActionResult Index()
		{
			var userId = _userManager.GetUserId(User);

			var cart =  _context.Carts
				.Include(c => c.CartItems)
				.ThenInclude(i => i.Product)
				.FirstOrDefault(c => c.UserId == userId);
			return View(cart?.CartItems ?? new List<CartItem>());
		}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddToCart(int productId, int quantity)
        {
            if (!User.Identity.IsAuthenticated)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, requireLogin = true });
                return RedirectToAction("Login", "Account");
            }

            var userId = _userManager.GetUserId(User);

            // Kiểm tra sản phẩm tồn tại
            var product = _context.Products.Find(productId);
            if (product == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Sản phẩm không tồn tại." });
                return RedirectToAction("Index", "Product");
            }

            var cart = _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId, CartItems = new List<CartItem>() };
                _context.Carts.Add(cart);
                _context.SaveChanges();
            }

            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
            int currentQtyInCart = cartItem?.Quantity ?? 0;
            int newQty = currentQtyInCart + quantity;

            // Validate không vượt quá tồn kho
            if (newQty > product.Stock)
            {
                int canAdd = product.Stock - currentQtyInCart;

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    if (canAdd <= 0)
                        return Json(new { success = false, message = $"Sản phẩm này đã có {currentQtyInCart} trong giỏ, không thể thêm (tồn kho: {product.Stock})." });
                    else
                        return Json(new { success = false, message = $"Chỉ có thể thêm tối đa {canAdd} sản phẩm nữa (tồn kho: {product.Stock})." });
                }

                TempData["Error"] = $"Không thể thêm, tồn kho chỉ còn {product.Stock}.";
                return RedirectToAction("Index", "Cart");
            }

            // Thêm vào giỏ
            if (cartItem == null)
            {
                cart.CartItems.Add(new CartItem { ProductId = productId, Quantity = quantity });
            }
            else
            {
                cartItem.Quantity = newQty;
            }
            _context.SaveChanges();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var updatedCart = _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefault(c => c.UserId == userId);
                var totalItems = updatedCart?.CartItems.Sum(i => i.Quantity) ?? 0;
                return Json(new { success = true, cartCount = totalItems });
            }

            return RedirectToAction("Index");
        }

        public IActionResult Increase(int id)
		{
			var userId = _userManager.GetUserId(User);
			var cart = _context.Carts
				.Include(c => c.CartItems)
				.ThenInclude(p => p.Product)
				.FirstOrDefault(u => u.UserId == userId);
			if (cart == null)
			{
				return NotFound();
			}
			var cartitem = cart.CartItems.FirstOrDefault(p => p.CartItemId == id);
			if(cartitem == null)
			{
				return NotFound();
			}
			cartitem.Quantity++;
			_context.SaveChanges();
			return RedirectToAction("Index");
		}

		public IActionResult Decrease(int id)
		{
			var userId = _userManager.GetUserId(User);
			var cart = _context.Carts
				.Include(c => c.CartItems)
				.ThenInclude(p => p.Product)
				.FirstOrDefault(u => u.UserId == userId);
			if (cart == null)
			{
				return NotFound();
			}
			var cartitem = cart.CartItems.FirstOrDefault(p => p.CartItemId == id);
			if (cartitem == null)
			{
				return NotFound();
			}
			if(cartitem.Quantity > 1)
			{
				cartitem.Quantity--;
				_context.SaveChanges();
			}
			else if (cartitem.Quantity == 1)
			{
				return RedirectToAction("Remove", new { id = id });
			}
			return RedirectToAction("Index");
		}

		public IActionResult Remove(int id)
		{
			var userId = _userManager.GetUserId(User);
			var cart = _context.Carts
				.Include(c => c.CartItems)
				.ThenInclude(p => p.Product)
				.FirstOrDefault(u => u.UserId == userId);
			if (cart == null)
			{
				return NotFound();
			}
			var cartitem = cart.CartItems.FirstOrDefault(p => p.CartItemId == id);
			if (cartitem == null)
			{
				return NotFound();
			}
			_context.CartItems.Remove(cartitem);
			_context.SaveChanges();
			return RedirectToAction("Index");
		}
	}
}
