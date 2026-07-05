window.dashSpecMatrix = {
  hostElement(hostId) {
    return typeof hostId === "string" ? document.getElementById(hostId) : hostId;
  },

  canvasElement(canvasId) {
    return typeof canvasId === "string" ? document.getElementById(canvasId) : canvasId;
  },

  cellBackground(scale, value, min, max) {
    const normalized = (scale || "heat").toLowerCase();
    if (max <= min) {
      return normalized === "mono" ? "hsl(210, 70%, 45%)" : "hsl(210, 80%, 48%)";
    }
    const t = (value - min) / (max - min);
    if (normalized === "mono") {
      const lightness = 28 + t * 24;
      return `hsl(210, 65%, ${lightness.toFixed(0)}%)`;
    }
    const hue = 215 - t * 215;
    const lightness = 32 + t * 22;
    const saturation = 55 + t * 35;
    return `hsl(${hue.toFixed(0)}, ${saturation.toFixed(0)}%, ${lightness.toFixed(0)}%)`;
  },

  cellText(value, min, max) {
    if (max <= min) {
      return "#f8fafc";
    }
    const t = (value - min) / (max - min);
    return t >= 0.45 ? "#0f172a" : "#f8fafc";
  },

  fitCellSize(xCount, yCount, availableWidth, availableHeight, gapPx) {
    const yLabelCol = 144;
    const xLabelRow = 28;
    const chrome = 16;
    const gridW = Math.max(0, availableWidth - yLabelCol - chrome);
    const gridH = Math.max(0, availableHeight - xLabelRow - chrome);
    const minCellW = xCount > 16 ? 30 : 16;
    let cellW = Math.floor((gridW - gapPx * (xCount + 1)) / Math.max(1, xCount));
    let cellH = Math.floor((gridH - gapPx * (yCount + 1)) / Math.max(1, yCount));
    cellW = Math.min(52, Math.max(minCellW, cellW));
    cellH = Math.min(48, Math.max(16, cellH));
    return { cellW, cellH };
  },

  render(canvas, payload) {
    const xCount = payload.xCount;
    const yCount = payload.yCount;
    const cells = payload.cells;
    const min = payload.min;
    const max = payload.max;
    const colorScale = payload.colorScale || "heat";
    const gapPx = payload.gap ?? 2;
    const cellW = payload.cellWidth ?? 26;
    const cellH = payload.cellHeight ?? 22;
    const showValues = payload.showValues !== false;

    const width = xCount * (cellW + gapPx) + gapPx;
    const height = yCount * (cellH + gapPx) + gapPx;
    const dpr = window.devicePixelRatio || 1;

    canvas.width = Math.max(1, Math.floor(width * dpr));
    canvas.height = Math.max(1, Math.floor(height * dpr));
    canvas.style.width = `${width}px`;
    canvas.style.height = `${height}px`;

    const ctx = canvas.getContext("2d");
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);

    canvas._matrixLayout = { cellW, cellH, gapPx, xCount, yCount, cells };

    for (let yi = 0; yi < yCount; yi++) {
      for (let xi = 0; xi < xCount; xi++) {
        const value = cells[yi][xi];
        const x = gapPx + xi * (cellW + gapPx);
        const y = gapPx + yi * (cellH + gapPx);

        if (value == null) {
          ctx.fillStyle = "#141c28";
          ctx.strokeStyle = "#334155";
          ctx.lineWidth = 1;
          ctx.fillRect(x, y, cellW, cellH);
          ctx.setLineDash([3, 3]);
          ctx.strokeRect(x + 0.5, y + 0.5, cellW - 1, cellH - 1);
          ctx.setLineDash([]);
          continue;
        }

        ctx.fillStyle = this.cellBackground(colorScale, value, min, max);
        ctx.fillRect(x, y, cellW, cellH);

        if (showValues && cellW >= 20 && cellH >= 16) {
          ctx.fillStyle = this.cellText(value, min, max);
          const fontSize = Math.min(12, Math.max(9, Math.floor(Math.min(cellW, cellH) * 0.42)));
          ctx.font = `600 ${fontSize}px system-ui, sans-serif`;
          ctx.textAlign = "center";
          ctx.textBaseline = "middle";
          ctx.fillText(String(Math.round(value)), x + cellW / 2, y + cellH / 2);
        }
      }
    }

    return { cellW, cellH, width, height };
  },

  fitAndRender(host, canvas, payload) {
    const gapPx = payload.gap ?? 2;
    const scroll = host.querySelector(".matrix-canvas-scroll") || host;
    const width = scroll.clientWidth || host.getBoundingClientRect().width || 800;
    const maxHeight = payload.maxHeight ?? 420;
    const { cellW, cellH } = this.fitCellSize(
      payload.xCount,
      payload.yCount,
      width,
      maxHeight,
      gapPx);
    host.style.setProperty("--matrix-cell-w", `${cellW}px`);
    host.style.setProperty("--matrix-cell-h", `${cellH}px`);
    return this.render(canvas, {
      ...payload,
      cellWidth: cellW,
      cellHeight: cellH,
    });
  },

  mount(hostId, canvasId, payload) {
    const host = this.hostElement(hostId);
    const canvas = this.canvasElement(canvasId);
    if (!host || !canvas) {
      return;
    }

    this.unmount(hostId);
    host._matrixPayload = payload;
    host._matrixCanvas = canvas;

    const renderNow = () => {
      if (host._matrixRenderFrame) {
        cancelAnimationFrame(host._matrixRenderFrame);
      }
      host._matrixRenderFrame = requestAnimationFrame(() => {
        host._matrixRenderFrame = 0;
        if (!host._matrixPayload || !host._matrixCanvas) {
          return;
        }
        host._matrixLastLayout = "";
        this.fitAndRender(host, host._matrixCanvas, host._matrixPayload);
      });
    };

    renderNow();
    const observer = new ResizeObserver(() => renderNow());
    observer.observe(host.querySelector(".matrix-canvas-scroll") || host);
    host._matrixResizeObserver = observer;
  },

  update(hostId, canvasId, payload) {
    const host = this.hostElement(hostId);
    const canvas = this.canvasElement(canvasId);
    if (!host || !canvas) {
      this.mount(hostId, canvasId, payload);
      return;
    }

    if (!host._matrixCanvas || !host._matrixResizeObserver) {
      this.mount(hostId, canvasId, payload);
      return;
    }

    host._matrixPayload = payload;
    host._matrixCanvas = canvas;
    host._matrixLastLayout = "";
    if (host._matrixRenderFrame) {
      cancelAnimationFrame(host._matrixRenderFrame);
    }
    host._matrixRenderFrame = requestAnimationFrame(() => {
      host._matrixRenderFrame = 0;
      if (!host._matrixPayload || !host._matrixCanvas) {
        return;
      }
      this.fitAndRender(host, host._matrixCanvas, host._matrixPayload);
    });
  },

  unmount(hostId) {
    const host = this.hostElement(hostId);
    if (!host) {
      return;
    }

    if (host._matrixRenderFrame) {
      cancelAnimationFrame(host._matrixRenderFrame);
      host._matrixRenderFrame = 0;
    }
    if (host._matrixResizeObserver) {
      host._matrixResizeObserver.disconnect();
      host._matrixResizeObserver = null;
    }
    host._matrixPayload = null;
    host._matrixCanvas = null;
    host._matrixLastLayout = "";
  },

  hitTest(canvasId, offsetX, offsetY) {
    const canvas = this.canvasElement(canvasId);
    if (!canvas) {
      return null;
    }

    const layout = canvas._matrixLayout;
    if (!layout) {
      return null;
    }

    const xi = Math.floor((offsetX - layout.gapPx) / (layout.cellW + layout.gapPx));
    const yi = Math.floor((offsetY - layout.gapPx) / (layout.cellH + layout.gapPx));
    if (xi < 0 || yi < 0 || xi >= layout.xCount || yi >= layout.yCount) {
      return null;
    }

    const value = layout.cells[yi][xi];
    return { xi, yi, value };
  },
};
