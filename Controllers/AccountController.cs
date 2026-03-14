using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebLinhKienPc;
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
        public IActionResult Register()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Tạo một đối tượng user kiểu IdentityUser, sao chép dữ liệu từ RegisterViewModel cho user.
                // Giá trị Username cũng là giá trị Email nhập trên form.
                var user = new IdentityUser
                {
                    UserName = model.Email,
                    Email = model.Email
                };

                // Lưu dữ liệu vào bảng AspNetUsers
                var result = await userManager.CreateAsync(user, model.Password);

                // Nếu lưu thành công thì chuyển đến trang chủ hiển thị danh sách sản phẩm
                if (result.Succeeded)
                {
                    await signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("index", "product");
                }

                // Nếu có lỗi từ Identity (vd: mật khẩu yếu), thêm lỗi vào ModelState
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        //[HttpPost]
        //public async Task<IActionResult> Login(LoginViewModel model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        // Kiểm tra thông tin đăng nhập
        //        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: false);
        //        if (result.Succeeded)
        //        {
        //            return RedirectToAction("index", "product");
        //        }
        //        else
        //        {
        //            ModelState.AddModelError("", "Thông tin đăng nhập không chính xác.");
        //        }
        //    }
        //    return View(model);
        //}

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Product");
        }
    }
}