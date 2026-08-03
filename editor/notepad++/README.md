; Notepad++ User Defined Language — DashSpec (stub)
; Sync keywords from editor/vscode-dashspec/syntaxes/dashspec.tmLanguage.json when DSL changes.
; Import: Language → Define your language → Import → dashspec.xml (export from this file after edit)

; Extensions: dashspec dashdiagram dashpresentation dashlayout dashcatalog dashpalette dashinclude
; Comment: #
; Operators: =

[Keywords 1]
runtime configuration wiring report extensions filter bind show card data view layout chrome
presentation transform series diagram phase page toolbar filters when focus preserve drill
invoke buttons views datasource include use end bar line heatmap table

[Keywords 2]
@dashboard @tab @catalog @diagram @presentation !include goto

[Operators]
=

[Folder & Default]
# 0 40

; Full UDL XML export is manual for v0.1 — use VS Code extension as primary editor.
; See editor/README.md
