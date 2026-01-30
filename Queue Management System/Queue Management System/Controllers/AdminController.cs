using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Queue_Management_System.Controllers
{
    [Authorize(Roles = "Admin")] // Only admins can access
    public class AdminController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Reports()
        {
            return View();
        }

        public IActionResult Users()
        {
            return View();
        }

        public IActionResult ServicePoints()
        {
            return View();
        }

        public IActionResult Services()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }
    }
}