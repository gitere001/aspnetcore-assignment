namespace Queue_Management_System.Models
{
    public class ServicePointDropdownDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public bool IsAssigned { get; set; }
    }
}