using Basera.Shared.Domain;
using Basera.Shared.Results;

namespace Basera.Modules.Identity.Domain;

/// <summary>
/// One hostel business (PRODUCT_SPEC §3): the tenant every tenant-owned row points to. Not itself
/// <see cref="ITenantOwned"/>: its <see cref="Entity.Id"/> is the tenant id.
/// </summary>
public sealed class Tenant : Entity, IAuditable
{
    public const int NameMaxLength = 200;
    public const int TrialDays = 60;

    private Tenant(string name, PhoneNumber phone, DateOnly trialEndsOn)
    {
        Name = name;
        Phone = phone;
        Plan = TenantPlan.Starter;
        TrialEndsOn = trialEndsOn;
        Status = TenantStatus.Trial;
    }

    // For EF Core.
    private Tenant()
    {
        Name = "";
        Phone = null!;
    }

    /// <summary>Trading name shown in the app, e.g. "Sunrise Boys Hostel, Kirtipur".</summary>
    public string Name { get; private set; }

    /// <summary>Registered business name, for invoices and the police register.</summary>
    public string? LegalName { get; private set; }

    /// <summary>Nepal Permanent Account Number: 9 digits.</summary>
    public string? Pan { get; private set; }

    public string? Address { get; private set; }

    public PhoneNumber Phone { get; private set; }

    /// <summary>File storage key of the logo, never a public URL (§5 Files).</summary>
    public string? LogoPath { get; private set; }

    public TenantPlan Plan { get; private set; }

    public DateOnly TrialEndsOn { get; private set; }

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <summary>Owner self-signup: Starter plan on a 60-day trial from <paramref name="today"/> (Nepal date).</summary>
    public static Result<Tenant> Register(string? name, PhoneNumber phone, DateOnly today)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return DomainError.Validation("Enter the hostel name.", "hostelName");
        }

        if (trimmed.Length > NameMaxLength)
        {
            return DomainError.Validation($"The hostel name can be at most {NameMaxLength} characters.", "hostelName");
        }

        return new Tenant(trimmed, phone, today.AddDays(TrialDays));
    }

    /// <summary>Legal details for documents. A PAN, when given, is exactly 9 digits.</summary>
    public Result UpdateLegalDetails(string? legalName, string? pan, string? address)
    {
        var trimmedPan = string.IsNullOrWhiteSpace(pan) ? null : pan.Trim();
        if (trimmedPan is not null && (trimmedPan.Length != 9 || !trimmedPan.All(char.IsAsciiDigit)))
        {
            return Result.Failure(DomainError.Validation("A PAN has exactly 9 digits.", "pan"));
        }

        LegalName = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim();
        Pan = trimmedPan;
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        return Result.Success();
    }

    public void SetLogo(string? fileKey) => LogoPath = fileKey;
}

/// <summary>Plans by bed count (PRODUCT_SPEC §3).</summary>
public enum TenantPlan
{
    /// <summary>Up to 30 beds.</summary>
    Starter,

    /// <summary>Up to 100 beds.</summary>
    Standard,

    /// <summary>Unlimited beds, several properties.</summary>
    Pro,
}

public enum TenantStatus
{
    Trial,
    Active,
    Suspended,
    Closed,
}
