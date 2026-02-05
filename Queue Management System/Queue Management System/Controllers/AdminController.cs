using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Queue_Management_System.Data.Repositories;
using Queue_Management_System.Models;

using FastReport;
using FastReport.Export.Pdf;
using System.Drawing;

namespace Queue_Management_System.Controllers
{
    [Authorize(Roles = "Admin")] // Only admins can access
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly UserRepository _userRepository;
        private readonly ServiceRepository _serviceRepository;
        private readonly ServicePointRepository _servicePointRepository;
        private readonly DashboardRepository _dashboardRepository;

        public AdminController(UserRepository userRepository, ServiceRepository serviceRepository, ServicePointRepository servicePointRepository, DashboardRepository dashboardRepository, ILogger<AdminController> logger)
        {
            _logger = logger;
            _userRepository = userRepository;
            _serviceRepository = serviceRepository;
            _servicePointRepository = servicePointRepository;
            _dashboardRepository = dashboardRepository;
        }

        public async Task<IActionResult> Dashboard()
        {
            var stats = await _dashboardRepository.GetDashboardStats(DateTime.UtcNow);
            return View(stats);
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
            return Json(points);
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats([FromQuery] DateTime? date = null)
        {
            try
            {
                var targetDate = date ?? DateTime.UtcNow;
                var stats = await _dashboardRepository.GetDashboardStats(targetDate);

                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard stats");
                return StatusCode(500, new { error = "Failed to fetch dashboard statistics" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReport([FromBody] ReportRequestDto request)
        {
            try
            {
                if (request == null || request.ReportData == null)
                {
                    return BadRequest("No report data provided");
                }

                var reportData = request.ReportData;
                var startDate = request.StartDate;
                var endDate = request.EndDate;

                _logger.LogInformation($"Generating PDF report from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                // Create FastReport
                var report = new FastReport.Report();
                var page = new FastReport.ReportPage();

                // A4 Page Settings
                page.PaperWidth = 210; // mm
                page.PaperHeight = 297; // mm
                page.LeftMargin = 20;
                page.RightMargin = 20;
                page.TopMargin = 20;
                page.BottomMargin = 20;

                report.Pages.Add(page);

                // ========== HEADER SECTION ==========
                var headerBand = new FastReport.ReportTitleBand();
                headerBand.Height = 40;
                page.Bands.Add(headerBand);

                // Main Title
                var mainTitle = new FastReport.TextObject
                {
                    Text = "QUEUE MANAGEMENT SYSTEM",
                    Bounds = new System.Drawing.RectangleF(0, 5, 170, 15),
                    Font = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Center,
                    TextColor = System.Drawing.Color.Navy
                };
                headerBand.Objects.Add(mainTitle);

                // Subtitle
                var subtitle = new FastReport.TextObject
                {
                    Text = "ANALYTICAL REPORT",
                    Bounds = new System.Drawing.RectangleF(0, 22, 170, 12),
                    Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Center,
                    TextColor = System.Drawing.Color.Navy
                };
                headerBand.Objects.Add(subtitle);

                // Date Range
                var dateText = new FastReport.TextObject
                {
                    Text = $"Period: {startDate:MMMM d, yyyy} - {endDate:MMMM d, yyyy}",
                    Bounds = new System.Drawing.RectangleF(0, 35, 170, 10),
                    Font = new System.Drawing.Font("Arial", 9),
                    HorzAlign = FastReport.HorzAlign.Center
                };
                headerBand.Objects.Add(dateText);

                // ========== SUMMARY SECTION ==========
                var summaryBand = new FastReport.DataBand();
                summaryBand.Height = 60;
                page.Bands.Add(summaryBand);

                // Summary Title
                var summaryTitle = new FastReport.TextObject
                {
                    Text = "OVERALL SUMMARY",
                    Bounds = new System.Drawing.RectangleF(0, 5, 170, 12),
                    Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Left,
                    TextColor = System.Drawing.Color.DarkSlateGray
                };
                summaryBand.Objects.Add(summaryTitle);

                // Simple line using ShapeObject (like in HomeController)
                var line1 = new FastReport.ShapeObject
                {
                    Bounds = new System.Drawing.RectangleF(0, 20, 170, 1),
                    Shape = FastReport.ShapeKind.Rectangle,
                    Fill = new FastReport.SolidFill(System.Drawing.Color.LightGray)
                };
                summaryBand.Objects.Add(line1);

                // Summary Content
                var summaryContent = new FastReport.TextObject
                {
                    Text = $"Total Customers Served: {reportData.TotalCustomersServed}\n" +
                           $"Average Wait Time: {reportData.OverallAverageWaitTimeMinutes:F1} minutes\n" +
                           $"Average Service Time: {reportData.OverallAverageServiceTimeMinutes:F1} minutes",
                    Bounds = new System.Drawing.RectangleF(5, 25, 160, 30),
                    Font = new System.Drawing.Font("Arial", 9),
                    HorzAlign = FastReport.HorzAlign.Left
                };
                summaryBand.Objects.Add(summaryContent);

                // ========== SERVICE POINT PERFORMANCE ==========
                if (reportData.ServicePointPerformance?.Any() == true)
                {
                    // Section Header
                    var spHeaderBand = new FastReport.DataBand();
                    spHeaderBand.Height = 25;
                    page.Bands.Add(spHeaderBand);

                    var spHeader = new FastReport.TextObject
                    {
                        Text = "SERVICE POINT PERFORMANCE",
                        Bounds = new System.Drawing.RectangleF(0, 5, 170, 12),
                        Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left,
                        TextColor = System.Drawing.Color.DarkSlateGray
                    };
                    spHeaderBand.Objects.Add(spHeader);

                    // Create table for service points
                    float spTableHeight = reportData.ServicePointPerformance.Count * 25 + 30;
                    var spTableBand = new FastReport.DataBand();
                    spTableBand.Height = spTableHeight;
                    page.Bands.Add(spTableBand);

                    // Table Header Row
                    var headerRowBox = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(0, 5, 170, 25),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(240, 240, 240))
                    };
                    spTableBand.Objects.Add(headerRowBox);

                    // Column Headers
                    var spColumnHeaders = new FastReport.TextObject
                    {
                        Text = "Service Point               Served   Wait(min)   Service(min)",
                        Bounds = new System.Drawing.RectangleF(5, 10, 160, 10),
                        Font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left
                    };
                    spTableBand.Objects.Add(spColumnHeaders);

                    // Data Rows
                    float rowY = 35;
                    bool alternate = false;

                    foreach (var sp in reportData.ServicePointPerformance)
                    {
                        // Alternate row colors
                        var rowColor = alternate ?
                            System.Drawing.Color.White :
                            System.Drawing.Color.FromArgb(250, 250, 250);
                        alternate = !alternate;

                        // Row box
                        var rowBox = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(0, rowY, 170, 25),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(rowColor)
                        };
                        spTableBand.Objects.Add(rowBox);

                        // Row content
                        var rowText = new FastReport.TextObject
                        {
                            Text = $"{sp.ServicePointName,-25} {sp.CustomersServed,7}    {sp.AverageWaitTimeMinutes,8:F1}      {sp.AverageServiceTimeMinutes,8:F1}",
                            Bounds = new System.Drawing.RectangleF(5, rowY + 8, 160, 10),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Left
                        };
                        spTableBand.Objects.Add(rowText);

                        // Bottom border line for row
                        var rowLine = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(0, rowY + 24, 170, 1),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.LightGray)
                        };
                        spTableBand.Objects.Add(rowLine);

                        rowY += 25;
                    }
                }

                // ========== STAFF PERFORMANCE ==========
                if (reportData.StaffPerformance?.Any() == true)
                {
                    // Section Header
                    var staffHeaderBand = new FastReport.DataBand();
                    staffHeaderBand.Height = 25;
                    page.Bands.Add(staffHeaderBand);

                    var staffHeader = new FastReport.TextObject
                    {
                        Text = "STAFF PERFORMANCE",
                        Bounds = new System.Drawing.RectangleF(0, 5, 170, 12),
                        Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left,
                        TextColor = System.Drawing.Color.DarkSlateGray
                    };
                    staffHeaderBand.Objects.Add(staffHeader);

                    // Create table for staff
                    float staffTableHeight = reportData.StaffPerformance.Count * 25 + 30;
                    var staffTableBand = new FastReport.DataBand();
                    staffTableBand.Height = staffTableHeight;
                    page.Bands.Add(staffTableBand);

                    // Table Header Row
                    var staffHeaderRowBox = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(0, 5, 170, 25),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(240, 240, 240))
                    };
                    staffTableBand.Objects.Add(staffHeaderRowBox);

                    // Column Headers
                    var staffColumnHeaders = new FastReport.TextObject
                    {
                        Text = "Staff Member               Served   Wait(min)   Service(min)",
                        Bounds = new System.Drawing.RectangleF(5, 10, 160, 10),
                        Font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left
                    };
                    staffTableBand.Objects.Add(staffColumnHeaders);

                    // Data Rows
                    float rowY = 35;
                    bool alternate = false;

                    foreach (var staff in reportData.StaffPerformance)
                    {
                        // Alternate row colors
                        var rowColor = alternate ?
                            System.Drawing.Color.White :
                            System.Drawing.Color.FromArgb(250, 250, 250);
                        alternate = !alternate;

                        // Row box
                        var rowBox = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(0, rowY, 170, 25),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(rowColor)
                        };
                        staffTableBand.Objects.Add(rowBox);

                        // Row content
                        var rowText = new FastReport.TextObject
                        {
                            Text = $"{staff.StaffName,-25} {staff.CustomersServed,7}    {staff.AverageWaitTimeMinutes,8:F1}      {staff.AverageServiceTimeMinutes,8:F1}",
                            Bounds = new System.Drawing.RectangleF(5, rowY + 8, 160, 10),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Left
                        };
                        staffTableBand.Objects.Add(rowText);

                        // Bottom border line for row
                        var rowLine = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(0, rowY + 24, 170, 1),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.LightGray)
                        };
                        staffTableBand.Objects.Add(rowLine);

