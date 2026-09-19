namespace AtiatHire.Services;

public class WhatsAppOptions
{
    /// <summary>ATIAT's WhatsApp number in international format, digits only (e.g. 2348012345678).</summary>
    public string BusinessNumber { get; set; } = string.Empty;
}

public class StaffOptions
{
    public List<StaffUser> Users { get; set; } = new();
}

public class StaffUser
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
