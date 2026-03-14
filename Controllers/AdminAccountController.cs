using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebLinhKienPc.ViewModels;

namespace WebLinhKienPc.Controllers
{
	public class AdminAccountController: Controller
	{
		private readonly UserManager<IdentityUser> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;

		public AdminAccountController(UserManager<IdentityUser> userManager,
							   RoleManager<IdentityRole> roleManager)
		{
			_userManager = userManager;
			_roleManager = roleManager;
		}

		public IActionResult Index()
		{
			var users = _userManager.Users.ToList();
			return View(users);
		}

		public IActionResult CreateAccount()
		{
			ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
			return View();
		}
		[HttpPost]
		public async Task<IActionResult> CreateUser(CreateUserViewModel model)
		{
			if (!ModelState.IsValid)
			{
				ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
				return View(model);
			}

			var user = new IdentityUser
			{
				UserName = model.Email,
				Email = model.Email
			};

			var result = await _userManager.CreateAsync(user, model.Password);

			if (result.Succeeded)
			{
				if (!string.IsNullOrEmpty(model.Role))
				{
					await _userManager.AddToRoleAsync(user, model.Role);
				}

				return RedirectToAction("Index");
			}

			foreach (var error in result.Errors)
			{
				ModelState.AddModelError("", error.Description);
			}

			ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();

			return View(model);
		}
		public async Task<IActionResult> DeleteAccount(string id)
		{
			var user = await _userManager.FindByIdAsync(id);

			if (user != null)
			{
				await _userManager.DeleteAsync(user);
			}

			return RedirectToAction("Index");
		}


		public IActionResult Roles()
		{
			var roles = _roleManager.Roles.ToList();
			return View(roles);
		}

		public IActionResult CreateRole()
		{
			return View();
		}

		[HttpPost]
		public async Task<IActionResult> CreateRole(string roleName)
		{
			if (!await _roleManager.RoleExistsAsync(roleName))
			{
				await _roleManager.CreateAsync(new IdentityRole(roleName));
			}

			return RedirectToAction("Roles");
		}

		public async Task<IActionResult> DeleteRole(string id)
		{
			var role = await _roleManager.FindByIdAsync(id);

			if (role != null)
			{
				await _roleManager.DeleteAsync(role);
			}

			return RedirectToAction("Roles");
		}

		public async Task<IActionResult> UsersByRole(string role)
		{
			var users = await _userManager.GetUsersInRoleAsync(role);

			ViewBag.Role = role;

			return View(users);
		}
	}
}