                        rowY += 25;
                    }
                }

                // Footer
                var footerBand = new FastReport.PageFooterBand();
                footerBand.Height = 20;
                page.Bands.Add(footerBand);

                var footerText = new FastReport.TextObject
                {
                    Text = $"Generated on {DateTime.Now:MMMM d, yyyy h:mm tt} | Page [Page#]",
                    Bounds = new System.Drawing.RectangleF(0, 5, 170, 10),
                    Font = new System.Drawing.Font("Arial", 7),
                    HorzAlign = FastReport.HorzAlign.Center,
                    TextColor = System.Drawing.Color.Gray
                };
                footerBand.Objects.Add(footerText);

                // Prepare and export PDF
                report.Prepare();

                using (var ms = new System.IO.MemoryStream())
                {
                    var pdfExport = new FastReport.Export.Pdf.PDFExport();
                    report.Export(pdfExport, ms);
                    ms.Position = 0;

                    var fileName = $"Queue_Analytics_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
                    return File(ms.ToArray(), "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF report");
                return StatusCode(500, $"Failed to generate report: {ex.Message}");
            }
        }

        // DTOs for the request
        public class ReportRequestDto
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public AnalyticalReportDto ReportData { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetAnalyticalReportData(DateTime startDate, DateTime endDate)
        {
            try
            {
                if (startDate == default || startDate.Year <= 1)
                {
                    endDate = DateTime.Today;
                    startDate = DateTime.Today.AddDays(-7);
                }
                _logger.LogInformation($"Fetching report data: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                var reportData = await _dashboardRepository.GetAnalyticalReportData(startDate, endDate);

                return Json(reportData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching report data");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
