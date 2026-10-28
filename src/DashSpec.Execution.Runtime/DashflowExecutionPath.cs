using System.Globalization;
using DashSpec.Modeling.Parse.DataFlow;

namespace DashSpec.Execution.Runtime;

/// <summary>Resolved SQL source plus transformers from source to a card flow input (ADR-0078).</summary>
public sealed record DashflowExecutionPath(
    DashflowSourceDefinition Source,
    IReadOnlyList<DashflowTransformerDefinition> Transformers)
{
    /// <summary>
    /// Граф в обратную сторону (G⁻¹): шаги в обратном порядке, to_zone с обратным сдвигом.
    /// Данные идут по G от источника к диаграмме; выбор пользователя (клик по оси) идёт
    /// по G⁻¹ от диаграммы к источнику. Шаг без обратной операции делает инверсию невозможной.
    /// </summary>
    public DashflowExecutionPath Invert()
    {
        var transformers = new List<DashflowTransformerDefinition>();
        for (var i = Transformers.Count - 1; i >= 0; i--)
        {
            var transformer = Transformers[i];
            var steps = new List<DashflowTransformStepDefinition>();
            for (var s = transformer.Steps.Length - 1; s >= 0; s--)
            {
                steps.Add(InvertStep(transformer.Steps[s]));
            }

            transformers.Add(new DashflowTransformerDefinition(
                transformer.Id,
                transformer.Inputs,
                transformer.Outputs,
                transformer.DefaultInputPort,
                transformer.DefaultOutputPort,
                steps.ToArray()));
        }

        return new DashflowExecutionPath(Source, transformers);
    }

    /// <summary>Применить путь к времени суток: подписи оси и значения фильтров живут в этом кадре.</summary>
    public TimeOnly ApplyToTime(TimeOnly value)
    {
        var minutes = 0.0;
        foreach (var transformer in Transformers)
        {
            foreach (var step in transformer.Steps)
            {
                if (step.PluginId.Equals(BuiltinScalarTransformIds.ToZone, StringComparison.OrdinalIgnoreCase))
                {
                    minutes += ToZoneOffsetMinutes(step);
                }
            }
        }

        return value.AddMinutes(minutes);
    }

    /// <summary>Применить путь к полному моменту времени (для диапазонов drill-фильтров).</summary>
    public DateTime ApplyToDateTime(DateTime value)
    {
        var minutes = 0.0;
        foreach (var transformer in Transformers)
        {
            foreach (var step in transformer.Steps)
            {
                if (step.PluginId.Equals(BuiltinScalarTransformIds.ToZone, StringComparison.OrdinalIgnoreCase))
                {
                    minutes += ToZoneOffsetMinutes(step);
                }
            }
        }

        return value.AddMinutes(minutes);
    }

    private static DashflowTransformStepDefinition InvertStep(DashflowTransformStepDefinition step)
    {
        if (!step.PluginId.Equals(BuiltinScalarTransformIds.ToZone, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Dashflow transform '{step.PluginId}' has no inverse; the graph cannot be applied backwards.");
        }

        var parameters = step.Parameters
            .Select(p => p.Key == "offset_minutes"
                ? new DashflowTransformParameterDefinition(
                    p.Key,
                    (-int.Parse(p.Value, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture))
                : p)
            .ToArray();

        return new DashflowTransformStepDefinition(step.PluginId, parameters);
    }

    private static int ToZoneOffsetMinutes(DashflowTransformStepDefinition step)
    {
        foreach (var parameter in step.Parameters)
        {
            if (parameter.Key == "offset_minutes"
                && int.TryParse(parameter.Value, CultureInfo.InvariantCulture, out var minutes))
            {
                return minutes;
            }
        }

        return 0;
    }
}
