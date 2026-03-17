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

		public IActionResult AddToCart(int productId, int quantity)
		{
			var userId = _userManager.GetUserId(User);
			var cart = _context.Carts
				.Include(c => c.CartItems)
				.ThenInclude(i => i.Product)
				.FirstOrDefault(c => c.UserId == userId);

			if(cart == null)
			{
				cart = new Cart
				{
					UserId = userId,
					CartItems = new List<CartItem>()
				};

				_context.Carts.Add(cart);
				_context.SaveChanges();
			}

			var cartitem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
			if(cartitem == null)
			{
				cart.CartItems.Add(
					new CartItem
					{
						ProductId = productId,
						Quantity = quantity
					});
			}
			else
			{
				cartitem.Quantity+= quantity;
			}
			_context.SaveChanges();
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
