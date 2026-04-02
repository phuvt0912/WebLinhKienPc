using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.ViewModels;
using WebLinhKienPc.Models;
using System.Text.Json;
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

		public IActionResult Index()
		{
			var userId = _userManager.GetUserId(User);
			var orders = _context.Orders.Where(u => u.UserId == userId).ToList();
			return View(orders);
		}

		public IActionResult OrderDetails(int id)
		{
			var orders = _context.Orders
				.Include(od => od.OrderDetails)
				.ThenInclude(p => p.Product)
				.FirstOrDefault(order => order.OrderId == id);
			return View(orders);
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

            if (cart == null)
                return RedirectToAction("Index", "Cart");

            var cartItems = cart.CartItems.ToList();
            decimal totalPrice = 0;

            foreach (var item in cartItems)
                totalPrice += item.Product.Price * item.Quantity;

            var order = new Order
            {
                UserId = userId,
                OrderCode = GenerateOrderCode(),
                TotalPrice = totalPrice,
                Name = Name,
                Phone = Phone,
                Address = Address,
                OrderDetails = new List<OrderDetail>()
            };

            foreach (var item in cartItems)
            {
                order.OrderDetails.Add(new OrderDetail
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Product.Price,
                });

                // ===== TRỪ TỒN KHO =====
                item.Product.Stock -= item.Quantity;

                // Đảm bảo không âm
                if (item.Product.Stock < 0)
                    item.Product.Stock = 0;
            }

            _context.Orders.Add(order);
            _context.CartItems.RemoveRange(cart.CartItems);
            _context.SaveChanges(); // SaveChanges sẽ lưu cả thay đổi stock

            return RedirectToAction("Index", "Product");
        }

        public string GenerateOrderCode()
		{
			var random = new Random();
			return "DH" + DateTime.Now.ToString("yyyyMMdd") + random.Next(1000, 9999);
		}

		[HttpPost]
		public IActionResult PreCheckout(string Name, string Phone, string Address, string ShippingMethod, PaymentMethod PaymentMethod)
		{
			var userId = _userManager.GetUserId(User);
			var cart = _context.Carts
				.Include(c => c.CartItems)
				.ThenInclude(p => p.Product)
				.FirstOrDefault(u => u.UserId == userId);

			if (cart == null || !cart.CartItems.Any())
				return RedirectToAction("Index", "Cart");

			decimal shippingFee = CheckoutViewModel.GetShippingFee(ShippingMethod);
			decimal subTotal = cart.CartItems.Sum(i => i.Product.Price * i.Quantity);
			decimal totalPrice = subTotal + shippingFee;

			// Lưu session dạng string, parse lại dễ dàng
			var pending = new
			{
				Name,
				Phone,
				Address,
				ShippingMethod,
				PaymentMethod = PaymentMethod.ToString(),
				ShippingFee = shippingFee,
				SubTotal = subTotal,
				TotalPrice = totalPrice
			};
			HttpContext.Session.SetString("PendingOrder", JsonSerializer.Serialize(pending));

			if (PaymentMethod == PaymentMethod.CashOnDelivery)
			{
				// Thanh toán tiền mặt thì tạo đơn ngay
				return CreateOrderFromSession(userId);
			}

			// Hiển thị xác nhận thanh toán online
			ViewBag.Name = Name;
			ViewBag.Phone = Phone;
			ViewBag.Address = Address;
			ViewBag.ShippingMethod = ShippingMethod;
			ViewBag.ShippingLabel = CheckoutViewModel.GetShippingLabel(ShippingMethod);
			ViewBag.ShippingFee = shippingFee;
			ViewBag.SubTotal = subTotal;
			ViewBag.Total = totalPrice;

			return View("PaymentConfirm");
		}


		[HttpPost]
        public IActionResult ConfirmPayment()
        {
            var userId = _userManager.GetUserId(User);
            return CreateOrderFromSession(userId);
        }

		private IActionResult CreateOrderFromSession(string userId)
		{
			var pendingJson = HttpContext.Session.GetString("PendingOrder");

			if (string.IsNullOrEmpty(pendingJson))
				return RedirectToAction("Index", "Cart");

			var pending = JsonSerializer.Deserialize<JsonElement>(pendingJson);

			var cart = _context.Carts
				.Include(c => c.CartItems)
				.ThenInclude(p => p.Product)
				.FirstOrDefault(u => u.UserId == userId);

			if (cart == null) return RedirectToAction("Index", "Cart");

			// Lấy PaymentMethod từ session (luôn parse từ string)
			var paymentMethodStr = pending.GetProperty("PaymentMethod").GetString();
			var paymentMethod = Enum.Parse<PaymentMethod>(paymentMethodStr);

			var order = new Order
			{
				UserId = userId,
				OrderCode = GenerateOrderCode(),
				TotalPrice = pending.GetProperty("TotalPrice").GetDecimal(),
				Name = pending.GetProperty("Name").GetString(),
				Phone = pending.GetProperty("Phone").GetString(),
				Address = pending.GetProperty("Address").GetString(),
				PaymentMethod = paymentMethod,
				OrderDetails = new List<OrderDetail>()
			};

			foreach (var item in cart.CartItems)
			{
				order.OrderDetails.Add(new OrderDetail
				{
					ProductId = item.ProductId,
					Quantity = item.Quantity,
					Price = item.Product.Price
				});

				// Trừ tồn kho
				item.Product.Stock -= item.Quantity;
				if (item.Product.Stock < 0) item.Product.Stock = 0;
			}

			_context.Orders.Add(order);
			_context.CartItems.RemoveRange(cart.CartItems);
			_context.SaveChanges();

			HttpContext.Session.Remove("PendingOrder");
			TempData["OrderSuccess"] = order.OrderCode;

			return RedirectToAction("OrderDetails", "Order", new { id = order.OrderId });
		}

		// Action hủy - không đặt hàng
		[HttpPost]
        public IActionResult CancelPayment()
        {
            HttpContext.Session.Remove("PendingOrder");
            return RedirectToAction("Index", "Cart");
        }
    }
}
