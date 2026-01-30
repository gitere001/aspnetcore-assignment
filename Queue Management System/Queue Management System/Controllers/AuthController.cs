using Microsoft.AspNetCore.Mvc;
using Queue_Management_System.Data.Repositories;
using Queue_Management_System.Models;
using BCrypt.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Queue_Management_System.Controllers
{
	public class AuthController : Controller
	{
		private readonly UserRepository _userRepository;

		public AuthController(UserRepository userRepository)
		{
			_userRepository = userRepository;
		}

		// GET: /Auth/Login
		[HttpGet]
		public IActionResult Login()
		{
			return View();
		}

		// POST: /Auth/Login
		[HttpPost]
		public async Task<IActionResult> Login(string usernameOrEmail, string password)
		{
			// 1. Fetch user by username or email
			var user = await _userRepository.GetUserByUsernameOrEmail(usernameOrEmail);

			if (user == null)
			{
				ViewBag.Error = "Invalid username/email or password";
				return View();
			}

			// 2. Verify password
			bool validPassword = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
			if (!validPassword)
			{
				ViewBag.Error = "Invalid username/email or password";
				return View();
			}

			// 3. Create claims for the user
			var claims = new List<Claim>
	{
		new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
		new Claim(ClaimTypes.Name, user.Username),
		new Claim(ClaimTypes.Email, user.Email),
		new Claim(ClaimTypes.Role, user.Role)
	};

			// 4. Create identity and principal
			var identity = new ClaimsIdentity(claims, "Cookies");
			var principal = new ClaimsPrincipal(identity);

			// 5. Sign in user (creates authentication cookie)
			await HttpContext.SignInAsync("Cookies", principal);

			Console.WriteLine($"[DEBUG] Login successful! UserId={user.Id}, Role={user.Role}");

			if (user.Role == "Admin")
			{
				return RedirectToAction("Dashboard", "Admin");
			}
			else if (user.Role == "Staff") // ADD THIS
			{
				return RedirectToAction("ServicePoint", "Queue"); // You need to create this
			}
			else
			{
				return RedirectToAction("Index", "Home");
			}
		}

		[HttpPost]
		public async Task<IActionResult> Register(string username, string email, string password)
		{
			// 1. Check if user already exists
			var existingUser = await _userRepository.GetUserByUsernameOrEmail(username);
			if (existingUser != null)
			{
				ViewBag.Error = "Username already exists";
				return View();
			}

			// 2. Hash the password
			string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

			// 3. Create User object
			var newUser = new User
			{
				Username = username,
				Email = email,
				PasswordHash = hashedPassword,
				Role = "Admin" // or "User" depending on what you want
			};

			// 4. Add user to DB
			int userId = await _userRepository.AddUser(newUser);
			Console.WriteLine($"[DEBUG] New user registered with Id={userId}");



			return RedirectToAction("Index", "Home");
		}

		[HttpPost]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync("Cookies");
			return RedirectToAction("Login");
		}
	}
}
