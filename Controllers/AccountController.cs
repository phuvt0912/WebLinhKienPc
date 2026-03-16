using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
                await userManager.AddToRoleAsync(user, "KhachHang");
                await signInManager.SignInAsync(user, isPersistent: false);
                if (isAjax)
                    return Ok(new { redirectUrl = "/Home/Index" });
                return RedirectToAction("Index", "Home");
            }

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
                    return Ok(new { redirectUrl = "/Home/Index" });
                return RedirectToAction("Index", "Home");
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
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult LoginWithGoogle()
        {
            var redirectUrl = Url.Action("GoogleCallback", "Account");
            var properties = signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            properties.Parameters["prompt"] = "select_account";
            return Challenge(properties, "Google");
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback()
        {
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction("Login");

            var result = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey,
                isPersistent: false, bypassTwoFactor: true
            );

            if (result.Succeeded)
                return RedirectToAction("Index", "Home");

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new IdentityUser { UserName = email, Email = email };
                    await userManager.CreateAsync(user);
                    await userManager.AddToRoleAsync(user, "KhachHang");
                }
                await userManager.AddLoginAsync(user, info);
                await signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Login");
        }
    }
}