// Table column copy for the Radzen grid (ADR-0115 §4): clipboard API first,
// execCommand fallback. Runs inside the user gesture; returns true on success.
window.dashSpecRadzenTableCopy = async function (text) {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch (err) {
    console.warn("[dashspec] clipboard API failed, falling back to execCommand:", err);
  }

  try {
    var area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "");
    area.style.position = "fixed";
    area.style.top = "0";
    area.style.left = "0";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.focus();
    area.select();
    var ok = document.execCommand("copy");
    document.body.removeChild(area);
    if (!ok) {
      console.error("[dashspec] execCommand copy returned false");
    }
    return ok;
  } catch (err) {
    console.error("[dashspec] table column copy failed:", err);
    return false;
  }
};
