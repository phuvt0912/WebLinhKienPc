using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;
using ClosedXML.Excel;
using System.IO;
using System;

namespace WebLinhKienPc.Controllers
{
    [Authorize(Roles = "Admin,NhanVien")]
    public class AdminOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminOrderController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            var orders = _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .ToList();
            return View(orders);
        }

        public IActionResult OrderDetails(int id)
        {
            var order = _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefault(o => o.OrderId == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // ===== HỦY ĐƠN =====
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index");
            }

            if (order.Status == OrderStatus.Completed)
            {
                TempData["Error"] = "Không thể hủy đơn đã hoàn thành.";
                return RedirectToAction("OrderDetails", new { id });
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                TempData["Error"] = "Đơn hàng này đã bị hủy trước đó.";
                return RedirectToAction("OrderDetails", new { id });
            }

            // DEBUG - xem OrderDetails có load được không
            Console.WriteLine($"=== CancelOrder #{order.OrderCode} ===");
            Console.WriteLine($"OrderDetails count: {order.OrderDetails?.Count ?? 0}");

            foreach (var detail in order.OrderDetails ?? new List<OrderDetail>())
            {
                Console.WriteLine($"  ProductId: {detail.ProductId} | Product null: {detail.Product == null} | Qty: {detail.Quantity}");

                if (detail.Product != null)
                {
                    Console.WriteLine($"  Stock trước: {detail.Product.Stock}");
                    detail.Product.Stock += detail.Quantity;
                    Console.WriteLine($"  Stock sau: {detail.Product.Stock}");
                }
                else
                {
                    // Product null → load thủ công
                    var product = await _context.Products.FindAsync(detail.ProductId);
                    if (product != null)
                    {
                        Console.WriteLine($"  Load thủ công - Stock trước: {product.Stock}");
                        product.Stock += detail.Quantity;
                        Console.WriteLine($"  Load thủ công - Stock sau: {product.Stock}");
                    }
                }
            }

            order.Status = OrderStatus.Cancelled;
            var saved = await _context.SaveChangesAsync();
            Console.WriteLine($"SaveChanges result: {saved} rows affected");

            TempData["Success"] = $"Đã hủy đơn hàng #{order.OrderCode} thành công.";
            return RedirectToAction("OrderDetails", new { id });
        }

		// Action này cho AJAX từ Index page (dropdown)
		[Authorize(Roles = "Admin,NhanVien")]
		[HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatusAjax([FromBody] UpdateStatusModel model)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == model.OrderId);

                if (order == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

                if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
                    return Json(new { success = false, message = "Không thể cập nhật đơn đã hoàn thành hoặc đã huỷ" });

                var newStatus = Enum.Parse<OrderStatus>(model.Status);

                // ===== NẾU HỦY → HOÀN TRẢ STOCK =====
                if (newStatus == OrderStatus.Cancelled)
                {
                    foreach (var detail in order.OrderDetails)
                    {
                        var product = detail.Product
                            ?? await _context.Products.FindAsync(detail.ProductId);

                        if (product != null)
                            product.Stock += detail.Quantity;
                    }
                }

                order.Status = newStatus;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Đã cập nhật trạng thái đơn #{order.OrderCode}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdateStatus([FromBody] BulkUpdateModel model)
        {
            try
            {
                var orders = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .Where(o => model.OrderIds.Contains(o.OrderId))
                    .ToListAsync();

                var newStatus = Enum.Parse<OrderStatus>(model.Status);
                int updatedCount = 0;

                foreach (var order in orders)
                {
                    if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
                        continue;

                    // ===== NẾU HỦY → HOÀN TRẢ STOCK =====
                    if (newStatus == OrderStatus.Cancelled)
                    {
                        foreach (var detail in order.OrderDetails)
                        {
                            var product = detail.Product
                                ?? await _context.Products.FindAsync(detail.ProductId);

                            if (product != null)
                                product.Stock += detail.Quantity;
                        }
                    }

                    order.Status = newStatus;
                    updatedCount++;
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Đã cập nhật {updatedCount}/{orders.Count} đơn hàng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi: " + ex.Message });
            }
        }

        public async Task<IActionResult> ExportExcel()
        {
            var orders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Tạo file Excel
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Đơn hàng");

                // Header
                worksheet.Cell(1, 1).Value = "Mã đơn hàng";
                worksheet.Cell(1, 2).Value = "Khách hàng";
                worksheet.Cell(1, 3).Value = "Số điện thoại";
                worksheet.Cell(1, 4).Value = "Địa chỉ";
                worksheet.Cell(1, 5).Value = "Tổng tiền";
                worksheet.Cell(1, 6).Value = "Trạng thái";
                worksheet.Cell(1, 7).Value = "Ngày đặt";

                // Data
                int row = 2;
                foreach (var order in orders)
                {
                    worksheet.Cell(row, 1).Value = order.OrderCode;
                    worksheet.Cell(row, 2).Value = order.Name;
                    worksheet.Cell(row, 3).Value = order.Phone ?? "";
                    worksheet.Cell(row, 4).Value = order.Address ?? "";
                    worksheet.Cell(row, 5).Value = order.TotalPrice;
                    worksheet.Cell(row, 6).Value = order.Status.ToString();
                    worksheet.Cell(row, 7).Value = order.OrderDate.ToString("dd/MM/yyyy HH:mm");
                    row++;
                }

                // Format
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"DonHang_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                }
            }
        }

        public class UpdateStatusModel
        {
            public int OrderId { get; set; }
            public string Status { get; set; }
        }

        public class BulkUpdateModel
        {
            public List<int> OrderIds { get; set; }
            public string Status { get; set; }
        }
    }
}