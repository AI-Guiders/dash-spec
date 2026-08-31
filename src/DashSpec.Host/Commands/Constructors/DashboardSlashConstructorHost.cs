#nullable enable

using System.Globalization;
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands.Constructors;

public sealed class DashboardSlashConstructorHost
{
    public SlashValueConstructorRegistry Registry { get; } = new();
    public DateConstructorSegmentProvider SegmentProvider { get; }
    public SlashValueConstructorNavigator Navigator { get; }
    public SlashConstructorSession Session { get; }
    readonly ISlashPrefixArmProfile _datePrefixProfile;

    public DashboardSlashConstructorHost(IDashboardCultureAmbient cultureAmbient)
    {
        SegmentProvider = new DateConstructorSegmentProvider(cultureAmbient);
        DateConstructorCatalog.Register(Registry);
        Navigator = new SlashValueConstructorNavigator(Registry, SegmentProvider);
        Session = new SlashConstructorSession(Navigator);
        _datePrefixProfile = new SlashLocaleDatePrefixArmProfile(cultureAmbient);
    }

    public SlashCompletionOptions CreateCompletionOptions(CultureInfo culture, DateOnly anchorDate) =>
        new()
        {
            ConstructorRegistry = Registry,
            Culture = new SlashCultureAmbient(culture),
            SegmentProvider = SegmentProvider,
            AnchorDate = anchorDate,
            PrefixArmProfiles = [_datePrefixProfile],
        };
}
