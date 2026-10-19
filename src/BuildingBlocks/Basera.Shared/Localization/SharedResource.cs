namespace Basera.Shared.Localization;

/// <summary>
/// Marker for the shared strings in <c>SharedResource.resx</c> (English, the fallback) and
/// <c>SharedResource.ne.resx</c> (Nepali). Inject <c>IStringLocalizer&lt;SharedResource&gt;</c>, or in views
/// <c>IHtmlLocalizer&lt;SharedResource&gt;</c>. Keys follow <c>Area.Page.Element</c> (ARCHITECTURE §14);
/// strings used everywhere start with <c>Shared.</c>.
/// </summary>
public sealed class SharedResource
{
}
