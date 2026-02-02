using System.ComponentModel.DataAnnotations;
namespace Queue_Management_System.Models
{

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
	 public string Email { get; set; } = string.Empty;  // Add this
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? ServicePointId { get; set; }
    public string? ServicePointName { get; set; }
    public bool ServicePointIsActive { get; set; }
}}