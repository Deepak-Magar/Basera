namespace Basera.Shared.Authorization;

/// <summary>
/// The tenant roles of ARCHITECTURE §7 as they appear in the <c>role</c> claim. Identity owns the
/// <c>Role</c> enum and the memberships; these strings live here so every module's policies and
/// controllers can name a role without referencing the Identity project.
/// </summary>
public static class BaseraRoles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Warden = "Warden";
    public const string Cook = "Cook";
    public const string Resident = "Resident";
    public const string Parent = "Parent";

    public static readonly IReadOnlyList<string> All = [Owner, Manager, Warden, Cook, Resident, Parent];

    /// <summary>
    /// The area a role works in (ARCHITECTURE §1): management at <c>/owner</c>, wardens and cooks at <c>/staff</c>,
    /// residents and parents in their own areas. No or unknown role: the landing page.
    /// </summary>
    public static string HomePath(string? role) => role switch
    {
        Owner or Manager => "/owner",
        Warden or Cook => "/staff",
        Resident => "/resident",
        Parent => "/parent",
        _ => "/",
    };
}
