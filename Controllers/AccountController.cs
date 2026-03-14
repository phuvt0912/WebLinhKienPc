using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebLinhKienPc.Models;

namespace WebLinhKienPc.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly SignInManager<IdentityUser> signInManager;

        public AccountController(UserManager<IdentityUser> userManager,
                                 SignInManager<IdentityUser> signInManager)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult Register() => View("Auth");

        [HttpGet]
        public IActionResult Login() => View("Auth");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Kiểm tra là AJAX request không
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (!ModelState.IsValid)
            {
                if (isAjax)
                    return BadRequest(new { message = "Thông tin không hợp lệ." });
                return View("Auth", model);
            }

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email
            };

            var result = await userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await signInManager.SignInAsync(user, isPersistent: false);
                if (isAjax)
                    return Ok(new { redirectUrl = "/Product/Index" });
                return RedirectToAction("Index", "Product");
            }

            // Lấy lỗi đầu tiên từ Identity (vd: mật khẩu yếu, email đã tồn tại)
            var errorMessage = result.Errors.FirstOrDefault()?.Description
                               ?? "Đăng ký thất bại. Vui lòng thử lại.";

            if (isAjax)
                return BadRequest(new { message = errorMessage });

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View("Auth", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (!ModelState.IsValid)
            {
                if (isAjax)
                    return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin." });
                return View("Auth", model);
            }

            var result = await signInManager.PasswordSignInAsync(
                model.Email, model.Password,
                isPersistent: false,
                lockoutOnFailure: false
            );

            if (result.Succeeded)
            {
                if (isAjax)
                    return Ok(new { redirectUrl = "/Product/Index" });
                return RedirectToAction("Index", "Product");
            }

            if (isAjax)
                return BadRequest(new { message = "Email hoặc mật khẩu không đúng." });

            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View("Auth", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Product");
        }
    }
}