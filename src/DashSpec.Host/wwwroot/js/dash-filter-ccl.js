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

  function bindInput(input) {
    if (!input || bound.has(input)) {
      return;
    }

    bound.add(input);
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
