window.dashspecDownload = {
  downloadText(fileName, text, mimeType) {
    const blob = new Blob([text], { type: mimeType || "text/csv;charset=utf-8" });
    this._triggerDownload(fileName || "export.csv", blob);
  },

  downloadBase64(fileName, base64, mimeType) {
    const binary = atob(base64 || "");
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }
    const blob = new Blob([bytes], {
      type: mimeType || "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    });
    this._triggerDownload(fileName || "export.xlsx", blob);
  },

  _triggerDownload(fileName, blob) {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  },
};
