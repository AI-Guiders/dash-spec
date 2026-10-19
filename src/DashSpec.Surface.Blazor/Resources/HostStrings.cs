using System.Diagnostics.CodeAnalysis;

namespace DashSpec.Surface.Blazor.Resources;

/// <summary>
/// Marker for <c>HostStrings.resx</c> / <c>HostStrings.ru.resx</c> (ADR-0077).
/// The resource base name equals this type's full name.
/// </summary>
[SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "Resource marker type for IStringLocalizer<T>.")]
public sealed class HostStrings
{
}
