"use strict";
var aiguidersInput = (() => {
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
    commandLine: () => command_line_exports,
    keyboard: () => keyboard_exports,
    surfaces: () => surfaces
  });

  // src/keyboard/index.ts
  var keyboard_exports = {};
  __export(keyboard_exports, {
    isAcceptCompletion: () => isAcceptCompletion,
    keyStateFromEvent: () => keyStateFromEvent,
    preventDefaultWhenSuggestOpen: () => preventDefaultWhenSuggestOpen,
    shouldCapturePreventDefault: () => shouldCapturePreventDefault
  });

  // src/keyboard/accept-keys.ts
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

  // src/keyboard/command-line-keys.ts
  function shouldCapturePreventDefault(state, suggestOpen) {
    if (preventDefaultWhenSuggestOpen(state, suggestOpen)) {
      return true;
    }
    return suggestOpen && (state.key === "ArrowUp" || state.key === "ArrowDown");
  }

  // src/keyboard/key-state.ts
  function keyStateFromEvent(event) {
    return {
      key: event.key,
      ctrlKey: event.ctrlKey,
      altKey: event.altKey,
      metaKey: event.metaKey,
      shiftKey: event.shiftKey
    };
  }

  // src/surfaces/command-line/index.ts
  var command_line_exports = {};
  __export(command_line_exports, {
    bindInput: () => bindInput,
    evaluateCompletion: () => evaluateCompletion
  });

  // src/surfaces/command-line/dom-binder.ts
  var bound = /* @__PURE__ */ new WeakSet();
  function readSuggestOpen(input) {
    return input.getAttribute("data-command-line-suggest") === "true" || input.getAttribute("data-ccl-suggest") === "true";
  }
  function suppressBrowserAutocomplete(input) {
    input.setAttribute("autocomplete", "off");
    input.setAttribute("autocorrect", "off");
    input.setAttribute("autocapitalize", "off");
    input.setAttribute("spellcheck", "false");
    input.setAttribute("data-lpignore", "true");
    input.setAttribute("data-1p-ignore", "true");
    input.setAttribute("data-form-type", "other");
    input.setAttribute("name", "aiguiders-command-line-" + Math.random().toString(36).slice(2));
    if (input.dataset.commandLineAutocompleteBound === "true") {
      return;
    }
    input.dataset.commandLineAutocompleteBound = "true";
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
        if (shouldCapturePreventDefault(state, readSuggestOpen(input))) {
          event.preventDefault();
        }
      },
      true
    );
  }

  // ../ir-command/dist/index.js
  var CommandArgTailKind;
  (function(CommandArgTailKind2) {
    CommandArgTailKind2["None"] = "None";
    CommandArgTailKind2["Optional"] = "Optional";
    CommandArgTailKind2["Required"] = "Required";
    CommandArgTailKind2["Picker"] = "Picker";
    CommandArgTailKind2["ImplicitSelection"] = "ImplicitSelection";
    CommandArgTailKind2["ImplicitLineRange"] = "ImplicitLineRange";
  })(CommandArgTailKind || (CommandArgTailKind = {}));
  var CatalogPathRole;
  (function(CatalogPathRole2) {
    CatalogPathRole2["Canonical"] = "Canonical";
    CatalogPathRole2["Alias"] = "Alias";
  })(CatalogPathRole || (CatalogPathRole = {}));
  function domainOmittedInPath(fields) {
    return fields.pathRole === CatalogPathRole.Alias && fields.domain.length > 0;
  }
  function semanticFields(entry) {
    return {
      domain: entry.domain,
      object: entry.object,
      intent: entry.intent,
      pathRole: entry.pathRole
    };
  }
  function resolvedPickerChoices(entry) {
    return entry.argPickerChoices;
  }
  var implicitSelection = "implicit:selection";
  var implicitLineRange = "implicit:line_range";
  function parseArgTailKind(raw) {
    if (!raw || raw.trim().length === 0) {
      return CommandArgTailKind.Optional;
    }
    const t = raw.trim();
    const lower = t.toLowerCase();
    if (lower === "none")
      return CommandArgTailKind.None;
    if (lower === "required")
      return CommandArgTailKind.Required;
    if (lower === "optional")
      return CommandArgTailKind.Optional;
    if (t.localeCompare(implicitSelection, void 0, { sensitivity: "accent" }) === 0) {
      return CommandArgTailKind.ImplicitSelection;
    }
    if (t.localeCompare(implicitLineRange, void 0, { sensitivity: "accent" }) === 0) {
      return CommandArgTailKind.ImplicitLineRange;
    }
    const pickerPrefix = ["suggest:", "picker+constructor:", "picker:"];
    if (pickerPrefix.some((p) => lower.startsWith(p))) {
      return CommandArgTailKind.Picker;
    }
    return CommandArgTailKind.Optional;
  }
  function extractSuggestionId(raw) {
    if (!raw || raw.trim().length === 0) {
      return void 0;
    }
    let text = raw.trim();
    const lower = text.toLowerCase();
    if (lower.startsWith("picker+constructor:")) {
      text = text.slice("picker+constructor:".length).trim();
    } else if (lower.startsWith("suggest:")) {
      text = text.slice("suggest:".length).trim();
    } else if (!lower.startsWith("picker:")) {
      return void 0;
    } else {
      text = text.slice("picker:".length).trim();
    }
    const plus = text.indexOf("+");
    const id = plus < 0 ? text : text.slice(0, plus).trim();
    return id.length === 0 ? void 0 : id;
  }

  // ../command-plane-catalog/dist/index.js
  function normalizePath(path) {
    let p = path.trim();
    if (p.startsWith("/")) {
      p = p.slice(1);
    }
    return p.trim();
  }
  function allPaths(entry) {
    const paths = [entry.path];
    for (const alias of entry.pathAliases ?? []) {
      if (alias.trim().length > 0) {
        paths.push(alias);
      }
    }
    return paths;
  }
  function buildCatalogIndex(entries) {
    const map = /* @__PURE__ */ new Map();
    const routes = [];
    for (const descriptor of entries) {
      const canonicalPath = normalizePath(descriptor.path);
      for (const path of allPaths(descriptor)) {
        const key = normalizePath(path);
        if (key.length === 0) {
          continue;
        }
        const pathRole = key.toLowerCase() === canonicalPath.toLowerCase() ? CatalogPathRole.Canonical : CatalogPathRole.Alias;
        const route = {
          path: key,
          commandId: descriptor.commandId,
          help: descriptor.help ?? "",
          group: descriptor.group,
          argTail: descriptor.argTail ?? "optional",
          argTailKind: parseArgTailKind(descriptor.argTail),
          domain: descriptor.domain ?? "",
          object: descriptor.object ?? "",
          intent: descriptor.intent ?? "",
          pathRole,
          argHint: descriptor.argHint,
          argPickerChoices: descriptor.argPickerChoices ?? []
        };
        map.set(key.toLowerCase(), route);
        routes.push(route);
      }
    }
    return {
      routes,
      tryGet(path) {
        return map.get(normalizePath(path).toLowerCase());
      },
      tryResolveLongestPrefix(tokens, endsWithSpace) {
        if (tokens.length === 0) {
          return void 0;
        }
        for (let take = tokens.length; take >= 1; take--) {
          const candidate = tokens.slice(0, take).join(" ");
          const route = map.get(candidate.toLowerCase());
          if (!route) {
            continue;
          }
          const isExactPath = take === tokens.length && endsWithSpace;
          const endsWithSpaceAfterPath = take < tokens.length ? false : endsWithSpace;
          const argTail = take < tokens.length ? tokens.slice(take).join(" ") : "";
          return {
            canonicalPath: candidate,
            argTail,
            isExactPath,
            endsWithSpaceAfterPath,
            entry: route
          };
        }
        return void 0;
      }
    };
  }

  // ../ir-invocation/dist/index.js
  var InvocationLinePhase;
  (function(InvocationLinePhase2) {
    InvocationLinePhase2["Path"] = "Path";
    InvocationLinePhase2["Arg"] = "Arg";
    InvocationLinePhase2["Ready"] = "Ready";
  })(InvocationLinePhase || (InvocationLinePhase = {}));
  var ArgMechanic;
  (function(ArgMechanic2) {
    ArgMechanic2["Picker"] = "Picker";
    ArgMechanic2["FreeText"] = "FreeText";
    ArgMechanic2["Optional"] = "Optional";
    ArgMechanic2["Constructor"] = "Constructor";
    ArgMechanic2["TypedInput"] = "TypedInput";
  })(ArgMechanic || (ArgMechanic = {}));
  var ArgCompletionItemKind;
  (function(ArgCompletionItemKind2) {
    ArgCompletionItemKind2["Segment"] = "Segment";
    ArgCompletionItemKind2["Picker"] = "Picker";
    ArgCompletionItemKind2["ConstructorEntry"] = "ConstructorEntry";
    ArgCompletionItemKind2["ConstructorStep"] = "ConstructorStep";
  })(ArgCompletionItemKind || (ArgCompletionItemKind = {}));
  function createArgCompletionItem(insertText, commandPath, help, group, stepSegment, kind = ArgCompletionItemKind.Segment, pickValue) {
    return {
      insertText,
      commandPath,
      help,
      group: group ?? null,
      stepSegment: stepSegment ?? null,
      kind,
      pickValue: pickValue ?? null
    };
  }
  function guidanceMode(guidance) {
    if (guidance.phase === InvocationLinePhase.Path)
      return InvocationLinePhase.Path;
    if (guidance.phase === InvocationLinePhase.Ready)
      return InvocationLinePhase.Ready;
    return guidance.argMechanic ?? guidance.phase;
  }

  // ../notations-slash/dist/slash-command-notation.js
  function parseSlashBody(body) {
    const endsWithSpaceAfterTokens = body.endsWith(" ");
    const tokens = body.split(" ").filter((t) => t.length > 0);
    return { tokens, endsWithSpaceAfterTokens };
  }

  // ../command-plane-slash/dist/slash-completion-sort.js
  function orderCompletionItems(items) {
    return [...items].sort((a, b) => sortKey(a.commandPath).localeCompare(sortKey(b.commandPath), void 0, {
      sensitivity: "base"
    }));
  }
  function sortKey(slashPath) {
    let path = slashPath.trim();
    if (path.startsWith("/")) {
      path = path.slice(1);
    }
    return path.toLowerCase();
  }

  // ../command-plane-slash/dist/slash-line-resolver.js
  function buildResolution(canonicalPath, argTail, argTailKind, isExactPathMatch, endsWithSpaceAfterPath) {
    const hasArgTailContent = argTail.trim().length > 0;
    const shouldHideSegmentSuggestions = argTailKind === CommandArgTailKind.None && isExactPathMatch || argTailKind === CommandArgTailKind.Optional && (isExactPathMatch || endsWithSpaceAfterPath || hasArgTailContent) || argTailKind === CommandArgTailKind.Required && hasArgTailContent || argTailKind === CommandArgTailKind.Picker && (endsWithSpaceAfterPath || hasArgTailContent);
    return {
      canonicalPath,
      argTail,
      argTailKind,
      isCatalogMatch: true,
      isExactPathMatch,
      endsWithSpaceAfterPath,
      hasArgTailContent,
      shouldHideSegmentSuggestions,
      isRunnable: argTailKind !== CommandArgTailKind.Required || hasArgTailContent
    };
  }
  function parseTypedBody(body) {
    const wire = parseSlashBody(body);
    return { tokens: wire.tokens, endsWithSpace: wire.endsWithSpaceAfterTokens };
  }
  function tryResolveBody(body, catalog) {
    const { tokens, endsWithSpace } = parseTypedBody(body);
    if (tokens.length === 0) {
      return null;
    }
    const resolved = catalog.tryResolveLongestPrefix(tokens, endsWithSpace);
    if (!resolved) {
      return null;
    }
    return buildResolution(resolved.canonicalPath, resolved.argTail, resolved.entry.argTailKind, resolved.isExactPath, resolved.endsWithSpaceAfterPath);
  }

  // ../command-plane-slash/dist/catalog-path-completion.js
  var snapshots = /* @__PURE__ */ new WeakMap();
  function splitPath(slashPath) {
    let path = slashPath.trim();
    if (path.startsWith("/")) {
      path = path.slice(1);
    }
    return path.length === 0 ? [] : path.split(" ").filter((s) => s.length > 0);
  }
  function buildSnapshot(catalog) {
    const allRoutes = [];
    let hasSemanticStructure = false;
    for (const route of catalog.routes) {
      const pathSegs = splitPath(route.path);
      if (pathSegs.length === 0) {
        continue;
      }
      allRoutes.push({
        route,
        pathSegments: pathSegs,
        commandPath: `/${pathSegs.join(" ")}`,
        help: route.help,
        group: route.group
      });
      const sem = semanticFields(route);
      if (domainOmittedInPath(sem) && route.object.length > 0) {
        hasSemanticStructure = true;
      } else if (route.domain.length > 0) {
        hasSemanticStructure = true;
      }
    }
    return { allRoutes, hasSemanticStructure };
  }
  function getSnapshot(catalog) {
    let snap = snapshots.get(catalog);
    if (!snap) {
      snap = buildSnapshot(catalog);
      snapshots.set(catalog, snap);
    }
    return snap;
  }
  function prefixMatches(segs, prefixTokens2) {
    if (prefixTokens2.length > segs.length) {
      return false;
    }
    for (let i = 0; i < prefixTokens2.length; i++) {
      if (segs[i]?.toLowerCase() !== prefixTokens2[i]?.toLowerCase()) {
        return false;
      }
    }
    return true;
  }
  function hasChildSegments(snap, tokens) {
    for (const route of snap.allRoutes) {
      if (route.pathSegments.length <= tokens.length) {
        continue;
      }
      if (prefixMatches(route.pathSegments, tokens)) {
        return true;
      }
    }
    return false;
  }
  function getCatalogPathSuggestionsFromTokens(catalog, tokens, endsWithSpace, typedBody) {
    const snap = getSnapshot(catalog);
    return getFlatPathSuggestions(catalog, snap, [...tokens], endsWithSpace, typedBody);
  }
  function getFlatPathSuggestions(_catalog, snap, tokens, endsWithSpace, _typedBody) {
    let ends = endsWithSpace;
    if (!ends && tokens.length > 0 && hasChildSegments(snap, tokens)) {
      ends = true;
    }
    const depth = ends ? tokens.length : Math.max(0, tokens.length - 1);
    const partial = ends || tokens.length === 0 ? "" : tokens[tokens.length - 1] ?? "";
    const prefixTokens2 = ends ? tokens : tokens.slice(0, Math.max(0, tokens.length - 1));
    const seen = /* @__PURE__ */ new Set();
    const list = [];
    for (const route of snap.allRoutes) {
      const segs = route.pathSegments;
      if (segs.length <= depth) {
        continue;
      }
      if (!prefixMatches(segs, prefixTokens2)) {
        continue;
      }
      const next = segs[depth] ?? "";
      if (partial.length > 0 && !next.toLowerCase().startsWith(partial.toLowerCase())) {
        continue;
      }
      const seenKey = next.toLowerCase();
      if (seen.has(seenKey)) {
        continue;
      }
      seen.add(seenKey);
      const insertSegs = [...prefixTokens2, next];
      const slashPath = `/${insertSegs.join(" ")}`;
      const more = segs.length > depth + 1 || route.route.argTailKind !== CommandArgTailKind.None;
      const insert = slashPath + (more ? " " : "");
      const help = segs.length === depth + 1 ? route.help : `${route.commandPath} \u2014 ${route.help}`;
      list.push(createArgCompletionItem(insert, route.commandPath, help, route.group ?? null, next));
    }
    return orderCompletionItems(list);
  }
  function usesFlatPaths(catalog) {
    return !getSnapshot(catalog).hasSemanticStructure;
  }

  // ../command-plane-slash/dist/slash-arg-completion.js
  function shouldCompleteArg(line, route) {
    return route.argTailKind !== CommandArgTailKind.None && line.isCatalogMatch && (line.isExactPathMatch || line.endsWithSpaceAfterPath || line.hasArgTailContent || route.argTailKind === CommandArgTailKind.Picker);
  }
  function getArgSuggestions(line, route, suggestionBroker) {
    const partial = line.argTail.trim();
    const choices = resolveChoices(line, route, partial, suggestionBroker);
    if (choices.length === 0) {
      return [];
    }
    return buildPickerItems(line, route, choices, partial);
  }
  function hasArgChoices(route, partial, suggestionBroker) {
    return resolveChoices(null, route, partial, suggestionBroker).length > 0;
  }
  function resolveChoices(line, route, partial, suggestionBroker) {
    const staticChoices = resolvedPickerChoices(route);
    if (staticChoices.length > 0) {
      return staticChoices;
    }
    if (route.argTailKind !== CommandArgTailKind.Picker || !suggestionBroker) {
      return [];
    }
    const suggestionId = extractSuggestionId(route.argTail);
    if (!suggestionId) {
      return [];
    }
    const canonicalPath = line?.canonicalPath ?? route.path;
    return suggestionBroker.getSuggestions(suggestionId, partial, route, canonicalPath);
  }
  function buildPickerItems(line, route, choices, partial) {
    const canonicalPath = `/${line.canonicalPath.trimStart()}`;
    const buckets = /* @__PURE__ */ new Map();
    for (const choice of choices) {
      if (!matchesPickerChoice(choice, partial)) {
        continue;
      }
      const value = choice.value.trim();
      if (value.length === 0) {
        continue;
      }
      const label = choice.label?.trim() || value;
      const insert = `${canonicalPath} ${value}`;
      const help = choice.hint?.trim() || label;
      addPickerSuggestion(buckets, label, insert, canonicalPath, help, route.group, value);
    }
    return orderCompletionItems(buckets.values());
  }
  function addPickerSuggestion(buckets, listTitle, insert, slashPath, help, group, value) {
    const existing = buckets.get(listTitle.toLowerCase());
    if (!existing || slashPath.length >= existing.commandPath.length) {
      buckets.set(listTitle.toLowerCase(), createArgCompletionItem(insert, slashPath, help, group ?? null, listTitle, ArgCompletionItemKind.Picker, value));
    }
  }
  function matchesPickerChoice(choice, partial) {
    if (partial.length === 0) {
      return true;
    }
    const value = choice.value ?? "";
    const label = choice.label ?? "";
    const hint = choice.hint ?? "";
    return value.toLowerCase().startsWith(partial.toLowerCase()) || label.toLowerCase().includes(partial.toLowerCase()) || hint.toLowerCase().includes(partial.toLowerCase());
  }

  // ../command-plane-slash/dist/slash-input-guidance.js
  function resolveSlashInputGuidance(catalog, typedBody, suggestionBroker, items) {
    const body = typedBody.trimStart();
    const line = tryResolveBody(body, catalog);
    if (line) {
      const route = catalog.tryGet(line.canonicalPath);
      if (route) {
        const breadcrumb = buildBreadcrumb(line.canonicalPath, line.argTail);
        const argTailKind = route.argTailKind;
        if (shouldCompleteArg(line, route) && awaitingArgInput(line, route)) {
          return resolveArgGuidance(line, route, suggestionBroker, items, breadcrumb, argTailKind);
        }
        if (line.isRunnable) {
          return {
            breadcrumb,
            placeholder: "Press Enter to run",
            hint: route.help,
            phase: InvocationLinePhase.Ready,
            canonicalPath: line.canonicalPath,
            argTailKind,
            readyWire: line.argTail.trim(),
            displayTail: line.argTail.trim()
          };
        }
        if (shouldCompleteArg(line, route)) {
          return resolveArgGuidance(line, route, suggestionBroker, items, breadcrumb, argTailKind);
        }
      }
    }
    return resolvePathGuidance(body, items);
  }
  function awaitingArgInput(line, route) {
    switch (route.argTailKind) {
      case CommandArgTailKind.Required:
        return !line.hasArgTailContent;
      case CommandArgTailKind.Picker:
        return !line.hasArgTailContent;
      case CommandArgTailKind.Optional:
        return line.endsWithSpaceAfterPath && !line.hasArgTailContent;
      default:
        return false;
    }
  }
  function resolveArgGuidance(line, route, suggestionBroker, items, breadcrumb, argTailKind) {
    const partial = line.argTail.trim();
    const hasPickerSurface = route.argTailKind === CommandArgTailKind.Picker || extractSuggestionId(route.argTail) !== void 0;
    if (hasPickerSurface) {
      const hasChoices = items.length > 0 || hasArgChoices(route, partial, suggestionBroker);
      const hint = route.argHint ?? (hasChoices ? "Choose a value \u2014 Tab to insert" : "Type to filter choices");
      const placeholder = route.argHint ?? (hasChoices ? "Pick a value" : "Type to filter choices");
      return {
        breadcrumb,
        placeholder,
        hint,
        phase: InvocationLinePhase.Arg,
        argMechanic: ArgMechanic.Picker,
        canonicalPath: line.canonicalPath,
        argTailKind,
        displayTail: partial.length > 0 ? partial : null
      };
    }
    switch (route.argTailKind) {
      case CommandArgTailKind.Required:
        return {
          breadcrumb,
          placeholder: formatFreeTextPlaceholder(route.argHint),
          hint: route.argHint ?? "Type the required argument and press Enter",
          phase: InvocationLinePhase.Arg,
          argMechanic: ArgMechanic.FreeText,
          canonicalPath: line.canonicalPath,
          argTailKind
        };
      case CommandArgTailKind.Optional:
        return {
          breadcrumb,
          placeholder: route.argHint ?? "Optional argument \u2014 Enter to run",
          hint: route.argHint ?? "Add an argument or press Enter to run without it",
          phase: InvocationLinePhase.Arg,
          argMechanic: ArgMechanic.Optional,
          canonicalPath: line.canonicalPath,
          argTailKind
        };
      default:
        return {
          breadcrumb,
          placeholder: "Continue typing the command path",
          hint: route.help,
          phase: InvocationLinePhase.Path,
          canonicalPath: line.canonicalPath,
          argTailKind
        };
    }
  }
  function resolvePathGuidance(body, items) {
    const breadcrumb = body.length === 0 ? "/" : `/${body.trimEnd()}`;
    if (items.length > 0) {
      const next = items[0].stepSegment ?? items[0].commandPath.trimStart().slice(1);
      return {
        breadcrumb,
        placeholder: `Next: ${next}`,
        hint: items[0].help,
        phase: InvocationLinePhase.Path,
        canonicalPath: items[0].commandPath.trimStart().slice(1),
        argTailKind: CommandArgTailKind.None
      };
    }
    return {
      breadcrumb,
      placeholder: "Type a command path",
      hint: "Start typing \u2014 Tab completes the next segment",
      phase: InvocationLinePhase.Path,
      canonicalPath: null,
      argTailKind: CommandArgTailKind.None
    };
  }
  function formatFreeTextPlaceholder(argHint) {
    return argHint?.trim() ? `${argHint.trim()} (free text)` : "Type value (free text)";
  }
  function buildBreadcrumb(canonicalPath, argTail) {
    const segments = canonicalPath.split(" ").map((s) => s.trim()).filter((s) => s.length > 0);
    if (argTail.trim().length > 0) {
      segments.push(argTail.trim());
    }
    return `/${segments.join(" \u203A ")}`;
  }

  // ../command-plane-slash/dist/slash-step-completion.js
  var CompletionStep;
  (function(CompletionStep2) {
    CompletionStep2["Domain"] = "Domain";
    CompletionStep2["Object"] = "Object";
    CompletionStep2["Intent"] = "Intent";
    CompletionStep2["Arg"] = "Arg";
  })(CompletionStep || (CompletionStep = {}));
  var snapshots2 = /* @__PURE__ */ new WeakMap();
  function semanticKey(domain, object) {
    return `${domain.toLowerCase()}\0${object.toLowerCase()}`;
  }
  function splitPath2(slashPath) {
    let path = slashPath.trim();
    if (path.startsWith("/")) {
      path = path.slice(1);
    }
    return path.length === 0 ? [] : path.split(" ").filter((s) => s.length > 0);
  }
  function trackHelp(map, key, route) {
    const existing = map.get(key);
    if (!existing || route.path.length > existing.len) {
      map.set(key, { help: route.help, len: route.path.length });
    }
  }
  function buildSnapshot2(catalog) {
    const allRoutes = [];
    const domainsWithCanonicalPrefix = /* @__PURE__ */ new Set();
    const elisionObjectToDomain = /* @__PURE__ */ new Map();
    const objectsByDomain = /* @__PURE__ */ new Map();
    const flatIntentsByDomain = /* @__PURE__ */ new Map();
    const routesBySemantic = /* @__PURE__ */ new Map();
    const helpDomain = /* @__PURE__ */ new Map();
    const helpElision = /* @__PURE__ */ new Map();
    const helpObject = /* @__PURE__ */ new Map();
    for (const route of catalog.routes) {
      const sem = semanticFields(route);
      const pathSegs = splitPath2(route.path);
      if (pathSegs.length === 0) {
        continue;
      }
      const indexed = {
        route,
        semantics: sem,
        pathSegments: pathSegs,
        commandPath: `/${pathSegs.join(" ")}`,
        help: route.help,
        group: route.group
      };
      allRoutes.push(indexed);
      const key = semanticKey(sem.domain, sem.object ?? "");
      const list = routesBySemantic.get(key) ?? [];
      list.push(indexed);
      routesBySemantic.set(key, list);
      if (domainOmittedInPath(sem) && route.object.length > 0) {
        elisionObjectToDomain.set(route.object.toLowerCase(), sem.domain);
      } else if (sem.domain.length > 0) {
        domainsWithCanonicalPrefix.add(sem.domain.toLowerCase());
      }
      let objects = objectsByDomain.get(sem.domain.toLowerCase());
      if (!objects) {
        objects = /* @__PURE__ */ new Set();
        objectsByDomain.set(sem.domain.toLowerCase(), objects);
      }
      if (route.object.length > 0) {
        objects.add(route.object.toLowerCase());
      }
      if (route.object.length === 0 && sem.intent.length > 0) {
        let flat = flatIntentsByDomain.get(sem.domain.toLowerCase());
        if (!flat) {
          flat = /* @__PURE__ */ new Map();
          flatIntentsByDomain.set(sem.domain.toLowerCase(), flat);
        }
        const existing = flat.get(sem.intent.toLowerCase());
        if (!existing || route.path.length > existing.route.path.length) {
          flat.set(sem.intent.toLowerCase(), indexed);
        }
      }
      trackHelp(helpDomain, sem.domain.toLowerCase(), route);
      if (domainOmittedInPath(sem) && route.object.length > 0) {
        trackHelp(helpElision, route.object.toLowerCase(), route);
      }
      if (route.object.length > 0) {
        trackHelp(helpObject, semanticKey(sem.domain, route.object), route);
      }
    }
    return {
      allRoutes,
      domainsWithCanonicalPrefix,
      elisionObjectToDomain,
      objectsByDomain,
      flatIntentsByDomain,
      routesBySemantic,
      helpDomain,
      helpElision,
      helpObject
    };
  }
  function getSnapshot2(catalog) {
    let snap = snapshots2.get(catalog);
    if (!snap) {
      snap = buildSnapshot2(catalog);
      snapshots2.set(catalog, snap);
    }
    return snap;
  }
  function getSlashStepSuggestions(catalog, typedBody, suggestionBroker) {
    const { tokens, endsWithSpace } = parseTypedBody(typedBody);
    return getSlashStepSuggestionsFromTokens(catalog, tokens, endsWithSpace, typedBody, suggestionBroker);
  }
  function getSlashStepSuggestionsFromTokens(catalog, tokens, endsWithSpace, typedBody, suggestionBroker) {
    const line = tryResolveBody(typedBody, catalog);
    if (line) {
      const route = catalog.tryGet(line.canonicalPath);
      if (route && shouldCompleteArg(line, route)) {
        const argItems = getArgSuggestions(line, route, suggestionBroker);
        if (argItems.length > 0 || line.shouldHideSegmentSuggestions) {
          return argItems;
        }
      }
    }
    if (line?.shouldHideSegmentSuggestions) {
      return [];
    }
    if (usesFlatPaths(catalog)) {
      return getCatalogPathSuggestionsFromTokens(catalog, tokens, endsWithSpace, typedBody);
    }
    const snap = getSnapshot2(catalog);
    const state = resolveCompletionState(snap, tokens, endsWithSpace);
    switch (state.step) {
      case CompletionStep.Domain:
        return buildDomainSuggestions(snap, state.partialToken);
      case CompletionStep.Object:
        return buildObjectSuggestions(snap, state.domain, state.partialToken, tokens, endsWithSpace);
      case CompletionStep.Intent:
        return buildIntentSuggestions(snap, catalog, state.domain, state.object ?? "", state.partialToken, tokens, endsWithSpace);
      default:
        return [];
    }
  }
  function resolveCompletionState(snap, tokens, endsWithSpace) {
    if (tokens.length === 0) {
      return { step: CompletionStep.Domain, domain: null, object: null, partialToken: "" };
    }
    if (!endsWithSpace) {
      if (tokens.length === 1) {
        const t = tokens[0].toLowerCase();
        if (snap.domainsWithCanonicalPrefix.has(t)) {
          return { step: CompletionStep.Object, domain: tokens[0], object: null, partialToken: "" };
        }
        const elisionDomain = snap.elisionObjectToDomain.get(t);
        if (elisionDomain) {
          return {
            step: CompletionStep.Intent,
            domain: elisionDomain,
            object: tokens[0],
            partialToken: ""
          };
        }
        return { step: CompletionStep.Domain, domain: null, object: null, partialToken: tokens[0] };
      }
      const prefixOne = tryResolvePrefix(snap, prefixTokens(tokens, 1), true);
      if (prefixOne?.object) {
        return {
          step: CompletionStep.Intent,
          domain: prefixOne.domain,
          object: prefixOne.object,
          partialToken: tokens[tokens.length - 1]
        };
      }
      const domainOnlyPrefix = tryResolvePrefix(snap, [tokens[0]], true);
      if (tokens.length >= 2 && snap.domainsWithCanonicalPrefix.has(tokens[0].toLowerCase()) && domainOnlyPrefix && !domainOnlyPrefix.object) {
        return tokens.length === 2 ? {
          step: CompletionStep.Object,
          domain: domainOnlyPrefix.domain,
          object: null,
          partialToken: tokens[1]
        } : {
          step: CompletionStep.Intent,
          domain: domainOnlyPrefix.domain,
          object: "",
          partialToken: tokens[tokens.length - 1]
        };
      }
      return {
        step: CompletionStep.Domain,
        domain: null,
        object: null,
        partialToken: tokens[tokens.length - 1]
      };
    }
    if (tokens.length === 1) {
      const t0 = tokens[0].toLowerCase();
      if (snap.domainsWithCanonicalPrefix.has(t0)) {
        return { step: CompletionStep.Object, domain: tokens[0], object: null, partialToken: "" };
      }
      const elisionDomain = snap.elisionObjectToDomain.get(t0);
      if (elisionDomain) {
        return {
          step: CompletionStep.Intent,
          domain: elisionDomain,
          object: tokens[0],
          partialToken: ""
        };
      }
      return { step: CompletionStep.Domain, domain: null, object: null, partialToken: "" };
    }
    const twoTokenPrefix = tryResolvePrefix(snap, [...tokens], true);
    if (tokens.length === 2 && twoTokenPrefix) {
      if (twoTokenPrefix.object) {
        return {
          step: CompletionStep.Intent,
          domain: twoTokenPrefix.domain,
          object: twoTokenPrefix.object,
          partialToken: ""
        };
      }
      return { step: CompletionStep.Arg, domain: twoTokenPrefix.domain, object: "", partialToken: "" };
    }
    const multiPrefix = tryResolvePrefix(snap, [...tokens], true);
    if (tokens.length >= 3 && multiPrefix) {
      return {
        step: CompletionStep.Arg,
        domain: multiPrefix.domain,
        object: multiPrefix.object,
        partialToken: ""
      };
    }
    return { step: CompletionStep.Arg, domain: null, object: null, partialToken: "" };
  }
  function tryResolvePrefix(snap, tokens, endsWithSpace) {
    if (tokens.length === 0) {
      return null;
    }
    const t0 = tokens[0].toLowerCase();
    const elisionDomain = snap.elisionObjectToDomain.get(t0);
    if (elisionDomain) {
      if (tokens.length === 1) {
        return { domain: elisionDomain, object: tokens[0] };
      }
      return endsWithSpace ? { domain: elisionDomain, object: tokens[0] } : null;
    }
    if (!snap.domainsWithCanonicalPrefix.has(t0)) {
      return null;
    }
    if (tokens.length === 1) {
      return { domain: tokens[0], object: "" };
    }
    return { domain: tokens[0], object: tokens[1] };
  }
  function prefixTokens(tokens, dropLast) {
    if (dropLast <= 0) {
      return [...tokens];
    }
    const count = tokens.length - dropLast;
    return count <= 0 ? [] : tokens.slice(0, count);
  }
  function matchesPartial(value, partial) {
    return partial.length === 0 || value.toLowerCase().startsWith(partial.toLowerCase());
  }
  function buildDomainSuggestions(snap, partial) {
    const buckets = /* @__PURE__ */ new Map();
    for (const domain of snap.domainsWithCanonicalPrefix) {
      if (!matchesPartial(domain, partial)) {
        continue;
      }
      addSuggestion(buckets, domain, `/${domain} `, `/${domain}`, bestHelpForDomain(snap, domain));
    }
    for (const [starter, elisionDomain] of snap.elisionObjectToDomain) {
      if (!matchesPartial(starter, partial)) {
        continue;
      }
      addSuggestion(buckets, starter, `/${starter} `, `/${starter}`, bestHelpForElisionStarter(snap, starter, elisionDomain));
    }
    return orderCompletionItems(buckets.values());
  }
  function buildObjectSuggestions(snap, domain, partial, tokens, endsWithSpace) {
    const buckets = /* @__PURE__ */ new Map();
    const domainKey = domain.toLowerCase();
    const objects = snap.objectsByDomain.get(domainKey);
    if (objects) {
      for (const obj of objects) {
        if (!obj || !matchesPartial(obj, partial)) {
          continue;
        }
        const insertPath = `/${domain} ${obj} `;
        addSuggestion(buckets, obj, insertPath, insertPath.trimEnd(), bestHelpForObject(snap, domain, obj));
      }
    }
    const flatIntents = snap.flatIntentsByDomain.get(domainKey);
    if (flatIntents) {
      for (const [intent, route] of flatIntents) {
        if (!matchesPartial(intent, partial)) {
          continue;
        }
        const pathSegs = route.pathSegments;
        const insert = buildInsertFromTyped(null, tokens, endsWithSpace, pathSegs, pathSegs.length - 1, intent);
        addSuggestion(buckets, intent, insert, route.commandPath, route.help, route.group);
      }
    }
    return orderCompletionItems(buckets.values());
  }
  function buildIntentSuggestions(snap, catalog, domain, obj, partial, tokens, endsWithSpace) {
    const routes = snap.routesBySemantic.get(semanticKey(domain, obj));
    if (!routes) {
      return [];
    }
    const buckets = /* @__PURE__ */ new Map();
    const segmentIndex = endsWithSpace ? tokens.length : Math.max(0, tokens.length - 1);
    for (const route of routes) {
      const pathSegs = route.pathSegments;
      if (segmentIndex >= pathSegs.length) {
        continue;
      }
      if (!pathPrefixMatches(pathSegs, tokens, endsWithSpace)) {
        continue;
      }
      const segmentValue = pathSegs[segmentIndex] ?? "";
      if (!matchesPartial(segmentValue, partial)) {
        continue;
      }
      const insert = buildInsertFromTyped(catalog, tokens, endsWithSpace, pathSegs, segmentIndex, segmentValue);
      addSuggestion(buckets, segmentValue, insert, route.commandPath, route.help, route.group);
    }
    return orderCompletionItems(buckets.values());
  }
  function addSuggestion(buckets, listTitle, insert, slashPath, help, group) {
    const key = listTitle.toLowerCase();
    const existing = buckets.get(key);
    if (!existing || slashPath.length > existing.commandPath.length) {
      buckets.set(key, createArgCompletionItem(insert, slashPath, help, group ?? null, listTitle));
    }
  }
  function pathPrefixMatches(pathSegs, tokens, endsWithSpace) {
    if (tokens.length === 0) {
      return true;
    }
    if (endsWithSpace) {
      if (tokens.length >= pathSegs.length) {
        return false;
      }
      for (let i = 0; i < tokens.length; i++) {
        if (pathSegs[i]?.toLowerCase() !== tokens[i]?.toLowerCase()) {
          return false;
        }
      }
      return true;
    }
    if (tokens.length > pathSegs.length) {
      return false;
    }
    for (let i = 0; i < tokens.length - 1; i++) {
      if (pathSegs[i]?.toLowerCase() !== tokens[i]?.toLowerCase()) {
        return false;
      }
    }
    const last = tokens[tokens.length - 1] ?? "";
    const pathLast = pathSegs[tokens.length - 1] ?? "";
    return pathLast.toLowerCase().startsWith(last.toLowerCase());
  }
  function buildInsertFromTyped(catalog, typedTokens, _endsWithSpace, pathSegs, completeSegmentIndex, segmentValue) {
    const resultSegs = [];
    for (let i = 0; i < completeSegmentIndex; i++) {
      resultSegs.push(i < typedTokens.length ? typedTokens[i] : pathSegs[i]);
    }
    resultSegs.push(segmentValue);
    let slashPath = `/${resultSegs.join(" ")}`;
    if (completeSegmentIndex + 1 < pathSegs.length || catalog !== null && segmentNeedsArgTail(catalog, slashPath)) {
      slashPath += " ";
    }
    return slashPath;
  }
  function segmentNeedsArgTail(catalog, slashPath) {
    const line = tryResolveBody(slashPath.slice(1), catalog);
    if (line?.isExactPathMatch && line.argTailKind === CommandArgTailKind.None) {
      return false;
    }
    return line !== null && line.argTailKind !== CommandArgTailKind.None;
  }
  function bestHelpForDomain(snap, domain) {
    return snap.helpDomain.get(domain.toLowerCase())?.help ?? domain;
  }
  function bestHelpForElisionStarter(snap, starter, domain) {
    return snap.helpElision.get(starter.toLowerCase())?.help ?? `${starter} (${domain})`;
  }
  function bestHelpForObject(snap, domain, obj) {
    return snap.helpObject.get(semanticKey(domain, obj))?.help ?? obj;
  }

  // ../command-plane-slash/dist/slash-completion.js
  function getSlashCompletionResult(catalog, typedBody, suggestionBroker) {
    const items = getSlashStepSuggestions(catalog, typedBody, suggestionBroker);
    const guidance = resolveSlashInputGuidance(catalog, typedBody, suggestionBroker, items);
    return { items, guidance };
  }

  // ../command-plane-slash/dist/stub-arg-suggestion-broker.js
  function createStubArgSuggestionBroker(stubs) {
    const map = /* @__PURE__ */ new Map();
    if (stubs) {
      for (const [key, stub] of Object.entries(stubs)) {
        map.set(key, stub.choices);
      }
    }
    return {
      getSuggestions(suggestionId, _partial, _route, _canonicalPath) {
        return map.get(suggestionId) ?? [];
      }
    };
  }

  // src/surfaces/command-line/client-completion.ts
  function evaluateCompletion(catalogEntries, body, pickerStubs) {
    const catalog = buildCatalogIndex([...catalogEntries]);
    const broker = createStubArgSuggestionBroker(pickerStubs);
    const result = getSlashCompletionResult(catalog, body, broker);
    return {
      items: result.items,
      guidance: {
        ...result.guidance,
        mode: guidanceMode(result.guidance)
      }
    };
  }

  // src/browser-entry.ts
  var surfaces = {
    commandLine: command_line_exports
  };
  return __toCommonJS(browser_entry_exports);
})();
