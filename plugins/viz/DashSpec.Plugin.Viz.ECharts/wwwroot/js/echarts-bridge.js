// DashSpec ECharts bridge (spike, ADR-0112): render/iterate one heatmap per component
// instance. The .NET side owns the option; this file owns the chart lifecycle, the
// tooltip formatter (payload tooltips), the value-label formatter and the click→indices
// mapping (ADR-0107). Keyed by the DotNetObjectReference identity — one chart per ref.
//
// Geometry contract: the component ships the host box as an inline snapshot (cells at
// --ech-cell, cols/rows × side); the bridge mounts into it and never re-fits. The old
// label-width fit was cut deliberately (runaway ratchet 544px, merged date labels,
// canvas-container gap) — do not restore it; a new fill-mode fit, if ever designed,
// starts from the pane size, never from the label box.
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
    render: function (hostArg, optionJson, ctxJson, dotnet) {
      // Circuit guard — see bindPanes. Returns false: the .NET side treats it as
      // "nothing mounted", same as a missing host.
      try {
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

      // The host arrives as a DOM id string (the .NET side no longer ships the
      // ElementReference: cached across renders it becomes the {__internalId}
      // wrapper, and echarts cannot mount on it — the "getContext" class of deaths).
      const host = typeof hostArg === 'string'
        ? document.getElementById(hostArg)
        : hostArg;
      if (!host || typeof host.appendChild !== 'function') {
        // The host div is not in the DOM (inactive view, detached card) — there is
        // nothing to mount on; skipping is the correct outcome, a thrown error here
        // kills the whole circuit.
        return false;
      }
      host.innerHTML = '';
      const container = document.createElement('div');
      container.style.width = '100%';
      container.style.height = '100%';
      host.appendChild(container);

      const chart = echarts.init(container, null, { renderer: 'canvas' });
      chart.setOption(option);
      wireClicks(chart, ctx, dotnet);
      charts.set(dotnet, { chart: chart, ctx: ctx, option: option, selected: null });
      return true;
      } catch (e) {
        console.error('[dashspecECharts] render failed (chart degrades, circuit survives):', e);
        return false;
      }
    },

    // Frozen panes: the body pane is the only scroller; the fixed axis strips mirror
    // its scroll via transforms. Wheel over a strip drives the body — nothing ever
    // slides under the axes.
    bindPanes: function (body, xInner, yInner) {
      // Circuit guard (Blazor Server): an exception here surfaces in .NET and kills the
      // whole page's circuit — every button on every card goes dead. The bridge is a
      // render helper: it fails loudly in the console, but never throws across the wire.
      try {
      const sync = function () {
        xInner.style.transform = 'translateX(' + -body.scrollLeft + 'px)';
        yInner.style.transform = 'translateY(' + -body.scrollTop + 'px)';
      };
      // Row-quantized scroll: both fold edges land on cell boundaries — no half row
      // or sliced label ever shows at the pane edge. Snap only after the gesture
      // settles (150ms idle), never mid-momentum.
      const quantize = function () {
        const frame = body.closest('.ech-frame');
        const side = parseFloat(getComputedStyle(frame).getPropertyValue('--ech-cell')) || 28;
        const top = Math.round(body.scrollTop / side) * side;
        const left = Math.round(body.scrollLeft / side) * side;
        if (Math.abs(top - body.scrollTop) >= 2 || Math.abs(left - body.scrollLeft) >= 2) {
          body.scrollTop = top;
          body.scrollLeft = left;
        }
      };
      const onScroll = function () {
        sync();
        if (body.__dashspecSnapTimer) {
          clearTimeout(body.__dashspecSnapTimer);
        }
        body.__dashspecSnapTimer = setTimeout(quantize, 150);
      };
      body.removeEventListener('scroll', body.__dashspecPaneSync || function () {});
      body.__dashspecPaneSync = onScroll;
      body.addEventListener('scroll', onScroll, { passive: true });
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

      // Whole-row fold, restored carefully: the body pane shows only whole matrix rows.
      // A fractional pane height (499px = 17 rows + 23px) slices the edge row in half —
      // and since the legend sits right below the frame, the sliced row reads as "the
      // legend covers the row". The size is measured from the FRAME, never from the
      // body itself: height set on the body cannot feed back into the measurement
      // (the old label-width fit died of exactly that ratchet). The ±1px guard keeps
      // the ResizeObserver at a fixed point.
      const foldFrame = body.closest('.ech-frame');
      const foldRows = function () {
        if (!foldFrame) {
          return;
        }
        const side = parseFloat(getComputedStyle(foldFrame).getPropertyValue('--ech-cell')) || 28;
        // rows are derived from the y-strip content height — bindPanes has no ctx.
        const rows = Math.round(yInner.getBoundingClientRect().height / side);
        const xStrip = foldFrame.querySelector('.ech-x-strip');
        const avail = foldFrame.clientHeight - (xStrip ? xStrip.offsetHeight : 0);
        if (avail <= 0 || rows * side <= avail) {
          // Short matrix: the natural height shows whole rows already.
          if (body.__dashspecFoldH) {
            body.__dashspecFoldH = null;
            body.style.height = '';
          }
          return;
        }
        const snapped = Math.floor(avail / side) * side;
        if (Math.abs(body.offsetHeight - snapped) <= 1) {
          return;
        }
        body.__dashspecFoldH = snapped;
        body.style.height = snapped + 'px';
      };
      foldRows();
      if (foldFrame) {
        if (foldFrame.__dashspecFoldObserver) {
          foldFrame.__dashspecFoldObserver.disconnect();
        }
        foldFrame.__dashspecFoldObserver = new ResizeObserver(foldRows);
        foldFrame.__dashspecFoldObserver.observe(foldFrame);
      }
      } catch (e) {
        console.error('[dashspecECharts] bindPanes failed (panes degrade, circuit survives):', e);
      }
    },

    // Viewer selection (ADR-0116) lives server-side now: the selected cell is baked
    // into the option (accent fill) on every render — no JS-side repaint roundtrip.

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
