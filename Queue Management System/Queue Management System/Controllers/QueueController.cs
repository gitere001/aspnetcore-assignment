using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Queue_Management_System.Data.Repositories;

namespace Queue_Management_System.Controllers
{
    public class QueueController : Controller
    {
        private readonly TicketRepository _ticketRepository;
        private readonly ILogger<QueueController> _logger;

        public QueueController(TicketRepository ticketRepository, ILogger<QueueController> logger)
        {
            _ticketRepository = ticketRepository;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult CheckinPage()
        {
            return View();
        }



        [HttpGet]
        public IActionResult WaitingPage()
        {
            return View();
        }



        [Authorize, HttpGet]
        public IActionResult ServicePoint()
        {
            return View();
        }

        public IActionResult Waiting()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCalledTickets()
        {
            try
            {
                var tickets = await _ticketRepository.GetCalledTickets();
                return Json(new { tickets });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching called tickets");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsAnnounced([FromBody] MarkAsAnnouncedRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.TicketNumber))
                {
                    return BadRequest(new { error = "Ticket number is required" });
                }

                var success = await _ticketRepository.UpdateAnnouncementStatus(request.TicketNumber, false);

                return Json(new { success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking ticket as announced");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        public class MarkAsAnnouncedRequest
        {
            public string TicketNumber { get; set; } = string.Empty;
        }


    }
}
