// DashSpec ECharts bridge (spike, ADR-0112): render/iterate one heatmap per component
// instance. The .NET side owns the option; this file owns the chart lifecycle, the
// tooltip formatter (payload tooltips), the value-label formatter and the click→indices
// mapping (ADR-0107). Keyed by the DotNetObjectReference identity — one chart per ref.
(function () {
  'use strict';

  const charts = new Map(); // DotNetObjectReference → { chart, ctx }

  function tooltipFormatter(p, ctx) {
    const xi = p.value[0];
    const yi = p.value[1];
    const v = p.value[2];
    let text = (ctx.yLabels[yi] || '') + ' \u00b7 ' + (ctx.xLabels[xi] || '') + ': ' + v;
    const tip = ctx.tooltips && ctx.tooltips[yi] && ctx.tooltips[yi][xi];
    if (tip) {
      text += ' \u2014 ' + tip;
    }
    return text;
  }

  function wireClicks(chart, ctx, dotnet) {
    chart.on('click', function (p) {
      if (p.componentType === 'series' && Array.isArray(p.value)) {
        dotnet.invokeMethodAsync('OnJsEvent', 'cell', p.value[0] | 0, p.value[1] | 0);
      } else if (p.componentType === 'xAxis') {
        dotnet.invokeMethodAsync('OnJsEvent', 'xLabel', ctx.xLabels.indexOf(p.value), -1);
      } else if (p.componentType === 'yAxis') {
        dotnet.invokeMethodAsync('OnJsEvent', 'yLabel', -1, ctx.yLabels.indexOf(p.value));
      }
    });
  }

  window.dashspecECharts = {
    render: function (host, optionJson, ctxJson, dotnet) {
      const previous = charts.get(dotnet);
      if (previous) {
        previous.chart.dispose();
        charts.delete(dotnet);
      }

      const ctx = JSON.parse(ctxJson);
      const option = JSON.parse(optionJson);
      option.tooltip = option.tooltip || {};
      option.tooltip.formatter = function (p) {
        return tooltipFormatter(p, ctx);
      };
      (option.series || []).forEach(function (series) {
        (series.data || []).forEach(function (item) {
          if (item.label) {
            item.label.formatter = function (p) {
              return String(p.value[2]);
            };
          }
        });
      });

      const chart = echarts.init(host, null, { renderer: 'canvas' });
      chart.setOption(option);
      wireClicks(chart, ctx, dotnet);
      charts.set(dotnet, { chart: chart, ctx: ctx, option: option, selected: null });

      // Fill the pane: compute the cell side from the actual body size — squares by
      // construction, dead space below disappears. The floor is the widest date label
      // (the label defines its column; 30px label — 30px cell), the fill is capped at
      // 64px. Re-fit on pane resize.
      const frame = host.closest('.ech-frame');
      const body = host.parentElement;
      const fit = function () {
        let labelWidth = 20;
        frame.querySelectorAll('.ech-x-label').forEach(function (l) {
          if (l.textContent.trim()) {
            labelWidth = Math.max(labelWidth, Math.ceil(l.scrollWidth));
          }
        });
        const fill = Math.floor(Math.min(body.clientWidth / ctx.cols, body.clientHeight / ctx.rows));
        const side = Math.max(labelWidth, Math.min(64, fill));
        frame.style.setProperty('--ech-cell', side + 'px');
        chart.resize({ width: ctx.cols * side, height: ctx.rows * side });
      };
      fit();
      if (frame.__dashspecFitObserver) {
        frame.__dashspecFitObserver.disconnect();
      }
      frame.__dashspecFitObserver = new ResizeObserver(fit);
      frame.__dashspecFitObserver.observe(body);
    },

    // Frozen panes: the body pane is the only scroller; the fixed axis strips mirror
    // its scroll via transforms. Wheel over a strip drives the body — nothing ever
    // slides under the axes.
    bindPanes: function (body, xInner, yInner) {
      const sync = function () {
        xInner.style.transform = 'translateX(' + -body.scrollLeft + 'px)';
        yInner.style.transform = 'translateY(' + -body.scrollTop + 'px)';
      };
      body.removeEventListener('scroll', body.__dashspecPaneSync || function () {});
      body.__dashspecPaneSync = sync;
      body.addEventListener('scroll', sync, { passive: true });
      sync();

      const forward = function (e) {
        if (body.scrollWidth > body.clientWidth || body.scrollHeight > body.clientHeight) {
          body.scrollLeft += e.deltaX;
          body.scrollTop += e.deltaY;
          if (body.scrollLeft !== 0 || e.deltaX !== 0 || e.deltaY !== 0) {
            e.preventDefault();
          }
        }
      };
      [xInner.parentElement, yInner.parentElement].forEach(function (strip) {
        if (!strip) {
          return;
        }
        strip.removeEventListener('wheel', strip.__dashspecPaneWheel || function () {});
        strip.__dashspecPaneWheel = forward;
        strip.addEventListener('wheel', forward, { passive: false });
      });
    },

    // Viewer selection (ADR-0116): the clicked cell keeps an accent border until the
    // payload changes. Repaints item borders in place and re-sets the series data.
    setSelection: function (dotnet, xi, yi) {
      const entry = charts.get(dotnet);
      if (!entry) {
        return;
      }
      const items = entry.option.series[0].data;
      const accent = '#f0a30a';
      const paint = function (item, on) {
        item.itemStyle = item.itemStyle || {};
        item.itemStyle.borderWidth = on ? 2 : 1;
        item.itemStyle.borderColor = on ? accent : 'rgba(15,23,42,0.65)';
      };
      if (entry.selected) {
        const prev = items[entry.selected.index];
        if (prev) {
          paint(prev, false);
        }
      }
      entry.selected = null;
      if (xi >= 0 && yi >= 0) {
        const index = items.findIndex(function (it) {
          return it.value[0] === xi && it.value[1] === yi;
        });
        if (index >= 0) {
          entry.selected = { index: index };
          paint(items[index], true);
        }
      }
      entry.chart.setOption({ series: [{ data: items }] });
    },

    dispose: function (dotnet) {
      const entry = charts.get(dotnet);
      if (!entry) {
        return;
      }
      entry.chart.dispose();
      charts.delete(dotnet);
    },
  };
})();
