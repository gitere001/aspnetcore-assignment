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
        private readonly ServicePointRepository _servicePointRepository;
        public StaffController(UserRepository userRepository, ILogger<StaffController> logger, TicketRepository ticketRepository, ServicePointRepository servicePointRepository)
        {
            _userRepository = userRepository;
            _logger = logger;
            _ticketRepository = ticketRepository;
            _servicePointRepository = servicePointRepository;
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

                var tickets = await _ticketRepository.GetTicketsByServicePoint(staff.ServicePointId.Value, userId);
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
        public async Task<IActionResult> CallNext()
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

                // Call next ticket - NOW TAKES ONLY 2 ARGUMENTS
                var result = await _ticketRepository.CallNextTicket(
                    staff.ServicePointId.Value,
                    userId
                );

                if (result == null)
                {
                    return Json(new { success = false, message = "No tickets waiting" });
                }

                return Json(new
                {
                    success = true,
                    ticketNumber = result["ticketNumber"]
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling next ticket");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAvailability()
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

                var servicePoint = await _servicePointRepository.GetServicePointById(staff.ServicePointId.Value);
                if (servicePoint == null)
                {
                    return BadRequest(new { error = "Service point not found" });
                }

                servicePoint.IsActive = !servicePoint.IsActive;

                await _servicePointRepository.UpdateServicePoint(servicePoint);
                return Json(new
                {
                    success = true,
                    isActive = servicePoint.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling availability");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTicketStatus([FromBody] UpdateTicketStatusRequest request)
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

                // Validate request
                if (string.IsNullOrEmpty(request.TicketNumber) || string.IsNullOrEmpty(request.NewStatus))
                {
                    return BadRequest(new { error = "Ticket number and status are required" });
                }

                // Validate status
                var validStatuses = new[] { "Serving", "Finished", "NoShow" };
                if (!validStatuses.Contains(request.NewStatus))
                {
                    return BadRequest(new { error = $"Invalid status. Must be: {string.Join(", ", validStatuses)}" });
                }

                // Update ticket status
                var success = await _ticketRepository.UpdateTicketStatus(
                    staff.ServicePointId.Value,
                    userId,
                    request.TicketNumber,
                    request.NewStatus
                );

                if (!success)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Failed to update ticket status. Ticket may not exist or status transition is invalid."
                    });
                }

                return Json(new
                {
                    success = true,
                    message = $"Ticket {request.TicketNumber} updated to {request.NewStatus}",
                    ticketNumber = request.TicketNumber,
                    newStatus = request.NewStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ticket status");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        public class UpdateTicketStatusRequest
        {
            public string TicketNumber { get; set; } = string.Empty;
            public string NewStatus { get; set; } = string.Empty;
        }

    }
}