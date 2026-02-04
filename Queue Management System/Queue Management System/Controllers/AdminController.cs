using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Queue_Management_System.Data.Repositories;
using Queue_Management_System.Models;

namespace Queue_Management_System.Controllers
{
    [Authorize(Roles = "Admin")] // Only admins can access
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly UserRepository _userRepository;
        private readonly ServiceRepository _serviceRepository;
        private readonly ServicePointRepository _servicePointRepository;

        public AdminController(UserRepository userRepository, ServiceRepository serviceRepository, ServicePointRepository servicePointRepository, ILogger<AdminController> logger)
        {
            _logger = logger;
            _userRepository = userRepository;
            _serviceRepository = serviceRepository;
            _servicePointRepository = servicePointRepository;
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

        public async Task<IActionResult> ServicePoints()
        {
            var servicePoints = await _servicePointRepository.GetAllServicePoints();
            return View(servicePoints);
        }

        public async Task<IActionResult> Services()
        {
            var services = await _serviceRepository.GetAllServices();
            return View(services);
        }
        public async Task<IActionResult> GetServicesForDropdown()
        {
            var services = await _serviceRepository.GetAllServices();
            return Json(services.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                prefix = s.PrefixCode
            }));
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


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddServicePoint(string name, int serviceId, bool isActive = true)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest("Service point name is required");
                }

                if (serviceId <= 0)
                {
                    return BadRequest("Please select a valid service");
                }

                // Check if service exists
                var service = await _serviceRepository.GetServiceById(serviceId);
                if (service == null)
                {
                    return BadRequest("Selected service does not exist");
                }

                // Optional: Check if service point with same name already exists
                var existingPoints = await _servicePointRepository.GetAllServicePoints();
                if (existingPoints.Any(sp => sp.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest($"Service point '{name}' already exists");
                }

                // Create service point
                var newServicePoint = new ServicePoint
                {
                    Name = name.Trim(),
                    ServiceId = serviceId,
                    IsActive = isActive
                };

                // Save to database
                int servicePointId = await _servicePointRepository.AddServicePoint(newServicePoint);

                return Ok(new
                {
                    id = servicePointId,
                    name = newServicePoint.Name,
                    serviceName = service.Name,
                    servicePrefix = service.PrefixCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding service point");
                return BadRequest($"Error adding service point: {ex.Message.Split(':')[0]}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetServicePointForEdit(int id)
        {
            var servicePoint = await _servicePointRepository.GetServicePointById(id);
            if (servicePoint == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = servicePoint.Id,
                name = servicePoint.Name,
                serviceId = servicePoint.ServiceId,
                isActive = servicePoint.IsActive,
                serviceName = servicePoint.ServiceName,
                servicePrefix = servicePoint.ServicePrefix
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditServicePoint(int id, string name, int serviceId, bool isActive)
        {
            try
            {
                _logger.LogInformation($"Editing service point ID: {id}, Name: {name}, ServiceId: {serviceId}, IsActive: {isActive}");
                // Validation
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest("Service point name is required");
                }

                if (serviceId <= 0)
                {
                    return BadRequest("Please select a valid service");
                }

                // Check if service point exists
                var existingPoint = await _servicePointRepository.GetServicePointById(id);
                if (existingPoint == null)
                {
                    return BadRequest("Service point not found");
                }

                // Check if service exists
                var service = await _serviceRepository.GetServiceById(serviceId);
                if (service == null)
                {
                    return BadRequest("Selected service does not exist");
                }

                // Update service point
                var updatedPoint = new ServicePoint
                {
                    Id = id,
                    Name = name.Trim(),
                    ServiceId = serviceId,
                    IsActive = isActive
                };

                await _servicePointRepository.UpdateServicePoint(updatedPoint);

                return Ok(new
                {
                    id = updatedPoint.Id,
                    name = updatedPoint.Name,
                    serviceName = service.Name,
                    servicePrefix = service.PrefixCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing service point");
                return BadRequest($"Error editing service point: {ex.Message.Split(':')[0]}");
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetServicePointsForAssignment()
        {
            var points = await _servicePointRepository.GetServicePointsForDropdown();
            _logger.LogInformation(
        "Fetched {Count} service points for assignment: {Points}",
        points.Count(),
        string.Join(", ",
            points.Select(p =>
                $"[{p.Id}] {p.Name} (Active={p.IsActive}, Assigned={p.IsAssigned})"
            )
        )
    );


            return Json(points);
        }




    }
}
