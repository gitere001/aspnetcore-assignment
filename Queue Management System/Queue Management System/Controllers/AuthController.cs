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
                ViewBag.UsernameOrEmail = usernameOrEmail; // Preserve username
                TempData["ErrorMessage"] = "Invalid username/email or password";
                return View();
            }

            // 2. Verify password
            bool validPassword = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!validPassword)
            {
                ViewBag.UsernameOrEmail = usernameOrEmail; // Preserve username
                TempData["ErrorMessage"] = "Invalid username/email or password";
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


            if (user.Role == "Admin")
            {

                return RedirectToAction("Dashboard", "Admin");
            }
            else if (user.Role == "Staff") // ADD THIS
            {
                return RedirectToAction("Dashboard", "Staff"); // You need to create this
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("Cookies");
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string confirmPassword, int? servicePointId)
        {
            try
            {
                // Validation
                if (password != confirmPassword)
                {
                    return BadRequest("Passwords do not match");
                }

                // Check if user exists
                var existingUser = await _userRepository.GetUserByUsernameOrEmail(username);
                if (existingUser != null)
                {
                    return BadRequest("Username already exists");
                }

                // Hash password
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

                // Create user
                var newUser = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = hashedPassword,
                    Role = "Admin",
                    ServicePointId = servicePointId
                };

                // Save to database
                int userId = await _userRepository.AddUser(newUser);

                return Ok();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("users_email_key"))
                {
                    return BadRequest("Email already exists. Please use a different email address.");
                }
                else if (ex.Message.Contains("users_username_key"))
                {
                    return BadRequest("Username already exists. Please choose a different username.");
                }
                else
                {
                    return BadRequest($"Error adding admin: {ex.Message.Split(':')[0]}");
                }
            }


        }
    }
}
