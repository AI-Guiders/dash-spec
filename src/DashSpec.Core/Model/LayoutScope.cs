namespace DashSpec.Core.Model;

/// <summary>Declared scope of a layout unit (`module layout &lt;id&gt;`, ADR-0026).</summary>
public enum LayoutScope
{
    Toolbar,
    Tab,
    Page,
    Card,
    Host,
}
