namespace DashSpec.Modeling.Core

open System

/// <summary>Reference instant for IANA → TimeShift conversion during parse (DST-aware at that instant).</summary>
module ZoneResolveReference =

    /// <summary>Defaults to <see cref="DateTime.UtcNow"/>; tests may override.</summary>
    let mutable Utc: DateTime = DateTime.UtcNow
