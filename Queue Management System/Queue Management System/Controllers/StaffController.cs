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
        private readonly ServiceRepository _serviceRepository;
        public StaffController(UserRepository userRepository, ILogger<StaffController> logger, TicketRepository ticketRepository, ServicePointRepository servicePointRepository, ServiceRepository serviceRepository)
        {
            _userRepository = userRepository;
            _logger = logger;
            _ticketRepository = ticketRepository;
            _servicePointRepository = servicePointRepository;
            _serviceRepository = serviceRepository;
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

        public class SelectServicePointRequest
        {
            public int ServicePointId { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> SelectServicePoint([FromBody] SelectServicePointRequest request)
        {
            int servicePointId = request.ServicePointId;

            _logger.LogInformation("Selecting service point ID: {ServicePointId}", servicePointId);
            if (servicePointId <= 0)
            {
                return BadRequest(new { error = "Invalid service point ID" });
            }

            // Get current user ID
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { error = "Not authenticated" });
            }

            var user = await _userRepository.GetUserById(userId);
            if (user == null || user.Role != "Staff")
            {
                return BadRequest(new { error = "Staff not found" });
            }

            var servicePoint = await _servicePointRepository.GetServicePointById(servicePointId);
            if (servicePoint == null)
            {
                return BadRequest(new { error = "Service point not found" });
            }


            // Update user's service point assignment
            await _userRepository.UpdateUserServicePoint(userId, servicePointId);

            return Json(new
            {
                success = true,
                message = "Service point selected successfully",

            });
        }

        [HttpPost]
        public async Task<IActionResult> LeaveServicePoint()
        {
            try
            {
                // Get current user ID
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new { error = "Not authenticated" });
                }

                var user = await _userRepository.GetUserById(userId);
                if (user == null || user.Role != "Staff")
                {
                    return BadRequest(new { error = "Staff not found" });
                }

                // CHECK: User must have a service point assigned to leave
                if (!user.ServicePointId.HasValue)
                {
                    return BadRequest(new
                    {
                        error = "No service point assigned. Nothing to leave."
                    });
                }

                var currentServicePointId = user.ServicePointId.Value;
                _logger.LogInformation("Staff {UserId} leaving service point ID: {ServicePointId}",
                    userId, currentServicePointId);

                // CHECK 1: Get active tickets for staff
                var activeTickets = await _ticketRepository.GetActiveTicketsForStaff(userId);
                _logger.LogInformation("Active tickets for staff ID {UserId}: {@Tickets}", userId, activeTickets);

                // Check if any tickets are in 'Serving' status
                var servingTickets = activeTickets.Where(t => t.Status == "Serving").ToList();
                if (servingTickets.Count > 0)
                {
                    return BadRequest(new
                    {
                        error = "Cannot leave service point while serving customers. Finish or transfer tickets first.",
                        tickets = servingTickets.Select(t => t.TicketNumber).ToList()
                    });
                }

                // Check if any tickets are in 'Called' status
                var calledTickets = activeTickets.Where(t => t.Status == "Called").ToList();
                _logger.LogInformation("Called tickets for staff ID {UserId}: {@Tickets}", userId, calledTickets);

                if (calledTickets.Count > 0)
                {
                    // Auto-return called tickets to waiting status
                    foreach (var ticket in calledTickets)
                    {
                        await _ticketRepository.ReturnTicketToWaiting(
                            ticket.TicketNumber,
                            userId
                        );
                    }
                    _logger.LogInformation("Returned {Count} called tickets to waiting status", calledTickets.Count);
                }

                // Remove service point assignment (set to NULL)
                await _userRepository.UpdateUserServicePoint(userId, null);

                return Json(new
                {
                    success = true,
                    message = "Successfully left service point",
                    returnedTickets = calledTickets.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving service point");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetServicePointsForAssignment()
        {
            var points = await _servicePointRepository.GetServicePointsForDropdown();

            return Json(points);
        }

        public class TransferTicketRequest
        {
            public int ServicePointId { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> TransferTicket([FromBody] TransferTicketRequest request)
        {
            try
            {
                // Validate request
                if (request.ServicePointId <= 0)
                {
                    return BadRequest(new { error = "Invalid service point ID" });
                }

                // Get current user ID
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new { error = "Not authenticated" });
                }



                // Get current user
                var user = await _userRepository.GetUserById(userId);
                if (user == null || user.Role != "Staff")
                {
                    return BadRequest(new { error = "Staff not found" });
                }

                if (!user.ServicePointId.HasValue)
                {
                    return BadRequest(new { error = "No service point assigned" });
                }

                // Get destination service point
                var destinationSp = await _servicePointRepository.GetServicePointById(request.ServicePointId);
                if (destinationSp == null)
                {
                    return BadRequest(new { error = "Destination service point not found" });
                }

                // Get currently serving ticket for this staff
                var activeTickets = await _ticketRepository.GetActiveTicketsForStaff(userId);
                var servingTicket = activeTickets.FirstOrDefault(t => t.Status == "Serving");

                if (servingTicket == null)
                {
                    return BadRequest(new { error = "No ticket currently being served" });
                }

                // Transfer the ticket
                var success = await _ticketRepository.TransferTicket(
                servingTicket.TicketNumber,
                userId,
                destinationSp.ServiceId,
                destinationSp.Id
                );

                if (!success)
                {
                    return BadRequest(new { error = "Failed to transfer ticket" });
                }

                return Json(new
                {
                    success = true,
                    message = $"Ticket {servingTicket.TicketNumber} transferred to {destinationSp.Name}",
                    ticketNumber = servingTicket.TicketNumber,
                    destination = destinationSp.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring ticket");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }



    }
}