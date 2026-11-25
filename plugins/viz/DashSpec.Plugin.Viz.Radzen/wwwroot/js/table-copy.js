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

// Doxygen documentation portal scroll-spy & section scroll helper (ADR-0138).
// Scrolls .dox-main smoothly to the target section/row and highlights the active
// "On this page" TOC link (`[data-dox-toc-target="<id>"]`) as the user scrolls.
window.dashSpecDocPortal = (function () {
  var currentContainer = null;
  var onScrollHandler = null;
  var rafId = null;
  var pinnedId = null;
  var pinnedTimer = null;

  function setActiveTocId(activeId) {
    var links = document.querySelectorAll(".dox-toc [data-dox-toc-target]");
    for (var i = 0; i < links.length; i++) {
      var el = links[i];
      var target = el.getAttribute("data-dox-toc-target");
      if (target && target === activeId) {
        el.classList.add("is-active");
      } else {
        el.classList.remove("is-active");
      }
    }
  }

  function updateActiveOnScroll() {
    rafId = null;
    if (pinnedId) {
      setActiveTocId(pinnedId);
      return;
    }

    var main = document.querySelector(".dox-main");
    if (!main) return;

    var anchors = main.querySelectorAll("[data-dox-sec]");
    if (!anchors || anchors.length === 0) return;

    var mainRect = main.getBoundingClientRect();
    var threshold = mainRect.top + 88;
    var activeId = anchors[0].getAttribute("data-dox-sec");

    for (var i = 0; i < anchors.length; i++) {
      var a = anchors[i];
      var rect = a.getBoundingClientRect();
      if (rect.top <= threshold) {
        activeId = a.getAttribute("data-dox-sec");
      } else {
        break;
      }
    }

    if (activeId) {
      setActiveTocId(activeId);
    }
  }

  function bindScrollSpy() {
    var main = document.querySelector(".dox-main");
    if (!main) return;

    if (currentContainer !== main) {
      if (currentContainer && onScrollHandler) {
        currentContainer.removeEventListener("scroll", onScrollHandler);
      }
      currentContainer = main;
      onScrollHandler = function () {
        if (!rafId) {
          rafId = window.requestAnimationFrame(updateActiveOnScroll);
        }
      };
      currentContainer.addEventListener("scroll", onScrollHandler, { passive: true });
    }

    updateActiveOnScroll();
  }

  function scrollToSection(sectionId) {
    var main = document.querySelector(".dox-main");
    if (!main || !sectionId) return;

    var target = document.getElementById(sectionId) ||
                 main.querySelector('[data-dox-sec="' + sectionId + '"]');
    if (!target) return;

    pinnedId = sectionId;
    if (pinnedTimer) window.clearTimeout(pinnedTimer);
    pinnedTimer = window.setTimeout(function () {
      pinnedId = null;
    }, 550);

    setActiveTocId(sectionId);
    var mainRect = main.getBoundingClientRect();
    var targetRect = target.getBoundingClientRect();
    var topOffset = targetRect.top - mainRect.top + main.scrollTop - 16;
    main.scrollTo({ top: Math.max(0, topOffset), behavior: "smooth" });
  }

  return {
    initScrollSpy: bindScrollSpy,
    scrollToSection: scrollToSection
  };
})();

// Global <C-LMB> (Ctrl/Cmd + Left Mouse Button) modifier tracker for diagram clicks.
// RadzenChart's SeriesClickEventArgs does not expose MouseEventArgs, so we record
// the modifier state on capture-phase pointerdown/mousedown/click and read it in OnSeriesClick.
window.dashSpecClickModifiers = (function () {
  var lastCtrlOrMeta = false;
  var lastTimestamp = 0;

  function record(e) {
    if (!e) return;
    lastCtrlOrMeta = !!(e.ctrlKey || e.metaKey);
    lastTimestamp = Date.now();
  }

  document.addEventListener("pointerdown", record, true);
  document.addEventListener("mousedown", record, true);
  document.addEventListener("click", record, true);

  return {
    consumeCtrlOrMeta: function () {
      var active = lastCtrlOrMeta && (Date.now() - lastTimestamp < 1500);
      lastCtrlOrMeta = false;
      return active;
    }
  };
})();



