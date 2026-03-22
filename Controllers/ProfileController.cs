using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLinhKienPc.AppDbContext;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProfileController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext context,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _env = env;
        }

        // ===== GET: Trang profile =====
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            // Tạo profile nếu chưa có
            if (profile == null)
            {
                profile = new UserProfile { UserId = user.Id };
                _context.UserProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            ViewBag.Profile = profile;
            return View(user);
        }

        // ===== POST: Cập nhật thông tin =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInfo(string username, string email)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin.";
                return RedirectToAction("Index");
            }

            user.UserName = username.Trim();
            user.Email = email.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Cập nhật thông tin thành công!";
            }
            else
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Index");
        }

        // ===== POST: Đổi mật khẩu =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            string currentPassword,
            string newPassword,
            string confirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(currentPassword) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                TempData["PwdError"] = "Vui lòng điền đầy đủ thông tin.";
                return RedirectToAction("Index");
            }

            if (newPassword != confirmPassword)
            {
                TempData["PwdError"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("Index");
            }

            if (newPassword.Length < 6)
            {
                TempData["PwdError"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return RedirectToAction("Index");
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["PwdSuccess"] = "Đổi mật khẩu thành công!";
            }
            else
            {
                TempData["PwdError"] = result.Errors.Any(e => e.Code == "PasswordMismatch")
                    ? "Mật khẩu hiện tại không đúng."
                    : string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Index");
        }

        // ===== POST: Upload avatar =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatar)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (avatar == null || avatar.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn ảnh.";
                return RedirectToAction("Index");
            }

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(avatar.FileName).ToLower();
            if (!allowed.Contains(ext))
            {
                TempData["Error"] = "Chỉ hỗ trợ JPG, PNG, GIF, WEBP.";
                return RedirectToAction("Index");
            }

            if (avatar.Length > 2 * 1024 * 1024)
            {
                TempData["Error"] = "Ảnh không được vượt quá 2MB.";
                return RedirectToAction("Index");
            }

            // Lấy hoặc tạo profile
            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                profile = new UserProfile { UserId = user.Id };
                _context.UserProfiles.Add(profile);
            }

            // Xóa avatar cũ
            if (!string.IsNullOrEmpty(profile.AvatarUrl))
            {
                var oldPath = Path.Combine(_env.WebRootPath, profile.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            // Lưu file mới
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{user.Id}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await avatar.CopyToAsync(stream);

            profile.AvatarUrl = $"/uploads/avatars/{fileName}";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật ảnh đại diện thành công!";
            return RedirectToAction("Index");
        }

        // ===== POST: Xóa avatar =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAvatar()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (profile != null && !string.IsNullOrEmpty(profile.AvatarUrl))
            {
                var oldPath = Path.Combine(_env.WebRootPath, profile.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);

                profile.AvatarUrl = null;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Đã xóa ảnh đại diện.";
            return RedirectToAction("Index");
        }
    }
}