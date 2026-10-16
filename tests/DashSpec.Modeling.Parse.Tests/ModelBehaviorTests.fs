namespace DashSpec.Modeling.Parse.Tests

open System
open Xunit
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.Document
open DashSpec.Modeling.Parse.Filter

module ModelBehaviorTests =

    let private filter widget bindScope singleSelect =
        { Kind = FilterKind.Field
          Name = "sample"
          DefaultExpression = None
          ColumnReference = None
          Label = None
          Widget = widget
          MinValue = None
          MaxValue = None
          GrainFilterName = None
          SingleSelect = singleSelect
          LayoutRef = None
          BindScopeHint = bindScope
          GrainLabels = None
          ApplyMode = "manual"
          Placement = None }

    [<Fact>]
    let ``filter widget and bind scope predicates preserve authoring semantics`` () =
        let day = filter (Some "DAY") None false
        Assert.True(day.IsDayWidget)
        Assert.False(day.IsComboboxWidget)
        Assert.False(day.IsSelectWidget)
        Assert.True(day.IsBindScopeTooltip)

        let select = filter (Some "SeLeCt") (Some "INLINE") false
        Assert.True(select.IsSelectWidget)
        Assert.True(select.IsSingleSelectField)
        Assert.True(select.IsBindScopeInline)
        Assert.False(select.IsBindScopeHidden)
        Assert.False(select.IsBindScopeTooltip)

        let hidden = filter None (Some "hidden") false
        Assert.True(hidden.IsBindScopeHidden)
        Assert.False(hidden.IsBindScopeInline)
        Assert.False(hidden.IsBindScopeTooltip)

        let explicitSingle = filter None None true
        Assert.True(explicitSingle.IsSingleSelectField)

    [<Fact>]
    let ``filters chrome predicates are case insensitive and retain defaults`` () =
        let chrome =
            { FiltersChromeDefinition.Default with
                Layout = "BAR"
                Sticky = "LiNe"
                Apply = "AUTO"
                ApplyControl = "ICON"
                FormatGuide = "SHOW" }

        Assert.True(chrome.IsApplyIconControl)
        Assert.True(chrome.ShowsFormatGuide)
        Assert.True(chrome.IsAutoApply)
        Assert.True(chrome.IsBarLayout)
        Assert.True(chrome.IsSticky)
        Assert.True(chrome.IsStickyLine)
        Assert.False(chrome.IsStickyCard)
        Assert.True(chrome.IsToolbarLabeledCells)

        let inlineCells = { chrome with Cells = "inline" }
        Assert.False(inlineCells.IsToolbarLabeledCells)

    [<Fact>]
    let ``working time clips only when the apply mode and time column require it`` () =
        let policy apply column =
            { Basis = ReportTimeBasis.Working
              Apply = apply
              WorkTimeColumn = column
              WorkCalendar = None }

        let clip = policy ReportTimeApply.Clip (Some "work_time")
        let both = policy ReportTimeApply.Both (Some "work_time")
        let measure = policy ReportTimeApply.Measure (Some "work_time")
        let noColumn = policy ReportTimeApply.Clip None
        let calendar = { clip with Basis = ReportTimeBasis.Calendar }

        Assert.True(clip.ClipsQueries)
        Assert.True(both.ClipsQueries)
        Assert.False(measure.ClipsQueries)
        Assert.False(noColumn.ClipsQueries)
        Assert.False(calendar.ClipsQueries)

    [<Fact>]
    let ``matrix render limits use defaults and check all three boundaries`` () =
        let limits = { MaxCells = None; MaxAxisLabels = None }
        Assert.Equal(2500, limits.EffectiveMaxCells)
        Assert.Equal(80, limits.EffectiveMaxAxisLabels)
        Assert.False(limits.IsOversized(50, 50))
        Assert.True(limits.IsOversized(51, 50))
        Assert.True(limits.IsOversized(81, 1))
        Assert.True(limits.IsOversized(1, 81))

        let custom = { limits with MaxCells = Some 10; MaxAxisLabels = Some 5 }
        Assert.Equal(10, custom.EffectiveMaxCells)
        Assert.Equal(5, custom.EffectiveMaxAxisLabels)
        Assert.True(custom.IsOversized(4, 3))
