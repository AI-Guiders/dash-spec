// Ambient declarations for the JS bridges loaded from CDN / Blazor host.
// These globals are provided at runtime (echarts from CDN, dashspecECharts
// mounted by index.mjs via Window) — declared here so checkJs diagnostics
// stay clean without disabling semantic checks.
declare const echarts: any;

interface Window {
  dashspecECharts: any;
}
