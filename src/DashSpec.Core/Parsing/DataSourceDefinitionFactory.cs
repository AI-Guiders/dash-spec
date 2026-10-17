using Microsoft.FSharp.Core;

namespace DashSpec.Core.Parsing;

/// <summary>Creates the F# data-source IR from .NET execution and host inputs.</summary>
public static class DataSourceDefinitionFactory
{
    public static DataSourceDefinition Create(
        DataSourceKind kind,
        string value,
        DataSourceSqlCarrier? sqlCarrier = null,
        string? sheet = null,
        string rowsType = "",
        bool providerInfer = false,
        string? providerId = null) =>
        new()
        {
            Kind = kind,
            Value = value,
            SqlCarrier = ToOption(sqlCarrier),
            Sheet = ToOption(sheet),
            RowsType = rowsType,
            ProviderInfer = providerInfer,
            ProviderId = ToOption(providerId),
        };

    private static FSharpOption<T> ToOption<T>(T? value) where T : struct =>
        value is T present ? FSharpOption<T>.Some(present) : FSharpOption<T>.None;

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
