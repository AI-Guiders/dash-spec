#nullable enable

using System.Globalization;
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands.Constructors;

public sealed class DashboardSlashConstructorHost
{
    public ValueConstructorRegistry Registry { get; } = new();
    public DateConstructorSegmentProvider SegmentProvider { get; }
    public ValueConstructorNavigator Navigator { get; }
    public ArgConstructorSession Session { get; }
    readonly IPrefixArmProfile _datePrefixProfile;

    public DashboardSlashConstructorHost(IDashboardCultureAmbient cultureAmbient)
    {
        SegmentProvider = new DateConstructorSegmentProvider(cultureAmbient);
        DateConstructorCatalog.Register(Registry);
        Navigator = new ValueConstructorNavigator(Registry, SegmentProvider);
        Session = new ArgConstructorSession(Navigator);
        _datePrefixProfile = new LocaleDatePrefixArmProfile(cultureAmbient);
    }

    public SlashCompletionOptions CreateCompletionOptions(CultureInfo culture, DateOnly anchorDate) =>
        new()
        {
            ConstructorRegistry = Registry,
            Culture = new CultureAmbient(culture),
            SegmentProvider = SegmentProvider,
            AnchorDate = anchorDate,
            PrefixArmProfiles = [_datePrefixProfile],
        };
}
