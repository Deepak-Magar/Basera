using Basera.Shared.Results;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Web;

/// <summary>
/// ADR-008: runs the FluentValidation validator registered for each action argument and adds its errors to
/// <c>ModelState</c>, before the action runs. The action checks <c>ModelState.IsValid</c> as usual and
/// redisplays the form (or the HTMX partial) with the messages next to the inputs.
/// </summary>
public sealed class FluentValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (!context.ActionArguments.TryGetValue(parameter.Name, out var argument) || argument is null)
            {
                continue;
            }

            // [Bind(Prefix = "Details")] posts "Details.DisplayName"; the error must use the same key to show up.
            var prefix = parameter.BindingInfo?.BinderModelName;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var error in result.Errors)
            {
                context.ModelState.AddModelError(
                    string.IsNullOrEmpty(prefix) ? error.PropertyName : $"{prefix}.{error.PropertyName}", error.ErrorMessage);
            }
        }

        await next();
    }
}

public static class ModelStateExtensions
{
    /// <summary>
    /// Puts a service's <see cref="DomainError"/> on the form field it names. Targets are camelCase on the wire
    /// ("hostelName"); form inputs are PascalCase properties (<c>HostelName</c>), under <paramref name="prefix"/>
    /// when the input was bound with one. No target: a form-level error.
    /// </summary>
    public static void AddDomainError(this ModelStateDictionary modelState, DomainError error, string? prefix = null)
    {
        var field = string.IsNullOrEmpty(error.Target) ? "" : char.ToUpperInvariant(error.Target[0]) + error.Target[1..];
        modelState.AddModelError(string.IsNullOrEmpty(prefix) || field.Length == 0 ? field : $"{prefix}.{field}", error.Message);
    }
}
