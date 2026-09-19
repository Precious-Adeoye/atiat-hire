using System.ComponentModel.DataAnnotations;
using AtiatHire.Infrastructure;
using AtiatHire.Models;

namespace AtiatHire.ViewModels;

public class HireRequestForm : IValidatableObject
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(120)]
    [Display(Name = "Full name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a phone number."), StringLength(30)]
    [RegularExpression(@"^\+?[0-9 ()\-]{7,20}$", ErrorMessage = "Enter a valid phone number, for example 0803 123 4567.")]
    [Display(Name = "Phone number")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the pickup location."), StringLength(200)]
    [Display(Name = "Pickup location")]
    public string PickupLocation { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the destination."), StringLength(200)]
    public string Destination { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose the pickup date and time.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Pickup date and time")]
    public DateTime? PickupDateTime { get; set; }

    [Range(1, 720, ErrorMessage = "Duration must be between 1 and 720 hours.")]
    [Display(Name = "Duration (hours)")]
    public int DurationHours { get; set; } = 8;

    [Range(1, 60, ErrorMessage = "Passengers must be between 1 and 60.")]
    [Display(Name = "Number of passengers")]
    public int Passengers { get; set; } = 1;

    [Display(Name = "Vehicle preference")]
    public VehicleType PreferredVehicleType { get; set; } = VehicleType.SUV;

    [Display(Name = "Driver")]
    public DriverRequirement DriverRequirement { get; set; } = DriverRequirement.WithDriver;

    [Display(Name = "Purpose of trip")]
    public TripPurpose Purpose { get; set; } = TripPurpose.Business;

    [StringLength(1000)]
    [Display(Name = "Additional information")]
    public string? AdditionalInformation { get; set; }

    /// <summary>Honeypot: hidden from people, filled in by simple bots.</summary>
    public string? Website { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PickupDateTime.HasValue && PickupDateTime.Value < TimeHelper.LagosNow.AddMinutes(30))
        {
            yield return new ValidationResult(
                "Choose a pickup time at least 30 minutes from now.",
                new[] { nameof(PickupDateTime) });
        }
    }
}

public class TrackForm
{
    [Required(ErrorMessage = "Enter your reference number.")]
    [Display(Name = "Reference number")]
    public string Reference { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the phone number you used for the request.")]
    [Display(Name = "Phone number")]
    public string Phone { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your username.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
