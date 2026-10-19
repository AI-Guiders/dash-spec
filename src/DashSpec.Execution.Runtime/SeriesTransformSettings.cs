using Microsoft.FSharp.Core;

namespace DashSpec.Execution.Runtime;

/// <summary>Настройки серии, выведенные из блока <c>transform series</c> (ADR-0007).</summary>
public sealed record SeriesTransformSettings(int Max, string OtherLabel)
{
    public static SeriesTransformSettings? FromBlock(SeriesTransformBlock? block)
    {
        if (block?.Max?.Value is int maxValue && maxValue > 0)
        {
            return new SeriesTransformSettings(maxValue, block.OtherLabel?.Value ?? "Other");
        }

        return null;
    }
}
