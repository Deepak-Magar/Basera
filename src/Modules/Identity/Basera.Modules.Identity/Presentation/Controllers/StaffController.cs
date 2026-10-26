using Basera.Modules.Identity.Application;
using Basera.Modules.Identity.Contracts;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Authorization;
using Basera.Shared.Localization;
using Basera.Shared.Web;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Basera.Modules.Identity.Presentation.Controllers;

public sealed class InviteStaffInput
{
    public string? Phone { get; set; }

    public Role Role { get; set; } = Role.Warden;
}

public sealed class InviteStaffInputValidator : AbstractValidator<InviteStaffInput>
{
    public InviteStaffInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.Phone).NotEmpty().WithMessage(t["Validation.Required"]);
        RuleFor(x => x.Role).Must(StaffInvitation.InvitableRoles.Contains).WithMessage(t["Owner.Staff.RoleNotAllowed"]);
    }
}

public sealed record ManagerPropertiesViewModel(Guid MembershipId, string Name, IReadOnlyList<AssignableProperty> Properties);

/// <summary>
/// The owner's staff pages: list, invite, change role, remove, withdraw an invitation (PRODUCT_SPEC §4.1),
/// and which properties a Manager runs.
/// Every action on one membership or invitation goes through <see cref="TenantMemberRequirement"/>.
/// </summary>
[Area("Owner")]
[Route("owner/staff")]
[Authorize(Policy = BaseraPolicies.RequireOwner)]
public sealed class StaffController(
    StaffService staff, InvitationService invitations, IAuthorizationService authorization, IStringLocalizer<SharedResource> t)
    : Controller
{
    private const string NoticeKey = "Owner.Staff.Notice";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData[NoticeKey] = TempData[NoticeKey];
        return View(await staff.GetOverviewAsync(cancellationToken));
    }

    [HttpGet("invite")]
    public IActionResult Invite() => View(new InviteStaffInput());

    [HttpPost("invite")]
    public async Task<IActionResult> Invite(InviteStaffInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await invitations.InviteAsync(input.Phone, input.Role, InvitationLink, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddDomainError(result.Error!);
            return View(input);
        }

        return Done(t["Owner.Staff.Invited"]);
    }

    [HttpPost("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromForm] Role role, CancellationToken cancellationToken)
    {
        var membership = await staff.FindMembershipAsync(id, cancellationToken);
        if (membership is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(membership))
        {
            return Forbid();
        }

        var result = await staff.ChangeRoleAsync(membership, role, cancellationToken);
        return Done(result.IsSuccess ? t["Owner.Staff.RoleChanged"] : result.Error!.Message);
    }

    [HttpPost("{id:guid}/remove")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var membership = await staff.FindMembershipAsync(id, cancellationToken);
        if (membership is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(membership))
        {
            return Forbid();
        }

        var result = await staff.RemoveAsync(membership, cancellationToken);
        return Done(result.IsSuccess ? t["Owner.Staff.Removed"] : result.Error!.Message);
    }

    /// <summary>The properties a Manager runs (Properties module, through <c>IStaffPropertyAssignments</c>).</summary>
    [HttpGet("{id:guid}/properties")]
    public async Task<IActionResult> Properties(Guid id, CancellationToken cancellationToken)
    {
        var membership = await staff.FindMembershipAsync(id, cancellationToken);
        if (membership is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(membership))
        {
            return Forbid();
        }

        var properties = await staff.PropertiesOfAsync(membership, cancellationToken);
        if (properties.IsFailure)
        {
            return Done(properties.Error!.Message);
        }

        return View(new ManagerPropertiesViewModel(membership.Id, await staff.NameOfAsync(membership, cancellationToken), properties.Value));
    }

    [HttpPost("{id:guid}/properties")]
    public async Task<IActionResult> Properties(Guid id, [FromForm] List<Guid> propertyIds, CancellationToken cancellationToken)
    {
        var membership = await staff.FindMembershipAsync(id, cancellationToken);
        if (membership is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(membership))
        {
            return Forbid();
        }

        var result = await staff.AssignPropertiesAsync(membership, propertyIds, cancellationToken);
        return Done(result.IsSuccess ? t["Owner.Staff.PropertiesSaved"] : result.Error!.Message);
    }

    [HttpPost("invitations/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindAsync(id, cancellationToken);
        if (invitation is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(invitation))
        {
            return Forbid();
        }

        await invitations.RevokeAsync(invitation, cancellationToken);
        return Done(t["Owner.Staff.InvitationRevoked"]);
    }

    private async Task<bool> CanManageAsync(object resource) =>
        (await authorization.AuthorizeAsync(User, resource, TenantMemberRequirement.Instance)).Succeeded;

    private RedirectToActionResult Done(string notice)
    {
        TempData[NoticeKey] = notice;
        return RedirectToAction(nameof(Index));
    }

    // Absolute, because it goes out by SMS. Behind Caddy the forwarded headers make scheme and host the public ones.
    private string InvitationLink(string token) => $"{Request.Scheme}://{Request.Host}{Request.PathBase}/account/invite/{token}";
}
