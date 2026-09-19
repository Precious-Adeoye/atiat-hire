using AtiatHire.Infrastructure;
using Microsoft.Extensions.Options;

namespace AtiatHire.Services;

/// <summary>Builds click-to-chat links so the digital request and the existing WhatsApp process share one reference number.</summary>
public class WhatsAppLinkBuilder
{
    private readonly string _businessNumber;

    public WhatsAppLinkBuilder(IOptions<WhatsAppOptions> options)
    {
        _businessNumber = PhoneHelper.Normalize(options.Value.BusinessNumber);
    }

    /// <summary>Customer -> ATIAT: continue the request on WhatsApp.</summary>
    public string ForCustomer(string reference) =>
        Build(_businessNumber,
            $"Hello ATIAT, I submitted vehicle-hire request {reference} and would like to continue here.");

    /// <summary>Staff -> customer: follow up on a request.</summary>
    public static string ForStaff(string customerPhone, string customerName, string reference) =>
        Build(PhoneHelper.Normalize(customerPhone),
            $"Hello {customerName}, this is ATIAT about your vehicle-hire request {reference}.");

    private static string Build(string number, string text) =>
        $"https://wa.me/{number}?text={Uri.EscapeDataString(text)}";
}
