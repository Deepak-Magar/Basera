using Basera.Shared.Domain.Calendar;
using Basera.Shared.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Basera.Shared.Web;

/// <summary>
/// Binds form and route values like "2083-06-27" or "२०८३-०६-२७" to <see cref="NepaliDate"/> (and
/// <c>NepaliDate?</c>). A value that is not a real BS date becomes a ModelState error next to the field,
/// in the page language. An empty value leaves a nullable date null; [Required] decides whether that's allowed.
/// </summary>
public sealed class NepaliDateModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
        var text = value.FirstValue;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        if (NepaliDate.TryParse(text, out var date))
        {
            bindingContext.Result = ModelBindingResult.Success(date);
            return Task.CompletedTask;
        }

        var localizer = bindingContext.HttpContext.RequestServices.GetService<IStringLocalizer<SharedResource>>();
        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            localizer?["Validation.BsDate"].Value ?? "Enter a BS date as yyyy-mm-dd.");
        return Task.CompletedTask;
    }
}

public sealed class NepaliDateModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(NepaliDate) ? new NepaliDateModelBinder() : null;
}
