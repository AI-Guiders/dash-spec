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

    const palette = ["#60a5fa", "#34d399", "#fbbf24", "#f472b6", "#a78bfa", "#fb7185", "#38bdf8", "#4ade80", "#eab308", "#14b8a6", "#ec4899", "#94a3b8"];
    const type = chartType || "line";
    const isRadial = type === "pie" || type === "doughnut";
    if (isRadial) {
      this._renderRadial(canvasId, canvas, type, labels, series, options, palette);
      return;
    }

    const isBar = type === "bar";
    const stacked = !!(options && options.stacked);
    const horizontal = isBar && !!(options && options.horizontal);
    const categoryLabels = labels || [];
    const categoryAxis = horizontal ? "y" : "x";
    const longCategoryLabels = categoryLabels.some((l) => String(l).length > 6);
    const referenceValues = (options && options.referenceValues) || null;
    const referenceLabel = (options && options.referenceLabel) || "Куплено";
    const categoryAxisLabel = (options && options.categoryAxisLabel) || "";
    const valueAxisLabel = (options && options.valueAxisLabel) || "";
    const valueAxisMax = (options && options.valueAxisMax) || null;
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
    const isPercentAxis = valueAxisScale === "percent";

    const valueMax = Math.max(
      0,
      ...datasets.flatMap((d) => (d.data || []).map((v) => (v == null ? 0 : Number(v)))),
      ...(hasReference ? referenceValues.map((v) => (v == null ? 0 : Number(v))) : []),
    );
    const hardValueMax =
      valueAxisMax != null && valueAxisMax > 0 ? valueAxisMax : isPercentAxis ? 100 : null;
    const paddedMax =
      hardValueMax != null
        ? hardValueMax
        : valueMax > 0
          ? Math.ceil(valueMax * 1.12)
          : undefined;

    const integerAxisStep = (max) => {
      if (max <= 10) {
        return 1;
      }
      if (max <= 25) {
        return 2;
      }
      if (max <= 60) {
        return 5;
      }
      if (max <= 150) {
        return 10;
      }
      return Math.max(10, Math.ceil(max / 12));
    };

    const percentLimit =
      isPercentAxis && hardValueMax != null && hardValueMax > 0 && !hasReference ? hardValueMax : null;

    const percentLimitPlugin = {
      id: "dashSpecPercentLimit",
      afterDatasetsDraw(chart) {
        if (percentLimit == null) {
          return;
        }
        const ctx = chart.ctx;
        const meta = chart.getDatasetMeta(0);
        if (!meta || !meta.data || !meta.data.length) {
          return;
        }
        const valueScale = horizontal ? chart.scales.x : chart.scales.y;
        const pixel = valueScale.getPixelForValue(percentLimit);
        const first = meta.data[0];
        const last = meta.data[meta.data.length - 1];
        const top = Math.min(first.y, last.y) - ((first.height || 12) / 2);
        const bottom = Math.max(first.y, last.y) + ((last.height || 12) / 2);
        ctx.save();
        ctx.strokeStyle = "#f97316";
        ctx.lineWidth = 2.5;
        ctx.setLineDash([5, 4]);
        ctx.beginPath();
        if (horizontal) {
          ctx.moveTo(pixel, top);
          ctx.lineTo(pixel, bottom);
        } else {
          ctx.moveTo(top, pixel);
          ctx.lineTo(bottom, pixel);
        }
        ctx.stroke();
        ctx.restore();
      },
    };

    const valueTickFormat = isPercentAxis
      ? {
          callback: (value) => `${value}%`,
        }
      : {};

    const valueAxisLimit =
      hardValueMax != null
        ? { max: hardValueMax, suggestedMax: hardValueMax }
        : paddedMax != null
          ? { suggestedMax: paddedMax }
          : {};

    const valueTicks = forceIntegerAxis
      ? { stepSize: integerAxisStep(hardValueMax ?? valueMax), precision: 0, maxTicksLimit: 12 }
      : isPercentAxis
        ? { stepSize: 25, maxTicksLimit: 5, ...valueTickFormat }
        : stacked
          ? { stepSize: 1, precision: 0 }
          : {};

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
      type,
      data: { labels: categoryLabels, datasets },
      plugins: [referencePlugin, percentLimitPlugin],
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
                const extra = [];
                if (hasReference) {
                  extra.push({
                    text: referenceLabel,
                    fillStyle: "transparent",
                    strokeStyle: "#f97316",
                    lineWidth: 2.5,
                    lineDash: [5, 4],
                    pointStyle: "line",
                  });
                }
                if (percentLimit != null) {
                  extra.push({
                    text: "лимит",
                    fillStyle: "transparent",
                    strokeStyle: "#f97316",
                    lineWidth: 2.5,
                    lineDash: [5, 4],
                    pointStyle: "line",
                  });
                }
                return defaults.concat(extra);
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
        onClick(event, elements, chart) {
          if (!elements || !elements.length) {
            return;
          }
          window.dashSpecCharts._emitCategoryClick(options, categoryLabels, elements[0].index);
        },
        onHover(event, elements, chart) {
          const canvasEl = chart && chart.canvas;
          if (!canvasEl) {
            return;
          }
          const clickEnabled = options && options.categoryClickEnabled;
          canvasEl.style.cursor =
            clickEnabled && elements && elements.length ? "pointer" : "default";
        },
        scales: {
          x: {
            stacked,
            beginAtZero: horizontal,
            ...(horizontal ? valueAxisLimit : {}),
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
            ...(!horizontal ? valueAxisLimit : {}),
            title: horizontal ? axisTitle(categoryAxisLabel) : axisTitle(valueAxisLabel),
            ticks: {
              maxRotation: 0,
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

  _emitCategoryClick(options, categoryLabels, idx) {
    const clickEnabled = options && options.categoryClickEnabled;
    const dotNetRef = options && options.dotNetRef;
    if (!clickEnabled || !dotNetRef || idx == null || idx < 0) {
      return false;
    }
    const label = categoryLabels[idx];
    if (label == null || label === "") {
      return false;
    }
    dotNetRef.invokeMethodAsync("OnCategoryClick", idx, String(label));
    return true;
  },

  _renderRadial(canvasId, canvas, type, labels, series, options, palette) {
    const categoryLabels = labels || [];
    const legend = (options && options.legend) || "right";
    const showLegend = legend !== "hidden";
    const legendPosition = legend === "hidden" ? "right" : legend;
    const valueAxisLabel = (options && options.valueAxisLabel) || "";
    const clickEnabled = !!(options && options.categoryClickEnabled);
    const item = (series && series[0]) || { name: "default", values: [], pointColors: null };
    const colors = (item.pointColors && item.pointColors.length === categoryLabels.length)
      ? item.pointColors
      : categoryLabels.map((_, i) => palette[i % palette.length]);
    const self = this;
    const defaultLegendClick = Chart.defaults.plugins.legend.onClick;

    this._instances[canvasId] = new Chart(canvas, {
      type,
      data: {
        labels: categoryLabels,
        datasets: [
          {
            label: item.name === "default" ? (valueAxisLabel || "value") : item.name,
            data: item.values || [],
            backgroundColor: colors,
            borderColor: "#0f172a",
            borderWidth: 1,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: type === "doughnut" ? "55%" : undefined,
        interaction: {
          mode: "nearest",
          intersect: true,
        },
        plugins: {
          legend: {
            display: showLegend,
            position: legendPosition,
            align: legendPosition === "right" ? "start" : "center",
            labels: {
              boxWidth: 10,
              padding: 8,
              font: { size: 11 },
            },
            onClick(event, legendItem, legendArg) {
              // With category drill, legend is a hit target (not hide/show).
              if (clickEnabled) {
                const idx = legendItem && typeof legendItem.index === "number"
                  ? legendItem.index
                  : -1;
                self._emitCategoryClick(options, categoryLabels, idx);
                return;
              }
              if (typeof defaultLegendClick === "function") {
                defaultLegendClick.call(this, event, legendItem, legendArg);
              }
            },
          },
          tooltip: {
            callbacks: {
              label(ctx) {
                const value = ctx.parsed;
                const total = (ctx.dataset.data || []).reduce((a, b) => a + (b == null ? 0 : Number(b)), 0);
                const pct = total > 0 ? ((Number(value) / total) * 100).toFixed(1) : "0";
                return `${ctx.label}: ${value} (${pct}%)`;
              },
            },
          },
        },
        onClick(event, elements) {
          if (!elements || !elements.length) {
            return;
          }
          self._emitCategoryClick(options, categoryLabels, elements[0].index);
        },
        onHover(event, elements, chart) {
          const canvasEl = chart && chart.canvas;
          if (!canvasEl) {
            return;
          }
          canvasEl.style.cursor =
            clickEnabled && elements && elements.length ? "pointer" : "default";
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
