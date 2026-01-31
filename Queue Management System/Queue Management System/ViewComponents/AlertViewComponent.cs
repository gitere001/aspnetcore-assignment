using Microsoft.AspNetCore.Mvc;

namespace Queue_Management_System.ViewComponents
{
    public class AlertViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(string message, string type = "success")
        {
            return View(new AlertViewModel { Message = message, Type = type });
        }
    }

    public class AlertViewModel
    {
        public string Message { get; set; } = "";
        public string Type { get; set; } = "success"; // success or danger
    }
}