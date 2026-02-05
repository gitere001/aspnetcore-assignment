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
                page.LeftMargin = 15;
                page.RightMargin = 15;
                page.TopMargin = 15;
                page.BottomMargin = 15;

                report.Pages.Add(page);

                // Available width: 210 - 30 = 180mm
                float pageWidth = 180f;

                // ========== HEADER SECTION ==========
                var headerBand = new FastReport.ReportTitleBand();
                headerBand.Height = 70;
                page.Bands.Add(headerBand);

                // Header background
                var headerBox = new FastReport.ShapeObject
                {
                    Bounds = new System.Drawing.RectangleF(0, 0, pageWidth, 65),
                    Shape = FastReport.ShapeKind.Rectangle,
                    Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
                };
                headerBand.Objects.Add(headerBox);

                // Main Title
                var mainTitle = new FastReport.TextObject
                {
                    Text = "QUEUE MANAGEMENT SYSTEM",
                    Bounds = new System.Drawing.RectangleF(0, 10, pageWidth, 12),
                    Font = new System.Drawing.Font("Arial", 16, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.White
                };
                headerBand.Objects.Add(mainTitle);

                // Subtitle
                var subtitle = new FastReport.TextObject
                {
                    Text = "ANALYTICAL REPORT",
                    Bounds = new System.Drawing.RectangleF(0, 26, pageWidth, 10),
                    Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.White
                };
                headerBand.Objects.Add(subtitle);

                // Date Range
                var dateText = new FastReport.TextObject
                {
                    Text = $"Period: {startDate:MMMM d, yyyy} - {endDate:MMMM d, yyyy}",
                    Bounds = new System.Drawing.RectangleF(0, 40, pageWidth, 8),
                    Font = new System.Drawing.Font("Arial", 9),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.White
                };
                headerBand.Objects.Add(dateText);

                // Generation timestamp
                var timestampText = new FastReport.TextObject
                {
                    Text = $"Generated: {DateTime.Now:MMMM d, yyyy h:mm tt}",
                    Bounds = new System.Drawing.RectangleF(0, 52, pageWidth, 7),
                    Font = new System.Drawing.Font("Arial", 7),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(230, 240, 255)
                };
                headerBand.Objects.Add(timestampText);

                // ========== SUMMARY SECTION ==========
                var summaryBand = new FastReport.DataBand();
                summaryBand.Height = 85;
                page.Bands.Add(summaryBand);

                // Summary Title
                var summaryTitle = new FastReport.TextObject
                {
                    Text = "OVERALL SUMMARY",
                    Bounds = new System.Drawing.RectangleF(0, 8, pageWidth, 10),
                    Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Left,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(41, 98, 255)
                };
                summaryBand.Objects.Add(summaryTitle);

                // Decorative line
                var summaryLine = new FastReport.ShapeObject
                {
                    Bounds = new System.Drawing.RectangleF(0, 20, 60, 1.5f),
                    Shape = FastReport.ShapeKind.Rectangle,
                    Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
                };
                summaryBand.Objects.Add(summaryLine);

                // Summary boxes - 3 columns with 5mm gaps
                float boxWidth = 56.67f; // (180 - 10) / 3
                float boxHeight = 50f;
                float boxY = 28f;
                float gap = 5f;

                // Box 1: Total Customers
                var box1 = new FastReport.ShapeObject
                {
                    Bounds = new System.Drawing.RectangleF(0, boxY, boxWidth, boxHeight),
                    Shape = FastReport.ShapeKind.RoundRectangle,
                    Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(248, 249, 250)),
                    Border = new FastReport.Border
                    {
                        Lines = FastReport.BorderLines.All,
                        Color = System.Drawing.Color.FromArgb(222, 226, 230),
                        Width = 1f
                    }
                };
                summaryBand.Objects.Add(box1);

                var box1Value = new FastReport.TextObject
                {
                    Text = reportData.TotalCustomersServed.ToString(),
                    Bounds = new System.Drawing.RectangleF(0, boxY + 10, boxWidth, 16),
                    Font = new System.Drawing.Font("Arial", 20, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(41, 98, 255)
                };
                summaryBand.Objects.Add(box1Value);

                var box1Label = new FastReport.TextObject
                {
                    Text = "Total Customers",
                    Bounds = new System.Drawing.RectangleF(0, boxY + 32, boxWidth, 8),
                    Font = new System.Drawing.Font("Arial", 8),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(108, 117, 125)
                };
                summaryBand.Objects.Add(box1Label);

                // Box 2: Avg Wait Time
                float box2X = boxWidth + gap;
                var box2 = new FastReport.ShapeObject
                {
                    Bounds = new System.Drawing.RectangleF(box2X, boxY, boxWidth, boxHeight),
                    Shape = FastReport.ShapeKind.RoundRectangle,
                    Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(248, 249, 250)),
                    Border = new FastReport.Border
                    {
                        Lines = FastReport.BorderLines.All,
                        Color = System.Drawing.Color.FromArgb(222, 226, 230),
                        Width = 1f
                    }
                };
                summaryBand.Objects.Add(box2);

                var box2Value = new FastReport.TextObject
                {
                    Text = $"{reportData.OverallAverageWaitTimeMinutes:F1}",
                    Bounds = new System.Drawing.RectangleF(box2X, boxY + 10, boxWidth, 16),
                    Font = new System.Drawing.Font("Arial", 20, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(23, 162, 184)
                };
                summaryBand.Objects.Add(box2Value);

                var box2Label = new FastReport.TextObject
                {
                    Text = "Avg Wait Time (min)",
                    Bounds = new System.Drawing.RectangleF(box2X, boxY + 32, boxWidth, 8),
                    Font = new System.Drawing.Font("Arial", 8),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(108, 117, 125)
                };
                summaryBand.Objects.Add(box2Label);

                // Box 3: Avg Service Time
                float box3X = (boxWidth + gap) * 2;
                var box3 = new FastReport.ShapeObject
                {
                    Bounds = new System.Drawing.RectangleF(box3X, boxY, boxWidth, boxHeight),
                    Shape = FastReport.ShapeKind.RoundRectangle,
                    Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(248, 249, 250)),
                    Border = new FastReport.Border
                    {
                        Lines = FastReport.BorderLines.All,
                        Color = System.Drawing.Color.FromArgb(222, 226, 230),
                        Width = 1f
                    }
                };
                summaryBand.Objects.Add(box3);

                var box3Value = new FastReport.TextObject
                {
                    Text = $"{reportData.OverallAverageServiceTimeMinutes:F1}",
                    Bounds = new System.Drawing.RectangleF(box3X, boxY + 10, boxWidth, 16),
                    Font = new System.Drawing.Font("Arial", 20, System.Drawing.FontStyle.Bold),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(40, 167, 69)
                };
                summaryBand.Objects.Add(box3Value);

                var box3Label = new FastReport.TextObject
                {
                    Text = "Avg Service Time (min)",
                    Bounds = new System.Drawing.RectangleF(box3X, boxY + 32, boxWidth, 8),
                    Font = new System.Drawing.Font("Arial", 8),
                    HorzAlign = FastReport.HorzAlign.Center,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(108, 117, 125)
                };
                summaryBand.Objects.Add(box3Label);

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
                        Bounds = new System.Drawing.RectangleF(0, 5, pageWidth, 10),
                        Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.FromArgb(41, 98, 255)
                    };
                    spHeaderBand.Objects.Add(spHeader);

                    var spLine = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(0, 17, 80, 1.5f),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
                    };
                    spHeaderBand.Objects.Add(spLine);

                    // Service Point Table
                    float rowHeight = 18f;
                    int rowCount = reportData.ServicePointPerformance.Count;
                    float tableHeight = (rowCount + 1) * rowHeight + 15;

                    var spTableBand = new FastReport.DataBand();
                    spTableBand.Height = tableHeight;
                    page.Bands.Add(spTableBand);

                    // Column widths - exact measurements
                    float col1 = 85f;  // Service Point
                    float col2 = 30f;  // Served
                    float col3 = 32f;  // Wait
                    float col4 = 33f;  // Service
                    float totalWidth = col1 + col2 + col3 + col4;

                    // Table border
                    var tableBorder = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(0, 5, totalWidth, (rowCount + 1) * rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.White),
                        Border = new FastReport.Border
                        {
                            Lines = FastReport.BorderLines.All,
                            Color = System.Drawing.Color.FromArgb(200, 200, 200),
                            Width = 1f
                        }
                    };
                    spTableBand.Objects.Add(tableBorder);

                    float currentY = 5f;

                    // Header row background
                    var headerBg = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(0, currentY, totalWidth, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
                    };
                    spTableBand.Objects.Add(headerBg);

                    // Header text - Service Point
                    var h1 = new FastReport.TextObject
                    {
                        Text = "Service Point",
                        Bounds = new System.Drawing.RectangleF(2, currentY + 3, col1 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    spTableBand.Objects.Add(h1);

                    // Header text - Served
                    var h2 = new FastReport.TextObject
                    {
                        Text = "Served",
                        Bounds = new System.Drawing.RectangleF(col1 + 2, currentY + 3, col2 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Center,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    spTableBand.Objects.Add(h2);

                    // Header text - Wait
                    var h3 = new FastReport.TextObject
                    {
                        Text = "Wait (min)",
                        Bounds = new System.Drawing.RectangleF(col1 + col2 + 2, currentY + 3, col3 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Center,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    spTableBand.Objects.Add(h3);

                    // Header text - Service
                    var h4 = new FastReport.TextObject
                    {
                        Text = "Service (min)",
                        Bounds = new System.Drawing.RectangleF(col1 + col2 + col3 + 2, currentY + 3, col4 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Center,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    spTableBand.Objects.Add(h4);

                    // Vertical lines in header
                    var vLine1 = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(col1, currentY, 0.5f, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(200, 210, 255))
                    };
                    spTableBand.Objects.Add(vLine1);

                    var vLine2 = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(col1 + col2, currentY, 0.5f, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(200, 210, 255))
                    };
                    spTableBand.Objects.Add(vLine2);

                    var vLine3 = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(col1 + col2 + col3, currentY, 0.5f, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(200, 210, 255))
                    };
                    spTableBand.Objects.Add(vLine3);

                    currentY += rowHeight;

                    // Data rows
                    bool alternate = false;
                    foreach (var sp in reportData.ServicePointPerformance)
                    {
                        var rowColor = alternate ?
                            System.Drawing.Color.FromArgb(248, 249, 250) :
                            System.Drawing.Color.White;
                        alternate = !alternate;

                        // Row background
                        var rowBg = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(0, currentY, totalWidth, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(rowColor)
                        };
                        spTableBand.Objects.Add(rowBg);

                        // Service Point name
                        var nameText = new FastReport.TextObject
                        {
                            Text = sp.ServicePointName,
                            Bounds = new System.Drawing.RectangleF(2, currentY + 3, col1 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Left,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = System.Drawing.Color.FromArgb(33, 37, 41)
                        };
                        spTableBand.Objects.Add(nameText);

                        // Served count
                        var servedText = new FastReport.TextObject
                        {
                            Text = sp.CustomersServed.ToString(),
                            Bounds = new System.Drawing.RectangleF(col1 + 2, currentY + 3, col2 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8, sp.CustomersServed > 0 ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular),
                            HorzAlign = FastReport.HorzAlign.Center,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = sp.CustomersServed > 0 ? System.Drawing.Color.FromArgb(41, 98, 255) : System.Drawing.Color.FromArgb(108, 117, 125)
                        };
                        spTableBand.Objects.Add(servedText);

                        // Wait time
                        var waitText = new FastReport.TextObject
                        {
                            Text = sp.AverageWaitTimeMinutes.ToString("F1"),
                            Bounds = new System.Drawing.RectangleF(col1 + col2 + 2, currentY + 3, col3 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Center,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = System.Drawing.Color.FromArgb(33, 37, 41)
                        };
                        spTableBand.Objects.Add(waitText);

                        // Service time
                        var serviceText = new FastReport.TextObject
                        {
                            Text = sp.AverageServiceTimeMinutes.ToString("F1"),
                            Bounds = new System.Drawing.RectangleF(col1 + col2 + col3 + 2, currentY + 3, col4 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Center,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = System.Drawing.Color.FromArgb(33, 37, 41)
                        };
                        spTableBand.Objects.Add(serviceText);

                        // Vertical lines
                        var vl1 = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(col1, currentY, 0.5f, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        spTableBand.Objects.Add(vl1);

                        var vl2 = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(col1 + col2, currentY, 0.5f, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        spTableBand.Objects.Add(vl2);

                        var vl3 = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(col1 + col2 + col3, currentY, 0.5f, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        spTableBand.Objects.Add(vl3);

                        // Horizontal line
                        var hLine = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(0, currentY + rowHeight, totalWidth, 0.5f),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        spTableBand.Objects.Add(hLine);

                        currentY += rowHeight;
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
                        Bounds = new System.Drawing.RectangleF(0, 5, pageWidth, 10),
                        Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.FromArgb(41, 98, 255)
                    };
                    staffHeaderBand.Objects.Add(staffHeader);

                    var staffLine = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(0, 17, 60, 1.5f),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
                    };
                    staffHeaderBand.Objects.Add(staffLine);

                    // Staff Table
                    float rowHeight = 18f;
                    int rowCount = reportData.StaffPerformance.Count;
                    float tableHeight = (rowCount + 1) * rowHeight + 15;

                    var staffTableBand = new FastReport.DataBand();
                    staffTableBand.Height = tableHeight;
                    page.Bands.Add(staffTableBand);

                    // Column widths for staff table (5 columns)
                    float sCol1 = 50f;  // Staff Name
                    float sCol2 = 50f;  // Service Point
                    float sCol3 = 25f;  // Served
                    float sCol4 = 27f;  // Wait
                    float sCol5 = 28f;  // Service
                    float sTotalWidth = sCol1 + sCol2 + sCol3 + sCol4 + sCol5;

                    // Table border
                    var tableBorder = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(0, 5, sTotalWidth, (rowCount + 1) * rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.White),
                        Border = new FastReport.Border
                        {
                            Lines = FastReport.BorderLines.All,
                            Color = System.Drawing.Color.FromArgb(200, 200, 200),
                            Width = 1f
                        }
                    };
                    staffTableBand.Objects.Add(tableBorder);

                    float currentY = 5f;

                    // Header row background
                    var headerBg = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(0, currentY, sTotalWidth, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
                    };
                    staffTableBand.Objects.Add(headerBg);

                    // Header text - Staff Member
                    var sh1 = new FastReport.TextObject
                    {
                        Text = "Staff Member",
                        Bounds = new System.Drawing.RectangleF(2, currentY + 3, sCol1 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    staffTableBand.Objects.Add(sh1);

                    // Header text - Service Point
                    var sh2 = new FastReport.TextObject
                    {
                        Text = "Service Point",
                        Bounds = new System.Drawing.RectangleF(sCol1 + 2, currentY + 3, sCol2 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Left,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    staffTableBand.Objects.Add(sh2);

                    // Header text - Served
                    var sh3 = new FastReport.TextObject
                    {
                        Text = "Served",
                        Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + 2, currentY + 3, sCol3 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Center,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    staffTableBand.Objects.Add(sh3);

                    // Header text - Wait
                    var sh4 = new FastReport.TextObject
                    {
                        Text = "Wait (min)",
                        Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + sCol3 + 2, currentY + 3, sCol4 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Center,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    staffTableBand.Objects.Add(sh4);

                    // Header text - Service
                    var sh5 = new FastReport.TextObject
                    {
                        Text = "Service (min)",
                        Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + sCol3 + sCol4 + 2, currentY + 3, sCol5 - 4, rowHeight - 6),
                        Font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold),
                        HorzAlign = FastReport.HorzAlign.Center,
                        VertAlign = FastReport.VertAlign.Center,
                        TextColor = System.Drawing.Color.White
                    };
                    staffTableBand.Objects.Add(sh5);

                    // Vertical lines in header
                    var svLine1 = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(sCol1, currentY, 0.5f, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(200, 210, 255))
                    };
                    staffTableBand.Objects.Add(svLine1);

                    var svLine2 = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(sCol1 + sCol2, currentY, 0.5f, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(200, 210, 255))
                    };
                    staffTableBand.Objects.Add(svLine2);

                    var svLine3 = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + sCol3, currentY, 0.5f, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(200, 210, 255))
                    };
                    staffTableBand.Objects.Add(svLine3);

                    var svLine4 = new FastReport.ShapeObject
                    {
                        Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + sCol3 + sCol4, currentY, 0.5f, rowHeight),
                        Shape = FastReport.ShapeKind.Rectangle,
                        Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(200, 210, 255))
                    };
                    staffTableBand.Objects.Add(svLine4);

                    currentY += rowHeight;

                    // Data rows
                    bool alternate = false;
                    foreach (var staff in reportData.StaffPerformance)
                    {
                        var rowColor = alternate ?
                            System.Drawing.Color.FromArgb(248, 249, 250) :
                            System.Drawing.Color.White;
                        alternate = !alternate;

                        // Row background
                        var rowBg = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(0, currentY, sTotalWidth, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(rowColor)
                        };
                        staffTableBand.Objects.Add(rowBg);

                        // Staff name
                        var nameText = new FastReport.TextObject
                        {
                            Text = staff.StaffName,
                            Bounds = new System.Drawing.RectangleF(2, currentY + 3, sCol1 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Left,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = System.Drawing.Color.FromArgb(33, 37, 41)
                        };
                        staffTableBand.Objects.Add(nameText);

                        // Service Point
                        var spText = new FastReport.TextObject
                        {
                            Text = staff.ServicePointName,
                            Bounds = new System.Drawing.RectangleF(sCol1 + 2, currentY + 3, sCol2 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Left,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = System.Drawing.Color.FromArgb(108, 117, 125)
                        };
                        staffTableBand.Objects.Add(spText);

                        // Served count
                        var servedText = new FastReport.TextObject
                        {
                            Text = staff.CustomersServed.ToString(),
                            Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + 2, currentY + 3, sCol3 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8, staff.CustomersServed > 0 ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular),
                            HorzAlign = FastReport.HorzAlign.Center,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = staff.CustomersServed > 0 ? System.Drawing.Color.FromArgb(41, 98, 255) : System.Drawing.Color.FromArgb(108, 117, 125)
                        };
                        staffTableBand.Objects.Add(servedText);

                        // Wait time
                        var waitText = new FastReport.TextObject
                        {
                            Text = staff.AverageWaitTimeMinutes.ToString("F1"),
                            Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + sCol3 + 2, currentY + 3, sCol4 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Center,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = System.Drawing.Color.FromArgb(33, 37, 41)
                        };
                        staffTableBand.Objects.Add(waitText);

                        // Service time
                        var serviceText = new FastReport.TextObject
                        {
                            Text = staff.AverageServiceTimeMinutes.ToString("F1"),
                            Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + sCol3 + sCol4 + 2, currentY + 3, sCol5 - 4, rowHeight - 6),
                            Font = new System.Drawing.Font("Arial", 8),
                            HorzAlign = FastReport.HorzAlign.Center,
                            VertAlign = FastReport.VertAlign.Center,
                            TextColor = System.Drawing.Color.FromArgb(33, 37, 41)
                        };
                        staffTableBand.Objects.Add(serviceText);

                        // Vertical lines
                        var svl1 = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(sCol1, currentY, 0.5f, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        staffTableBand.Objects.Add(svl1);

                        var svl2 = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(sCol1 + sCol2, currentY, 0.5f, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        staffTableBand.Objects.Add(svl2);

                        var svl3 = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + sCol3, currentY, 0.5f, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        staffTableBand.Objects.Add(svl3);

                        var svl4 = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(sCol1 + sCol2 + sCol3 + sCol4, currentY, 0.5f, rowHeight),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        staffTableBand.Objects.Add(svl4);

                        // Horizontal line
                        var hLine = new FastReport.ShapeObject
                        {
                            Bounds = new System.Drawing.RectangleF(0, currentY + rowHeight, sTotalWidth, 0.5f),
                            Shape = FastReport.ShapeKind.Rectangle,
                            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                        };
                        staffTableBand.Objects.Add(hLine);

                        currentY += rowHeight;
                    }
                }

                // ========== FOOTER ==========
                var footerBand = new FastReport.PageFooterBand();
                footerBand.Height = 25;
                page.Bands.Add(footerBand);

                // Footer separator line
                var footerLine = new FastReport.ShapeObject
                {
                    Bounds = new System.Drawing.RectangleF(0, 0, pageWidth, 0.5f),
                    Shape = FastReport.ShapeKind.Rectangle,
                    Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(222, 226, 230))
                };
                footerBand.Objects.Add(footerLine);

                // Footer text - left
                var footerLeft = new FastReport.TextObject
                {
                    Text = "Queue Management System",
                    Bounds = new System.Drawing.RectangleF(0, 8, pageWidth / 2, 8),
                    Font = new System.Drawing.Font("Arial", 7),
                    HorzAlign = FastReport.HorzAlign.Left,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(108, 117, 125)
                };
                footerBand.Objects.Add(footerLeft);

                // Footer text - right (page number)
                var footerRight = new FastReport.TextObject
                {
                    Text = "Page [Page#] of [TotalPages#]",
                    Bounds = new System.Drawing.RectangleF(pageWidth / 2, 8, pageWidth / 2, 8),
                    Font = new System.Drawing.Font("Arial", 7),
                    HorzAlign = FastReport.HorzAlign.Right,
                    VertAlign = FastReport.VertAlign.Center,
                    TextColor = System.Drawing.Color.FromArgb(108, 117, 125)
                };
                footerBand.Objects.Add(footerRight);

                // Prepare and export PDF
                report.Prepare();

                using (var ms = new System.IO.MemoryStream())
                {
                    var pdfExport = new FastReport.Export.Pdf.PDFExport();

                    // Basic PDF settings (compatible with free version)
                    pdfExport.ShowProgress = false;
                    pdfExport.Subject = "Queue Management Analytical Report";
                    pdfExport.Title = $"Queue Analytics {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}";
                    pdfExport.Author = "Queue Management System";
                    pdfExport.Keywords = "Queue, Analytics, Report, Performance";
                    pdfExport.Creator = "Queue Management System";

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
