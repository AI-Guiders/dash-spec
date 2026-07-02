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
    const referenceValues = (options && options.referenceValues) || null;
    const referenceLabel = (options && options.referenceLabel) || "Куплено";
    const categoryAxisLabel = (options && options.categoryAxisLabel) || "";
    const valueAxisLabel = (options && options.valueAxisLabel) || "";
    const defaultSeriesLabel = valueAxisLabel || "value";
    const hasReference =
      horizontal &&
      Array.isArray(referenceValues) &&
      referenceValues.some((v) => v !== null && v !== undefined && !Number.isNaN(Number(v)));

    const datasets = (series || []).map((item, index) => {
      const color = item.color || palette[index % palette.length];
      const pointColors = item.pointColors;
      const barFill = (c) => c + (isBar ? "cc" : "55");
      return {
        label: item.name === "default" ? defaultSeriesLabel : item.name,
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

    const valueMax = Math.max(
      0,
      ...datasets.flatMap((d) => (d.data || []).map((v) => (v == null ? 0 : Number(v)))),
      ...(hasReference ? referenceValues.map((v) => (v == null ? 0 : Number(v))) : []),
    );
    const paddedMax = valueMax > 0 ? Math.ceil(valueMax * 1.12) : undefined;

    const axisTitle = (text) =>
      text
        ? {
            display: true,
            text,
            font: { size: 11 },
            color: "#94a3b8",
          }
        : { display: false };

    const referencePlugin = {
      id: "dashSpecReferenceMarkers",
      afterDatasetsDraw(chart) {
        if (!hasReference) {
          return;
        }
        const ctx = chart.ctx;
        const meta = chart.getDatasetMeta(0);
        if (!meta || !meta.data) {
          return;
        }
        const xScale = chart.scales.x;
        ctx.save();
        ctx.strokeStyle = "#f97316";
        ctx.lineWidth = 2.5;
        ctx.setLineDash([5, 4]);
        for (let i = 0; i < referenceValues.length; i++) {
          const ref = referenceValues[i];
          if (ref == null || Number.isNaN(Number(ref))) {
            continue;
          }
          const bar = meta.data[i];
          if (!bar) {
            continue;
          }
          const x = xScale.getPixelForValue(Number(ref));
          const half = (bar.height || 12) / 2;
          ctx.beginPath();
          ctx.moveTo(x, bar.y - half);
          ctx.lineTo(x, bar.y + half);
          ctx.stroke();
        }
        ctx.restore();
      },
    };

    this._instances[canvasId] = new Chart(canvas, {
      type: chartType || "line",
      data: { labels: categoryLabels, datasets },
      plugins: [referencePlugin],
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
              generateLabels(chart) {
                const defaults = Chart.defaults.plugins.legend.labels.generateLabels(chart);
                if (!hasReference) {
                  return defaults;
                }
                return defaults.concat([
                  {
                    text: referenceLabel,
                    fillStyle: "transparent",
                    strokeStyle: "#f97316",
                    lineWidth: 2.5,
                    lineDash: [5, 4],
                    pointStyle: "line",
                  },
                ]);
              },
            },
          },
          tooltip: {
            mode: stacked ? "index" : "nearest",
            intersect: false,
            callbacks: {
              afterBody(items) {
                if (!hasReference || !items || !items.length) {
                  return [];
                }
                const idx = items[0].dataIndex;
                const ref = referenceValues[idx];
                if (ref == null || Number.isNaN(Number(ref))) {
                  return [];
                }
                const peak = items[0].parsed && horizontal ? items[0].parsed.x : items[0].parsed?.y;
                const lines = [`${referenceLabel}: ${ref}`];
                if (peak != null && Number(ref) > 0) {
                  const pct = ((Number(peak) / Number(ref)) * 100).toFixed(0);
                  lines.push(`Утилизация: ${pct}%`);
                }
                return lines;
              },
            },
          },
        },
        scales: {
          x: {
            stacked,
            beginAtZero: horizontal,
            suggestedMax: horizontal ? paddedMax : undefined,
            title: horizontal ? axisTitle(valueAxisLabel) : axisTitle(categoryAxisLabel),
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
            suggestedMax: !horizontal ? paddedMax : undefined,
            title: horizontal ? axisTitle(categoryAxisLabel) : axisTitle(valueAxisLabel),
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
