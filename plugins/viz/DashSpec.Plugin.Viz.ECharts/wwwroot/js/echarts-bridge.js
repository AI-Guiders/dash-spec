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
    render: function (hostArg, optionJson, ctxJson, dotnet) {
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

      // Fill the pane: compute the cell side from the actual body size — squares by
      // construction, dead space below disappears. The floor is the widest date label
      // (the label defines its column; 30px label — 30px cell), the fill is capped at
      // 64px. Re-fit on pane resize.
      const frame = host.closest('.ech-frame');
      const body = host.parentElement;
      const xStrip = frame.querySelector('.ech-x-strip');
      const fit = function () {
        // The agreed contract: the TOP strip defines the square's side — the widest
        // x-label plus breathing room. No fill clamp: the pane scrolls right, and
        // clamping to a shrunken fill re-merged the date labels ("06.1007.10").
        // Measure the TEXT (Range), never the box: the label box stretches to the
        // cell, so scrollWidth feeds the previous side back in — the cell ratchets
        // wider on every fit (the 544px runaway).
        let labelWidth = 32;
        frame.querySelectorAll('.ech-x-label').forEach(function (l) {
          if (l.textContent.trim()) {
            const range = document.createRange();
            range.selectNodeContents(l);
            const textW = Math.ceil(range.getBoundingClientRect().width);
            labelWidth = Math.max(labelWidth, textW + 12);
          }
        });
        const side = labelWidth;
        // The pane's available height comes from the frame (its max-height cap or the
        // fill slot) — read it there, not from the body: the snap below sets the
        // body's own height, and reading it back would feed back into the snap.
        const availH = frame.clientHeight - (xStrip ? xStrip.offsetHeight : 0);
        frame.style.setProperty('--ech-cell', side + 'px');
        // The bridge owns the host box: the component's inline size is a 28px-era
        // snapshot — without this the canvas outgrew its container and sat shifted
        // (the dead gap between the axis strip and the data).
        host.style.width = ctx.cols * side + 'px';
        host.style.height = ctx.rows * side + 'px';
        chart.resize({ width: ctx.cols * side, height: ctx.rows * side });
        // Whole-row fold: never slice the last visible row at the scroll edge — a
        // half row there reads as the legend riding onto the matrix. Snap only when
        // the content actually overflows; a short matrix keeps its natural height
        // (snapping it would spiral the frame's content height down).
        const contentH = ctx.rows * side;
        if (contentH > availH) {
          const snapped = Math.floor(availH / side) * side;
          if (body.__dashspecSnapH !== snapped) {
            body.__dashspecSnapH = snapped;
            body.style.height = snapped + 'px';
          }
        } else if (body.style.height !== '') {
          body.__dashspecSnapH = null;
          body.style.height = '';
        }
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
