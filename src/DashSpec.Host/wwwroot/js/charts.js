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
    const horizontal = isBar && !!(options && options.horizontal);
    const categoryLabels = labels || [];
    const categoryAxis = horizontal ? "y" : "x";
    const valueAxis = horizontal ? "x" : "y";
    const longCategoryLabels = categoryLabels.some((l) => String(l).length > 6);

    const datasets = (series || []).map((item, index) => {
      const color = item.color || palette[index % palette.length];
      const pointColors = item.pointColors;
      const barFill = (c) => c + (isBar ? "cc" : "55");
      return {
        label: item.name === "default" ? "value" : item.name,
        data: item.values,
        borderColor: pointColors || color,
        backgroundColor: pointColors ? pointColors.map(barFill) : barFill(color),
        borderWidth: item.name === "Other" ? 1 : isBar ? 1 : 2,
        pointRadius: isBar ? 0 : categoryLabels.length > 40 ? 0 : 2,
        pointHoverRadius: isBar ? 0 : 4,
        tension: isBar ? 0 : 0.15,
        spanGaps: !isBar,
      };
    });

    const legend = (options && options.legend) || "bottom";
    const showLegend = legend !== "hidden";
    const legendPosition = legend === "hidden" ? "bottom" : legend;
    const valueAxisScale = (options && options.valueAxisScale) || "decimal";
    const forceIntegerAxis = valueAxisScale === "integer";
    const valueTicks = forceIntegerAxis
      ? { stepSize: 1, precision: 0 }
      : stacked
        ? { stepSize: 1, precision: 0 }
        : {};

    this._instances[canvasId] = new Chart(canvas, {
      type: chartType || "line",
      data: { labels: categoryLabels, datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        indexAxis: horizontal ? "y" : "x",
        interaction: {
          mode: stacked ? "index" : "nearest",
          axis: categoryAxis,
          intersect: false,
        },
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
            beginAtZero: horizontal,
            ticks: {
              maxRotation: !horizontal && longCategoryLabels ? 45 : 0,
              minRotation: 0,
              autoSkip: !horizontal,
              autoSkipPadding: 6,
              maxTicksLimit: horizontal
                ? undefined
                : categoryLabels.length > 72
                  ? 12
                  : categoryLabels.length > 36
                    ? 18
                    : 24,
              font: { size: 10 },
              ...(horizontal ? valueTicks : {}),
            },
            grid: { color: "#2a354433" },
          },
          y: {
            stacked,
            beginAtZero: !horizontal,
            ticks: {
              maxRotation: horizontal && longCategoryLabels ? 0 : 0,
              minRotation: 0,
              autoSkip: horizontal,
              autoSkipPadding: horizontal ? 4 : 6,
              maxTicksLimit: horizontal
                ? categoryLabels.length > 24
                  ? 24
                  : undefined
                : undefined,
              font: { size: 10 },
              ...(horizontal ? {} : valueTicks),
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
