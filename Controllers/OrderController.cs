using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.ViewModels;
using WebLinhKienPc.Models;
namespace WebLinhKienPc.Controllers
{
	public class OrderController: Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<IdentityUser> _userManager;
		public OrderController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
		{
			_context = context;
			_userManager = userManager;
		}
		public IActionResult Checkout()
		{
			var userId =  _userManager.GetUserId(User);
			var cart = _context.Carts
			.Include(c => c.CartItems)
			.ThenInclude(i => i.Product)
			.FirstOrDefault(c => c.UserId == userId);

			var model = new CheckoutViewModel
			{
				CartItems = cart.CartItems.ToList()
			};
			return View(model);
		}

		[HttpPost]
		public IActionResult Checkout(string Name, string Phone, string Address)
		{
			var userId = _userManager.GetUserId(User);
			var cart = _context.Carts
				.Include(c => c.CartItems)
				.ThenInclude(p => p.Product)
				.FirstOrDefault(u => u.UserId == userId);
			if(cart == null)
			{
				return RedirectToAction("Index", "Cart");
			}
			var cartitems = cart.CartItems.ToList();
			decimal totalprice = 0;
			foreach(CartItem cartitem in cartitems)
			{
				totalprice += cartitem.Product.Price * cartitem.Quantity;
			}
			var order = new Order
			{
				UserId = userId,
				TotalPrice = totalprice,
				Name = Name,
				Phone = Phone,
				Address = Address,
				OrderDetails = new List<OrderDetail>()
			};
			foreach (CartItem item in cartitems)
			{
				order.OrderDetails.Add(new OrderDetail
				{
					ProductId = item.ProductId,
					Quantity = item.Quantity,
					Price = item.Quantity * item.Product.Price,
				});
			}
			_context.Orders.Add(order);
			_context.CartItems.RemoveRange(cart.CartItems);
			_context.SaveChanges();
			return RedirectToAction("Index", "Product");
		}
	}
}
