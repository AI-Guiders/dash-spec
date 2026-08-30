// CCL input: stop browser from stealing Tab / Ctrl+Space when completion is open.
window.dashFilterCcl = (function () {
  const bound = new WeakSet();

  function shouldPreventDefault(e, input) {
    if (input.getAttribute("data-ccl-suggest") !== "true") {
      return false;
    }

    if (e.key === "Tab") {
      return true;
    }

    return e.key === " " && e.ctrlKey && !e.altKey && !e.metaKey;
  }

  function suppressBrowserAutocomplete(input) {
    input.setAttribute("autocomplete", "off");
    input.setAttribute("autocorrect", "off");
    input.setAttribute("autocapitalize", "off");
    input.setAttribute("spellcheck", "false");
    input.setAttribute("data-lpignore", "true");
    input.setAttribute("data-1p-ignore", "true");
    input.setAttribute("data-form-type", "other");
    input.setAttribute("name", "dashspec-ccl-" + Math.random().toString(36).slice(2));

    if (input.dataset.cclAutocompleteBound === "true") {
      return;
    }

    input.dataset.cclAutocompleteBound = "true";
    input.setAttribute("readonly", "readonly");
    input.addEventListener("focus", function () {
      input.removeAttribute("readonly");
    });
    input.addEventListener("blur", function () {
      input.setAttribute("readonly", "readonly");
    });
  }

  function bindInput(input) {
    if (!input || typeof input.addEventListener !== "function" || bound.has(input)) {
      return;
    }

    bound.add(input);
    suppressBrowserAutocomplete(input);
    input.addEventListener(
      "keydown",
      function (e) {
        if (shouldPreventDefault(e, input)) {
          e.preventDefault();
        }
      },
      true
    );
  }

  return { bindInput };
})();
