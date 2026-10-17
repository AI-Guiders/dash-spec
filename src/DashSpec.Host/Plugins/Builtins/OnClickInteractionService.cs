using System.Collections;
using DashSpec.Core.Model;
using DashSpec.Viz;
using DashSpec.Surface.Blazor.Services.Presentation;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Reflection;
using System.Reflection;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class OnClickInteractionService : IOnClickInteractionService
{
    public ShowSelectionEffect? ResolveShowEffect(DashSpec.Modeling.Parse.Card.CardClickBehaviour? behaviour) =>
        CardSelectionPresenter.FindShowEffect(behaviour);

    public bool HasNavigationEffects(DashSpec.Modeling.Parse.Card.CardClickBehaviour? behaviour) =>
        behaviour?.Effects.Any(static effect => effect.IsNavigationEffect) == true;

    public IEnumerable<CardClickEffect> ExpandClickEffects(
        IEnumerable<DashSpec.Modeling.Parse.Card.CardClickEffect> effects) =>
        effects.Select(ToExecutionEffect).SelectMany(ExpandSingle);

    private static CardClickEffect ToExecutionEffect(DashSpec.Modeling.Parse.Card.CardClickEffect effect)
    {
        var (caseInfo, fields) = FSharpValue.GetUnionFields(
            effect,
            typeof(DashSpec.Modeling.Parse.Card.CardClickEffect),
            FSharpOption<BindingFlags>.None);

        return caseInfo.Name switch
        {
            "ShowSelection" => new ShowSelectionEffect(
                Enum.Parse<ShowPlacement>(fields[0]!.ToString()!),
                Enum.Parse<ShowFormat>(fields[1]!.ToString()!),
                Enum.Parse<ShowSource>(fields[2]!.ToString()!),
                (bool)fields[3]!,
                (fields[4] as FSharpOption<string>)?.Value),
            "SetFilterFromField" => new SetFilterFromFieldEffect(
                (string)fields[0]!,
                (string)fields[1]!),
            "DrillTableFromCell" => new DrillTableFromCellEffect(
                ((IEnumerable)fields[0]!)
                    .Cast<object>()
                    .Select(static bind => FSharpValue.GetTupleFields(bind))
                    .Select(static bind => new SetFilterFromFieldEffect(
                        (string)bind[0]!,
                        (string)bind[1]!))
                    .ToList()),
            "InvokeHandler" => new InvokeHandlerEffect(
                (string)fields[0]!,
                (IReadOnlyDictionary<string, string>)fields[1]!),
            "GotoTab" => new GotoTabEffect((string)fields[0]!),
            "FocusPhase" => new FocusPhaseEffect((string)fields[0]!),
            "GotoPage" => new GotoPageEffect((string)fields[0]!),
            "GotoCatalogEntry" => new GotoCatalogEntryEffect(
                (string)fields[0]!,
                (fields[1] as FSharpOption<IReadOnlyList<string>>)?.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(effect), caseInfo.Name, "Unknown click-effect case."),
        };
    }

    private static IEnumerable<CardClickEffect> ExpandSingle(CardClickEffect effect) =>
        effect is InvokeHandlerEffect invoke
            ? ExpandInvoke(invoke)
            : [effect];

    private static IEnumerable<CardClickEffect> ExpandInvoke(InvokeHandlerEffect invoke)
    {
        if (!string.Equals(invoke.HandlerId, "drill_down", StringComparison.OrdinalIgnoreCase))
        {
            yield return invoke;
            yield break;
        }

        var defaultFrom = invoke.Args.GetValueOrDefault("from") ?? "y";
        string? tabId = null;

        foreach (var (key, value) in invoke.Args)
        {
            if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(key, "tab", StringComparison.OrdinalIgnoreCase))
            {
                tabId = value;
                continue;
            }

            if (string.Equals(key, "target", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SetFilterFromFieldEffect(value, defaultFrom);
                continue;
            }

            yield return new SetFilterFromFieldEffect(key, value);
        }

        if (!string.IsNullOrWhiteSpace(tabId))
        {
            yield return new GotoTabEffect(tabId);
        }
    }
}
