using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Queue_Management_System.Data.Repositories;
using Queue_Management_System.Models;

namespace Queue_Management_System.Controllers
{
    [Authorize(Roles = "Admin")] // Only admins can access
    public class AdminController : Controller
    {
        private readonly UserRepository _userRepository;

        public AdminController(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Reports()
        {
            return View();
        }

        public async Task<IActionResult> Users()
        {
            var users = await _userRepository.GetAllUsers(); // Fetch users from repository
            return View(users);
        }

        public IActionResult ServicePoints()
        {
            return View();
        }

        public IActionResult Services()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStaff(string username, string email, string password, string confirmPassword, int? servicePointId)
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
                    Role = "Staff",
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
                    return BadRequest($"Error adding staff: {ex.Message.Split(':')[0]}");
                }
            }
        }
    }
}
