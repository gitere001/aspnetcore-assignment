using Microsoft.AspNetCore.Mvc;
using Queue_Management_System.Models;
using System.Diagnostics;
using Queue_Management_System.Data.Repositories;
using FastReport;
using FastReport.Export.Pdf;
using System.Drawing;

namespace Queue_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TicketRepository _ticketRepository;
        private readonly ServiceRepository _serviceRepository;

        public HomeController(ILogger<HomeController> logger, TicketRepository ticketRepository, ServiceRepository serviceRepository)
        {
            _logger = logger;
            _ticketRepository = ticketRepository;
            _serviceRepository = serviceRepository;
        }

        public IActionResult Index()
        {
            var services = _serviceRepository.GetAllServices().Result;
            return View(services);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        private async Task<string?> GeneratePrintablePDF(Ticket ticket, string serviceName)
        {
            try
            {


                var report = new Report();
                var page = new ReportPage();
                report.Pages.Add(page);

                var dataBand = new DataBand();
                dataBand.Height = 200;
                page.Bands.Add(dataBand);

                // 1. TITLE - Remove Center alignment
                var titleText = new TextObject
                {
                    Text = "HOSPITAL QUEUE TICKET",
                    Bounds = new RectangleF(20, 10, 200, 20),
                    Font = new Font("Arial", 14, FontStyle.Bold)
                    // REMOVED: HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(titleText);

                // 2. TICKET NUMBER - Remove Center alignment, use smaller font
                var ticketText = new TextObject
                {
                    Text = $"Ticket: {ticket.TicketNumber}",
                    Bounds = new RectangleF(20, 40, 200, 30),
                    Font = new Font("Arial", 20, FontStyle.Bold)
                    // REMOVED: HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(ticketText);

                // 3. SERVICE - Remove Center alignment
                var serviceText = new TextObject
                {
                    Text = $"Service: {serviceName}",
                    Bounds = new RectangleF(20, 80, 200, 15),
                    Font = new Font("Arial", 12)
                    // REMOVED: HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(serviceText);

                // 4. DATE - This one works, keep as is
                var dateText = new TextObject
                {
                    Text = $"Date: {DateTime.Now:yyyy-MM-dd HH:mm}",
                    Bounds = new RectangleF(20, 100, 200, 15),
                    Font = new Font("Arial", 10)
                    // REMOVED: HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(dateText);

                // 5. INSTRUCTION - This partially works, keep as is
                var instructionText = new TextObject
                {
                    Text = "Please wait to be called.",
                    Bounds = new RectangleF(20, 130, 200, 15),
                    Font = new Font("Arial", 9)
                    // REMOVED: HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(instructionText);

                report.Prepare();

                var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tickets");
                Directory.CreateDirectory(outputDir);

                var fileName = $"{ticket.TicketNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                var filePath = Path.Combine(outputDir, fileName);

                var pdfExport = new PDFExport();
                report.Export(pdfExport, filePath);

                _logger.LogInformation($"PDF generated at: {filePath}");
                return $"/tickets/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF");
                return null;
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateTicket(string serviceName, string prefix)
        {
            try

            {
                _logger.LogInformation($"GenerateTicket called - Service: {serviceName}, Prefix: {prefix}");
                var lastTicket = await _ticketRepository.GetLastTicketByPrefix(prefix);

                int nextNumber = 1;
                if (lastTicket != null)
                {
                    string numPart = lastTicket.TicketNumber.Substring(prefix.Length);
                    nextNumber = int.Parse(numPart) + 1;
                }
                string newTicketNumber = $"{prefix}{nextNumber:D3}";

                var service = await _serviceRepository.GetServiceByPrefix(prefix);
                if (service == null)
                {
                    return Json(new { success = false, message = "Service not found" });
                }
                var ticket = new Ticket
                {
                    TicketNumber = newTicketNumber,
                    ServiceId = service.Id,
                    Status = "Waiting"
                };

                int ticketId = await _ticketRepository.AddTicket(ticket);
                var pdfUrl = await GeneratePrintablePDF(ticket, serviceName);

                return Json(new
                {
                    success = true,
                    ticketNumber = newTicketNumber,
                    pdfUrl = pdfUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating ticket");
                return Json(new { success = false, message = "Failed to generate ticket" });
            }
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}