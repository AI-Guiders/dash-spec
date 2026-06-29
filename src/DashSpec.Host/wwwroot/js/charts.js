window.dashSpecCharts = {
  _instances: {},

  render(canvasId, chartType, labels, series, options) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) {
      return;
    }

    const existing = Chart.getChart(canvas);
    if (existing) {
      existing.destroy();
    }
    if (this._instances[canvasId]) {
      delete this._instances[canvasId];
    }

    const palette = ["#60a5fa", "#34d399", "#fbbf24", "#f472b6", "#a78bfa", "#fb7185", "#38bdf8", "#4ade80"];
    const isBar = (chartType || "line") === "bar";
    const stacked = !!(options && options.stacked);
    const datasets = (series || []).map((item, index) => ({
      label: item.name === "default" ? "value" : item.name,
      data: item.values,
      borderColor: palette[index % palette.length],
      backgroundColor: palette[index % palette.length] + (isBar ? "cc" : "55"),
      borderWidth: item.name === "Other" ? 1 : isBar ? 1 : 2,
      pointRadius: isBar ? 0 : labels.length > 40 ? 0 : 2,
      pointHoverRadius: isBar ? 0 : 4,
      tension: isBar ? 0 : 0.15,
      spanGaps: !isBar,
    }));

    const legend = (options && options.legend) || "bottom";
    const showLegend = legend !== "hidden";
    const legendPosition = legend === "hidden" ? "bottom" : legend;

    this._instances[canvasId] = new Chart(canvas, {
      type: chartType || "line",
      data: { labels: labels || [], datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: stacked ? "index" : "nearest", axis: "x", intersect: false },
        plugins: {
          legend: {
            display: showLegend,
            position: legendPosition,
            align: legendPosition === "right" ? "start" : "center",
            labels: {
              boxWidth: 10,
              padding: 10,
              font: { size: 11 },
            },
          },
          tooltip: {
            mode: stacked ? "index" : "nearest",
            intersect: false,
          },
        },
        scales: {
          x: {
            stacked,
            ticks: {
              maxRotation: 0,
              autoSkip: true,
              maxTicksLimit: 12,
              font: { size: 10 },
            },
            grid: { color: "#2a354433" },
          },
          y: {
            stacked,
            beginAtZero: true,
            ticks: {
              font: { size: 10 },
              stepSize: stacked ? 1 : undefined,
              precision: stacked ? 0 : undefined,
            },
            grid: { color: "#2a354455" },
          },
        },
      },
    });
  },

  destroy(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (canvas && window.Chart) {
      const existing = Chart.getChart(canvas);
      if (existing) {
        existing.destroy();
      }
    }
    delete this._instances[canvasId];
  },
};
