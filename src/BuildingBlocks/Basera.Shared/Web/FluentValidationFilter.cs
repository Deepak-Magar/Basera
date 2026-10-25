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
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var error in result.Errors)
            {
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
        }

        await next();
    }
}

public static class ModelStateExtensions
{
    /// <summary>
    /// Puts a service's <see cref="DomainError"/> on the form field it names. Targets are camelCase on the wire
    /// ("hostelName"); form inputs are PascalCase properties (<c>HostelName</c>). No target: a form-level error.
    /// </summary>
    public static void AddDomainError(this ModelStateDictionary modelState, DomainError error)
    {
        var key = string.IsNullOrEmpty(error.Target) ? "" : char.ToUpperInvariant(error.Target[0]) + error.Target[1..];
        modelState.AddModelError(key, error.Message);
    }
}
