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
                _logger.LogInformation("Generating printable PDF for ticket: {TicketNumber}", ticket.TicketNumber);
                _logger.LogInformation("Service Name: {ServiceName}", serviceName);
                var report = new Report();
                var page = new ReportPage();

                // Set page size for thermal printer (80mm width)
                page.PaperWidth = 226.77f;
                page.PaperHeight = 500;
                page.LeftMargin = 10;
                page.RightMargin = 10;
                page.TopMargin = 10;
                page.BottomMargin = 10;

                report.Pages.Add(page);

                var dataBand = new DataBand();
                dataBand.Height = 280; // Increased height
                page.Bands.Add(dataBand);

                // 1. HOSPITAL NAME
                var hospitalText = new TextObject
                {
                    Text = "CITY GENERAL HOSPITAL",
                    Bounds = new RectangleF(0, 5, 206.77f, 15),
                    Font = new Font("Arial", 10, FontStyle.Bold),
                    HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(hospitalText);

                // 2. TITLE
                var titleText = new TextObject
                {
                    Text = "QUEUE TICKET",
                    Bounds = new RectangleF(0, 25, 206.77f, 15),
                    Font = new Font("Arial", 11, FontStyle.Bold),
                    HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(titleText);

                // 3. SEPARATOR LINE - Using ShapeObject instead for smoother rendering
                var shape1 = new ShapeObject
                {
                    Bounds = new RectangleF(10, 45, 186.77f, 2),
                    Shape = ShapeKind.Rectangle,
                    Fill = new SolidFill(Color.Black)
                };
                dataBand.Objects.Add(shape1);

                // 4. TICKET NUMBER (Reduced from 32 to 28 for better balance)
                var ticketText = new TextObject
                {
                    Text = ticket.TicketNumber,
                    Bounds = new RectangleF(0, 55, 206.77f, 35),
                    Font = new Font("Arial", 24, FontStyle.Bold), // Reduced from 32
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center
                };
                dataBand.Objects.Add(ticketText);

                // 5. SERVICE NAME - INCREASED HEIGHT & WRAPPED TEXT
                var serviceText = new TextObject
                {
                    Text = serviceName.ToUpper(),
                    Bounds = new RectangleF(5, 95, 196.77f, 30), // Increased height to 30
                    Font = new Font("Arial", 11, FontStyle.Bold), // Reduced from 12
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    WordWrap = true // Enable word wrapping
                };
                dataBand.Objects.Add(serviceText);

                // 6. DATE AND TIME
                var dateText = new TextObject
                {
                    Text = DateTime.Now.ToString("dd-MMM-yyyy HH:mm"),
                    Bounds = new RectangleF(0, 130, 206.77f, 15),
                    Font = new Font("Arial", 9, FontStyle.Regular),
                    HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(dateText);

                // 7. SEPARATOR LINE 2
                var shape2 = new ShapeObject
                {
                    Bounds = new RectangleF(10, 150, 186.77f, 2),
                    Shape = ShapeKind.Rectangle,
                    Fill = new SolidFill(Color.Black)
                };
                dataBand.Objects.Add(shape2);

                // 8. INSTRUCTIONS - WRAPPED TEXT
                var instructionText = new TextObject
                {
                    Text = "PLEASE WAIT FOR YOUR\nNUMBER TO BE CALLED",
                    Bounds = new RectangleF(5, 160, 196.77f, 25),
                    Font = new Font("Arial", 9, FontStyle.Bold),
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    WordWrap = true
                };
                dataBand.Objects.Add(instructionText);

                // 9. SERVICE POINT
                var servicePointText = new TextObject
                {
                    Text = "Proceed to counter when called",
                    Bounds = new RectangleF(5, 190, 196.77f, 15),
                    Font = new Font("Arial", 8, FontStyle.Regular),
                    HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(servicePointText);

                // 10. THANK YOU MESSAGE
                var thankYouText = new TextObject
                {
                    Text = "Thank you for your patience",
                    Bounds = new RectangleF(5, 210, 196.77f, 15),
                    Font = new Font("Arial", 8, FontStyle.Italic),
                    HorzAlign = HorzAlign.Center
                };
                dataBand.Objects.Add(thankYouText);

                // 11. BOTTOM LINE
                var shape3 = new ShapeObject
                {
                    Bounds = new RectangleF(10, 230, 186.77f, 2),
                    Shape = ShapeKind.Rectangle,
                    Fill = new SolidFill(Color.Black)
                };
                dataBand.Objects.Add(shape3);

                // Add demo watermark
                var demoText = new TextObject
                {
                    Text = "DEMO VERSION",
                    Bounds = new RectangleF(0, 245, 206.77f, 12),
                    Font = new Font("Arial", 7, FontStyle.Regular),
                    HorzAlign = HorzAlign.Center,
                    TextColor = Color.Gray
                };
                dataBand.Objects.Add(demoText);

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