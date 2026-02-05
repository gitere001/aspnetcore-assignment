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

        _logger.LogInformation($"Report: {reportData}");

        // Create FastReport
        var report = new FastReport.Report();

        // IMPORTANT: FastReport works in PIXELS internally.
        // We convert mm → pixels using this constant (≈3.7795 px/mm)
        float mm = FastReport.Utils.Units.Millimeters;

        var page = new FastReport.ReportPage();

        // A4 settings (in mm)
        page.PaperWidth = 210;
        page.PaperHeight = 297;
        page.LeftMargin = 15;
        page.RightMargin = 15;
        page.TopMargin = 15;
        page.BottomMargin = 15;

        report.Pages.Add(page);

        // Safe usable width in pixels (~175 mm physical)
        float pageWidth = 175 * mm;

        // ========== HEADER SECTION ==========
        var headerBand = new FastReport.ReportTitleBand();
        headerBand.Height = 40 * mm;  // Reduced height

        // Header background
        var headerBox = new FastReport.ShapeObject
        {
            Bounds = new System.Drawing.RectangleF(0, 0, pageWidth, 35 * mm),
            Shape = FastReport.ShapeKind.Rectangle,
            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
        };
        headerBand.Objects.Add(headerBox);

        // Main Title
        var mainTitle = new FastReport.TextObject
        {
            Text = "QUEUE MANAGEMENT SYSTEM",
            Bounds = new System.Drawing.RectangleF(0, 5 * mm, pageWidth, 10 * mm),
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
            Bounds = new System.Drawing.RectangleF(0, 17 * mm, pageWidth, 8 * mm),
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
            Bounds = new System.Drawing.RectangleF(0, 26 * mm, pageWidth, 6 * mm),
            Font = new System.Drawing.Font("Arial", 9),
            HorzAlign = FastReport.HorzAlign.Center,
            VertAlign = FastReport.VertAlign.Center,
            TextColor = System.Drawing.Color.White
        };
        headerBand.Objects.Add(dateText);

        // Generation timestamp
        // var timestampText = new FastReport.TextObject
        // {
        //     Text = $"Generated: {DateTime.Now:MMMM d, yyyy h:mm tt}",
        //     Bounds = new System.Drawing.RectangleF(0, 33 * mm, pageWidth, 5 * mm),
        //     Font = new System.Drawing.Font("Arial", 7),
        //     HorzAlign = FastReport.HorzAlign.Center,
        //     VertAlign = FastReport.VertAlign.Center,
        //     TextColor = System.Drawing.Color.FromArgb(230, 240, 255)
        // };
        // headerBand.Objects.Add(timestampText);

        // Assign as ReportTitle (key fix for rendering)
        page.ReportTitle = headerBand;

        // ========== SUMMARY SECTION ==========
        var summaryBand = new FastReport.DataBand();
        summaryBand.Height = 55 * mm;  // Reduced from 110mm
        page.Bands.Add(summaryBand);

        // Summary Title
        var summaryTitle = new FastReport.TextObject
        {
            Text = "OVERALL SUMMARY",
            Bounds = new System.Drawing.RectangleF(0, 2 * mm, pageWidth, 8 * mm),
            Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold),
            HorzAlign = FastReport.HorzAlign.Left,
            VertAlign = FastReport.VertAlign.Center,
            TextColor = System.Drawing.Color.FromArgb(41, 98, 255)
        };
        summaryBand.Objects.Add(summaryTitle);

        // Decorative line
        var summaryLine = new FastReport.ShapeObject
        {
            Bounds = new System.Drawing.RectangleF(0, 12 * mm, 50 * mm, 1f),
            Shape = FastReport.ShapeKind.Rectangle,
            Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
        };
        summaryBand.Objects.Add(summaryLine);

        // Summary boxes - 3 columns
        float boxWidth = 50 * mm;    // Reduced width
        float boxHeight = 35 * mm;   // Reduced height
        float boxY = 18 * mm;        // Adjusted Y position
        float gap = (pageWidth - (boxWidth * 3)) / 4;  // Calculate gap for even spacing

        // Box 1: Total Customers
        float box1X = gap;
        var box1 = new FastReport.ShapeObject
        {
            Bounds = new System.Drawing.RectangleF(box1X, boxY, boxWidth, boxHeight),
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
            Bounds = new System.Drawing.RectangleF(box1X, boxY + 4 * mm, boxWidth, 14 * mm),
            Font = new System.Drawing.Font("Arial", 18, System.Drawing.FontStyle.Bold),
            HorzAlign = FastReport.HorzAlign.Center,
            VertAlign = FastReport.VertAlign.Center,
            TextColor = System.Drawing.Color.FromArgb(41, 98, 255)
        };
        summaryBand.Objects.Add(box1Value);

        var box1Label = new FastReport.TextObject
        {
            Text = "Total Customers Served",
            Bounds = new System.Drawing.RectangleF(box1X + 4, boxY + 20 * mm, boxWidth - 8, 12 * mm),
            Font = new System.Drawing.Font("Arial", 8.5f),
            HorzAlign = FastReport.HorzAlign.Center,
            VertAlign = FastReport.VertAlign.Center,
            TextColor = System.Drawing.Color.FromArgb(108, 117, 125),
            WordWrap = true
        };
        summaryBand.Objects.Add(box1Label);

        // Box 2: Avg Wait Time
        float box2X = box1X + boxWidth + gap;
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
            Bounds = new System.Drawing.RectangleF(box2X, boxY + 4 * mm, boxWidth, 14 * mm),
            Font = new System.Drawing.Font("Arial", 18, System.Drawing.FontStyle.Bold),
            HorzAlign = FastReport.HorzAlign.Center,
            VertAlign = FastReport.VertAlign.Center,
            TextColor = System.Drawing.Color.FromArgb(23, 162, 184)
        };
        summaryBand.Objects.Add(box2Value);

        var box2Label = new FastReport.TextObject
        {
            Text = "Average Wait Time (minutes)",
            Bounds = new System.Drawing.RectangleF(box2X + 4, boxY + 20 * mm, boxWidth - 8, 12 * mm),
            Font = new System.Drawing.Font("Arial", 8.5f),
            HorzAlign = FastReport.HorzAlign.Center,
            VertAlign = FastReport.VertAlign.Center,
            TextColor = System.Drawing.Color.FromArgb(108, 117, 125),
            WordWrap = true
        };
        summaryBand.Objects.Add(box2Label);

        // Box 3: Avg Service Time
        float box3X = box2X + boxWidth + gap;
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
            Bounds = new System.Drawing.RectangleF(box3X, boxY + 4 * mm, boxWidth, 14 * mm),
            Font = new System.Drawing.Font("Arial", 18, System.Drawing.FontStyle.Bold),
            HorzAlign = FastReport.HorzAlign.Center,
            VertAlign = FastReport.VertAlign.Center,
            TextColor = System.Drawing.Color.FromArgb(40, 167, 69)
        };
        summaryBand.Objects.Add(box3Value);

        var box3Label = new FastReport.TextObject
        {
            Text = "Average Service Time (minutes)",
            Bounds = new System.Drawing.RectangleF(box3X + 4, boxY + 20 * mm, boxWidth - 8, 12 * mm),
            Font = new System.Drawing.Font("Arial", 8.5f),
            HorzAlign = FastReport.HorzAlign.Center,
            VertAlign = FastReport.VertAlign.Center,
            TextColor = System.Drawing.Color.FromArgb(108, 117, 125),
            WordWrap = true
        };
        summaryBand.Objects.Add(box3Label);

        // ========== SERVICE POINT PERFORMANCE ==========
        if (reportData.ServicePointPerformance?.Any() == true)
        {
            // Section Header
            var spHeaderBand = new FastReport.DataBand();
            spHeaderBand.Height = 28 * mm;
            page.Bands.Add(spHeaderBand);

            var spHeader = new FastReport.TextObject
            {
                Text = "SERVICE POINT PERFORMANCE",
                Bounds = new System.Drawing.RectangleF(0, 6 * mm, pageWidth, 12 * mm),
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold),
                HorzAlign = FastReport.HorzAlign.Left,
                VertAlign = FastReport.VertAlign.Center,
                TextColor = System.Drawing.Color.FromArgb(41, 98, 255)
            };
            spHeaderBand.Objects.Add(spHeader);

            var spLine = new FastReport.ShapeObject
            {
                Bounds = new System.Drawing.RectangleF(0, 20 * mm, 90 * mm, 1.5f),
                Shape = FastReport.ShapeKind.Rectangle,
                Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
            };
            spHeaderBand.Objects.Add(spLine);

            // Service Point Table
            float rowHeight = 24 * mm;
            int rowCount = reportData.ServicePointPerformance.Count;
            float tableHeight = (rowCount + 1) * rowHeight + 20 * mm;

            var spTableBand = new FastReport.DataBand();
            spTableBand.Height = tableHeight;
            page.Bands.Add(spTableBand);

            // Wider columns in mm → converted to px
            float col1 = 98 * mm;  // Service Point
            float col2 = 26 * mm;  // Served
            float col3 = 26 * mm;  // Avg Wait
            float col4 = 25 * mm;  // Avg Service
            float totalWidth = col1 + col2 + col3 + col4;

            // Table border
            var tableBorder = new FastReport.ShapeObject
            {
                Bounds = new System.Drawing.RectangleF(0, 5 * mm, totalWidth, (rowCount + 1) * rowHeight),
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

            float currentY = 5 * mm;

            // Header background
            var headerBg = new FastReport.ShapeObject
            {
                Bounds = new System.Drawing.RectangleF(0, currentY, totalWidth, rowHeight),
                Shape = FastReport.ShapeKind.Rectangle,
                Fill = new FastReport.SolidFill(System.Drawing.Color.FromArgb(41, 98, 255))
            };
            spTableBand.Objects.Add(headerBg);

            // Header texts
            var h1 = new FastReport.TextObject
            {
                Text = "Service Point",
                Bounds = new System.Drawing.RectangleF(6, currentY + 4 * mm, col1 - 12, rowHeight - 8 * mm),
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                HorzAlign = FastReport.HorzAlign.Left,
                VertAlign = FastReport.VertAlign.Center,
                TextColor = System.Drawing.Color.White,
                WordWrap = true
            };
            spTableBand.Objects.Add(h1);

            var h2 = new FastReport.TextObject
            {
                Text = "Served",
                Bounds = new System.Drawing.RectangleF(col1 + 2, currentY + 4 * mm, col2 - 4, rowHeight - 8 * mm),
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                HorzAlign = FastReport.HorzAlign.Center,
                VertAlign = FastReport.VertAlign.Center,
                TextColor = System.Drawing.Color.White
            };
            spTableBand.Objects.Add(h2);

            var h3 = new FastReport.TextObject
            {
                Text = "Avg Wait (min)",
                Bounds = new System.Drawing.RectangleF(col1 + col2 + 2, currentY + 4 * mm, col3 - 4, rowHeight - 8 * mm),
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                HorzAlign = FastReport.HorzAlign.Center,
                VertAlign = FastReport.VertAlign.Center,
                TextColor = System.Drawing.Color.White
            };
            spTableBand.Objects.Add(h3);

            var h4 = new FastReport.TextObject
            {
                Text = "Avg Service (min)",
                Bounds = new System.Drawing.RectangleF(col1 + col2 + col3 + 2, currentY + 4 * mm, col4 - 4, rowHeight - 8 * mm),
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                HorzAlign = FastReport.HorzAlign.Center,
                VertAlign = FastReport.VertAlign.Center,
                TextColor = System.Drawing.Color.White
            };
            spTableBand.Objects.Add(h4);

            // Header vertical separators (thinner lines)
            spTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(col1, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(200, 210, 255)) });
            spTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(col1 + col2, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(200, 210, 255)) });
            spTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(col1 + col2 + col3, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(200, 210, 255)) });

            currentY += rowHeight;

            // Data rows
            bool alternate = false;
            foreach (var sp in reportData.ServicePointPerformance)
            {
                var rowColor = alternate ? System.Drawing.Color.FromArgb(248, 249, 250) : System.Drawing.Color.White;
                alternate = !alternate;

                var rowBg = new FastReport.ShapeObject
                {
                    Bounds = new RectangleF(0, currentY, totalWidth, rowHeight),
                    Shape = ShapeKind.Rectangle,
                    Fill = new SolidFill(rowColor)
                };
                spTableBand.Objects.Add(rowBg);

                var nameText = new FastReport.TextObject
                {
                    Text = sp.ServicePointName,
                    Bounds = new RectangleF(6, currentY + 5 * mm, col1 - 12, rowHeight - 10 * mm),
                    Font = new Font("Arial", 9),
                    HorzAlign = HorzAlign.Left,
                    VertAlign = VertAlign.Center,
                    TextColor = Color.FromArgb(33, 37, 41),
                    WordWrap = true
                };
                spTableBand.Objects.Add(nameText);

                var servedText = new FastReport.TextObject
                {
                    Text = sp.CustomersServed.ToString(),
                    Bounds = new RectangleF(col1 + 2, currentY + 4 * mm, col2 - 4, rowHeight - 8 * mm),
                    Font = new Font("Arial", 9, sp.CustomersServed > 0 ? FontStyle.Bold : FontStyle.Regular),
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    TextColor = sp.CustomersServed > 0 ? Color.FromArgb(41, 98, 255) : Color.FromArgb(108, 117, 125)
                };
                spTableBand.Objects.Add(servedText);

                var waitText = new FastReport.TextObject
                {
                    Text = sp.AverageWaitTimeMinutes.ToString("F1"),
                    Bounds = new RectangleF(col1 + col2 + 2, currentY + 4 * mm, col3 - 4, rowHeight - 8 * mm),
                    Font = new Font("Arial", 9),
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    TextColor = Color.FromArgb(33, 37, 41)
                };
                spTableBand.Objects.Add(waitText);

                var serviceText = new FastReport.TextObject
                {
                    Text = sp.AverageServiceTimeMinutes.ToString("F1"),
                    Bounds = new RectangleF(col1 + col2 + col3 + 2, currentY + 4 * mm, col4 - 4, rowHeight - 8 * mm),
                    Font = new Font("Arial", 9),
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    TextColor = Color.FromArgb(33, 37, 41)
                };
                spTableBand.Objects.Add(serviceText);

                // Vertical lines
                spTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(col1, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(222, 226, 230)) });
                spTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(col1 + col2, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(222, 226, 230)) });
                spTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(col1 + col2 + col3, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(222, 226, 230)) });

                // Horizontal line
                spTableBand.Objects.Add(new FastReport.ShapeObject
                {
                    Bounds = new RectangleF(0, currentY + rowHeight, totalWidth, 0.5f),
                    Shape = ShapeKind.Rectangle,
                    Fill = new SolidFill(Color.FromArgb(222, 226, 230))
                });

                currentY += rowHeight;
            }
        }

        // ========== STAFF PERFORMANCE ==========
        if (reportData.StaffPerformance?.Any() == true)
        {
            var staffHeaderBand = new FastReport.DataBand();
            staffHeaderBand.Height = 28 * mm;
            page.Bands.Add(staffHeaderBand);

            var staffHeader = new FastReport.TextObject
            {
                Text = "STAFF PERFORMANCE",
                Bounds = new RectangleF(0, 6 * mm, pageWidth, 12 * mm),
                Font = new Font("Arial", 12, FontStyle.Bold),
                HorzAlign = HorzAlign.Left,
                VertAlign = VertAlign.Center,
                TextColor = Color.FromArgb(41, 98, 255)
            };
            staffHeaderBand.Objects.Add(staffHeader);

            var staffLine = new FastReport.ShapeObject
            {
                Bounds = new RectangleF(0, 20 * mm, 70 * mm, 1.5f),
                Shape = ShapeKind.Rectangle,
                Fill = new SolidFill(Color.FromArgb(41, 98, 255))
            };
            staffHeaderBand.Objects.Add(staffLine);

            float rowHeight = 24 * mm;
            int rowCount = reportData.StaffPerformance.Count;
            float tableHeight = (rowCount + 1) * rowHeight + 20 * mm;

            var staffTableBand = new FastReport.DataBand();
            staffTableBand.Height = tableHeight;
            page.Bands.Add(staffTableBand);

            float sCol1 = 58 * mm;   // Staff Member
            float sCol2 = 68 * mm;   // Service Point
            float sCol3 = 20 * mm;   // Served
            float sCol4 = 20 * mm;   // Avg Wait
            float sCol5 = 20 * mm;   // Avg Service
            float sTotalWidth = sCol1 + sCol2 + sCol3 + sCol4 + sCol5;

            var tableBorder = new FastReport.ShapeObject
            {
                Bounds = new RectangleF(0, 5 * mm, sTotalWidth, (rowCount + 1) * rowHeight),
                Shape = ShapeKind.Rectangle,
                Fill = new SolidFill(Color.White),
                Border = new Border { Lines = BorderLines.All, Color = Color.FromArgb(200, 200, 200), Width = 1f }
            };
            staffTableBand.Objects.Add(tableBorder);

            float currentY = 5 * mm;

            var headerBg = new FastReport.ShapeObject
            {
                Bounds = new RectangleF(0, currentY, sTotalWidth, rowHeight),
                Shape = ShapeKind.Rectangle,
                Fill = new SolidFill(Color.FromArgb(41, 98, 255))
            };
            staffTableBand.Objects.Add(headerBg);

            var sh1 = new FastReport.TextObject
            {
                Text = "Staff Member",
                Bounds = new RectangleF(6, currentY + 4 * mm, sCol1 - 12, rowHeight - 8 * mm),
                Font = new Font("Arial", 9.5f, FontStyle.Bold),
                HorzAlign = HorzAlign.Left,
                VertAlign = VertAlign.Center,
                TextColor = Color.White,
                WordWrap = true
            };
            staffTableBand.Objects.Add(sh1);

            var sh2 = new FastReport.TextObject
            {
                Text = "Service Point",
                Bounds = new RectangleF(sCol1 + 6, currentY + 4 * mm, sCol2 - 12, rowHeight - 8 * mm),
                Font = new Font("Arial", 9.5f, FontStyle.Bold),
                HorzAlign = HorzAlign.Left,
                VertAlign = VertAlign.Center,
                TextColor = Color.White,
                WordWrap = true
            };
            staffTableBand.Objects.Add(sh2);

            var sh3 = new FastReport.TextObject
            {
                Text = "Served",
                Bounds = new RectangleF(sCol1 + sCol2 + 2, currentY + 4 * mm, sCol3 - 4, rowHeight - 8 * mm),
                Font = new Font("Arial", 9.5f, FontStyle.Bold),
                HorzAlign = HorzAlign.Center,
                VertAlign = VertAlign.Center,
                TextColor = Color.White
            };
            staffTableBand.Objects.Add(sh3);

            var sh4 = new FastReport.TextObject
            {
                Text = "Avg Wait (min)",
                Bounds = new RectangleF(sCol1 + sCol2 + sCol3 + 2, currentY + 4 * mm, sCol4 - 4, rowHeight - 8 * mm),
                Font = new Font("Arial", 9.5f, FontStyle.Bold),
                HorzAlign = HorzAlign.Center,
                VertAlign = VertAlign.Center,
                TextColor = Color.White
            };
            staffTableBand.Objects.Add(sh4);

            var sh5 = new FastReport.TextObject
            {
                Text = "Avg Service (min)",
                Bounds = new RectangleF(sCol1 + sCol2 + sCol3 + sCol4 + 2, currentY + 4 * mm, sCol5 - 4, rowHeight - 8 * mm),
                Font = new Font("Arial", 9.5f, FontStyle.Bold),
                HorzAlign = HorzAlign.Center,
                VertAlign = VertAlign.Center,
                TextColor = Color.White
            };
            staffTableBand.Objects.Add(sh5);

            // Header vertical lines
            staffTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(sCol1, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(200, 210, 255)) });
            staffTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(sCol1 + sCol2, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(200, 210, 255)) });
            staffTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(sCol1 + sCol2 + sCol3, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(200, 210, 255)) });
            staffTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(sCol1 + sCol2 + sCol3 + sCol4, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(200, 210, 255)) });

            currentY += rowHeight;

            bool alternate = false;
            foreach (var staff in reportData.StaffPerformance)
            {
                var rowColor = alternate ? Color.FromArgb(248, 249, 250) : Color.White;
                alternate = !alternate;

                var rowBg = new FastReport.ShapeObject
                {
                    Bounds = new RectangleF(0, currentY, sTotalWidth, rowHeight),
                    Shape = ShapeKind.Rectangle,
                    Fill = new SolidFill(rowColor)
                };
                staffTableBand.Objects.Add(rowBg);

                var nameText = new FastReport.TextObject
                {
                    Text = staff.StaffName,
                    Bounds = new RectangleF(6, currentY + 5 * mm, sCol1 - 12, rowHeight - 10 * mm),
                    Font = new Font("Arial", 8.8f),
                    HorzAlign = HorzAlign.Left,
                    VertAlign = VertAlign.Center,
                    TextColor = Color.FromArgb(33, 37, 41),
                    WordWrap = true
                };
                staffTableBand.Objects.Add(nameText);

                var spText = new FastReport.TextObject
                {
                    Text = staff.ServicePointName ?? "Not Assigned",
                    Bounds = new RectangleF(sCol1 + 6, currentY + 5 * mm, sCol2 - 12, rowHeight - 10 * mm),
                    Font = new Font("Arial", 8.8f),
                    HorzAlign = HorzAlign.Left,
                    VertAlign = VertAlign.Center,
                    TextColor = Color.FromArgb(108, 117, 125),
                    WordWrap = true
                };
                staffTableBand.Objects.Add(spText);

                var servedText = new FastReport.TextObject
                {
                    Text = staff.CustomersServed.ToString(),
                    Bounds = new RectangleF(sCol1 + sCol2 + 2, currentY + 4 * mm, sCol3 - 4, rowHeight - 8 * mm),
                    Font = new Font("Arial", 8.8f, staff.CustomersServed > 0 ? FontStyle.Bold : FontStyle.Regular),
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    TextColor = staff.CustomersServed > 0 ? Color.FromArgb(41, 98, 255) : Color.FromArgb(108, 117, 125)
                };
                staffTableBand.Objects.Add(servedText);

                var waitText = new FastReport.TextObject
                {
                    Text = staff.AverageWaitTimeMinutes.ToString("F1"),
                    Bounds = new RectangleF(sCol1 + sCol2 + sCol3 + 2, currentY + 4 * mm, sCol4 - 4, rowHeight - 8 * mm),
                    Font = new Font("Arial", 8.8f),
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    TextColor = Color.FromArgb(33, 37, 41)
                };
                staffTableBand.Objects.Add(waitText);

                var serviceText = new FastReport.TextObject
                {
                    Text = staff.AverageServiceTimeMinutes.ToString("F1"),
                    Bounds = new RectangleF(sCol1 + sCol2 + sCol3 + sCol4 + 2, currentY + 4 * mm, sCol5 - 4, rowHeight - 8 * mm),
                    Font = new Font("Arial", 8.8f),
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    TextColor = Color.FromArgb(33, 37, 41)
                };
                staffTableBand.Objects.Add(serviceText);

                // Vertical lines
                staffTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(sCol1, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(222, 226, 230)) });
                staffTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(sCol1 + sCol2, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(222, 226, 230)) });
                staffTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(sCol1 + sCol2 + sCol3, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(222, 226, 230)) });
                staffTableBand.Objects.Add(new FastReport.ShapeObject { Bounds = new RectangleF(sCol1 + sCol2 + sCol3 + sCol4, currentY, 0.5f, rowHeight), Shape = ShapeKind.Rectangle, Fill = new SolidFill(Color.FromArgb(222, 226, 230)) });

                // Horizontal line
                staffTableBand.Objects.Add(new FastReport.ShapeObject
                {
                    Bounds = new RectangleF(0, currentY + rowHeight, sTotalWidth, 0.5f),
                    Shape = ShapeKind.Rectangle,
                    Fill = new SolidFill(Color.FromArgb(222, 226, 230))
                });

                currentY += rowHeight;
            }
        }

        // ========== FOOTER ==========
        var footerBand = new FastReport.PageFooterBand();
        footerBand.Height = 30 * mm;

        var footerLine = new FastReport.ShapeObject
        {
            Bounds = new RectangleF(0, 4 * mm, pageWidth, 0.5f),
            Shape = ShapeKind.Rectangle,
            Fill = new SolidFill(Color.FromArgb(222, 226, 230))
        };
        footerBand.Objects.Add(footerLine);

        var footerLeft = new FastReport.TextObject
        {
            Text = "Queue Management System",
            Bounds = new RectangleF(0, 10 * mm, pageWidth / 2, 10 * mm),
            Font = new Font("Arial", 8),
            HorzAlign = HorzAlign.Left,
            VertAlign = VertAlign.Center,
            TextColor = Color.FromArgb(108, 117, 125)
        };
        footerBand.Objects.Add(footerLeft);

        var footerRight = new FastReport.TextObject
        {
            Text = "Page [Page#] of [TotalPages#]",
            Bounds = new RectangleF(pageWidth / 2, 10 * mm, pageWidth / 2, 10 * mm),
            Font = new Font("Arial", 8),
            HorzAlign = HorzAlign.Right,
            VertAlign = VertAlign.Center,
            TextColor = Color.FromArgb(108, 117, 125)
        };
        footerBand.Objects.Add(footerRight);

        // Assign as PageFooter (key fix for rendering)
        page.PageFooter = footerBand;

        // Prepare and export
        report.Prepare();

        using (var ms = new System.IO.MemoryStream())
        {
            var pdfExport = new FastReport.Export.Pdf.PDFExport();

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
