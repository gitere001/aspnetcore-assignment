using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Queue_Management_System.Data.Repositories;

namespace Queue_Management_System.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly UserRepository _userRepository;
        private readonly ILogger<StaffController> _logger;
        private readonly TicketRepository _ticketRepository;
        public StaffController(UserRepository userRepository, ILogger<StaffController> logger, TicketRepository ticketRepository)
        {
            _userRepository = userRepository;
            _logger = logger;
            _ticketRepository = ticketRepository;
        }

        public async Task<IActionResult> Dashboard()
        {
            // Get current user ID from claims
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return RedirectToAction("Login", "Auth");
            }

            // Fetch staff with service point details
            var staff = await _userRepository.GetStaffWithServicePoint(userId);
            if (staff == null)
            {

                // Staff not found or not assigned
                return View("NotAssigned");
            }
            ViewBag.StaffName = staff.Username;
            ViewBag.ServicePointName = staff.ServicePointName;
            ViewBag.ServicePointId = staff.ServicePointId;
            ViewBag.ServicePointIsActive = staff.ServicePointIsActive;

            return View(staff);
        }

        [HttpGet]
        public async Task<IActionResult> GetQueue()
        {
            try
            {
                // Get user ID from claims
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new { error = "Not authenticated" });
                }

                // Fetch staff with service point details
                var staff = await _userRepository.GetUserById(userId);
                _logger.LogInformation("Fetching queue for staff ID: {UserId}", userId);
                _logger.LogInformation("Staff details: {@Staff}", staff);
                if (staff == null)
                {
                    return Json(new { error = "Staff not found or not assigned" });
                }

                if (!staff.ServicePointId.HasValue)
                {
                    return Json(new { error = "No service point assigned" });
                }

                var tickets = await _ticketRepository.GetTicketsByServicePoint(staff.ServicePointId.Value);
                return Json(new { tickets });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching queue");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
        public class CallNextRequest
        {
            public string? NoShowTicketNumber { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CallNext([FromBody] CallNextRequest request)
        {
            try
            {
                // Get user ID from claims
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new { error = "Not authenticated" });
                }

                // Fetch staff with service point details
                var staff = await _userRepository.GetUserById(userId);
                if (staff == null || staff.Role != "Staff")
                {
                    return BadRequest(new { error = "Staff not found" });
                }

                if (!staff.ServicePointId.HasValue)
                {
                    return BadRequest(new { error = "No service point assigned" });
                }

                // Call next ticket
                var result = await _ticketRepository.CallNextTicket(
                    staff.ServicePointId.Value,
                    userId,
                    request?.NoShowTicketNumber
                );

                if (result == null)
                {
                    return Json(new { success = false, message = "No tickets waiting" });
                }

                return Json(new
                {
                    success = true,
                    ticketNumber = result["ticketNumber"],
                    
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling next ticket");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

    }
}