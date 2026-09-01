"use strict";
var aiguidersCcl = (() => {
  var __defProp = Object.defineProperty;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __export = (target, all) => {
    for (var name in all)
      __defProp(target, name, { get: all[name], enumerable: true });
  };
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

  // src/browser-entry.ts
  var browser_entry_exports = {};
  __export(browser_entry_exports, {
    bindInput: () => bindInput,
    isAcceptCompletion: () => isAcceptCompletion,
    preventDefaultWhenSuggestOpen: () => preventDefaultWhenSuggestOpen
  });

  // src/accept-keys.ts
  function isAcceptCompletion(state) {
    return state.key === "Tab" || state.key === " " && !!state.ctrlKey && !state.altKey && !state.metaKey && !state.shiftKey;
  }
  function preventDefaultWhenSuggestOpen(state, suggestOpen) {
    if (!suggestOpen) {
      return false;
    }
    if (state.key === "Tab") {
      return true;
    }
    return state.key === " " && !!state.ctrlKey && !state.altKey && !state.metaKey && !state.shiftKey;
  }

  // src/key-state.ts
  function keyStateFromEvent(event) {
    return {
      key: event.key,
      ctrlKey: event.ctrlKey,
      altKey: event.altKey,
      metaKey: event.metaKey,
      shiftKey: event.shiftKey
    };
  }

  // src/dom-binder.ts
  var bound = /* @__PURE__ */ new WeakSet();
  function readSuggestOpen(input) {
    return input.getAttribute("data-ccl-suggest") === "true";
  }
  function suppressBrowserAutocomplete(input) {
    input.setAttribute("autocomplete", "off");
    input.setAttribute("autocorrect", "off");
    input.setAttribute("autocapitalize", "off");
    input.setAttribute("spellcheck", "false");
    input.setAttribute("data-lpignore", "true");
    input.setAttribute("data-1p-ignore", "true");
    input.setAttribute("data-form-type", "other");
    input.setAttribute("name", "aiguiders-ccl-" + Math.random().toString(36).slice(2));
    if (input.dataset.cclAutocompleteBound === "true") {
      return;
    }
    input.dataset.cclAutocompleteBound = "true";
    input.setAttribute("readonly", "readonly");
    input.addEventListener("focus", () => {
      input.removeAttribute("readonly");
    });
    input.addEventListener("blur", () => {
      input.setAttribute("readonly", "readonly");
    });
  }
  function bindInput(input) {
    if (!input || typeof input.addEventListener !== "function" || bound.has(input)) {
      return;
    }
    if (!(input instanceof HTMLInputElement)) {
      return;
    }
    bound.add(input);
    suppressBrowserAutocomplete(input);
    input.addEventListener(
      "keydown",
      (event) => {
        const state = keyStateFromEvent(event);
        if (preventDefaultWhenSuggestOpen(state, readSuggestOpen(input))) {
          event.preventDefault();
        }
      },
      true
    );
  }
  return __toCommonJS(browser_entry_exports);
})();
