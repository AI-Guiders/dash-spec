(function () {
    'use strict';

    let monacoReadyPromise = null;
    let editorInstance = null;
    let diffEditorInstance = null;
    let graphInstance = null;
    let dotnetRef = null;
    let currentRelativePath = '';
    let suppressChangeEvent = false;
    let languageRegistered = false;

    function ensureMonacoLoaded() {
        if (window.monaco && window.monaco.editor) {
            registerDashSpecLanguage(window.monaco);
            return Promise.resolve(window.monaco);
        }

        if (monacoReadyPromise) {
            return monacoReadyPromise;
        }

        monacoReadyPromise = new Promise(function (resolve, reject) {
            const localVsBase = '/_content/DashSpec.Plugin.Studio/lib/monaco/vs';
            const loaderUrl = localVsBase + '/loader.js';
            const script = document.createElement('script');
            script.src = loaderUrl;
            script.onload = function () {
                window.require.config({
                    paths: { vs: localVsBase }
                });
                window.require(['vs/editor/editor.main'], function () {
                    registerDashSpecLanguage(window.monaco);
                    resolve(window.monaco);
                });
            };
            script.onerror = function (err) {
                reject(err);
            };
            document.head.appendChild(script);
        });

        return monacoReadyPromise;
    }

    function registerDashSpecLanguage(monaco) {
        if (languageRegistered) {
            return;
        }
        languageRegistered = true;

        monaco.languages.register({
            id: 'dashspec',
            extensions: ['.ds', '.dashspec'],
            aliases: ['DashSpec', 'dashspec']
        });

        monaco.languages.setLanguageConfiguration('dashspec', {
            comments: { lineComment: '#' },
            brackets: [['{', '}'], ['[', ']']],
            autoClosingPairs: [
                { open: '{', close: '}' },
                { open: '[', close: ']' },
                { open: '"', close: '"', notIn: ['string'] }
            ],
            surroundingPairs: [
                { open: '{', close: '}' },
                { open: '[', close: ']' },
                { open: '"', close: '"' }
            ],
            folding: {
                markers: {
                    start: /^\s*(module|card|filter|report|phase|page|toolbar|filters|chrome|diagram|bar|line|heatmap|table|data|view|layout|bind|show|on click|when|source|transformer|ports|transform|defaults|host|surfaces|commands|bindings|phrases)\b/,
                    end: /^\s*end\s+/
                }
            }
        });

        // Pure 1:1 mapping from DashSpecSyntaxKind enum (0..9) via DocumentSemanticTokensProvider:
        // 0: Comment, 1: ModuleHeader, 2: Keyword, 3: EndKeyword, 4: String,
        // 5: Number, 6: Operator, 7: Identifier, 8: Punctuation, 9: Table
        const semanticLegend = {
            tokenTypes: [
                'dsComment',
                'dsModuleHeader',
                'dsKeyword',
                'dsEndKeyword',
                'dsString',
                'dsNumber',
                'dsOperator',
                'dsIdentifier',
                'dsPunctuation',
                'dsTable'
            ],
            tokenModifiers: []
        };

        monaco.languages.registerDocumentSemanticTokensProvider('dashspec', {
            getLegend: function () {
                return semanticLegend;
            },
            provideDocumentSemanticTokens: async function (model) {
                try {
                    const resp = await fetch('/api/studio/lsp/semantic-tokens', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            relativePath: currentRelativePath,
                            text: model.getValue(),
                            lineNumber: 1,
                            column: 1
                        })
                    });
                    if (!resp.ok) {
                        return { data: new Uint32Array(0) };
                    }
                    const payload = await resp.json();
                    return { data: new Uint32Array(payload.data || []) };
                } catch (e) {
                    return { data: new Uint32Array(0) };
                }
            },
            releaseDocumentSemanticTokens: function () {}
        });

        monaco.editor.defineTheme('dashspec-dark', {
            base: 'vs-dark',
            inherit: true,
            rules: [
                { token: 'dsComment', foreground: '64748B', fontStyle: 'italic' },
                { token: 'dsModuleHeader', foreground: 'C084FC', fontStyle: 'bold' },
                { token: 'dsKeyword', foreground: '60A5FA' },
                { token: 'dsEndKeyword', foreground: 'C084FC', fontStyle: 'bold' },
                { token: 'dsString', foreground: 'A3E635' },
                { token: 'dsNumber', foreground: 'FB923C' },
                { token: 'dsOperator', foreground: '38BDF8', fontStyle: 'bold' },
                { token: 'dsIdentifier', foreground: 'E2E8F0' },
                { token: 'dsPunctuation', foreground: '94A3B8' },
                { token: 'dsTable', foreground: '2DD4BF' }
            ],
            colors: {
                'editor.background': '#0B0F19',
                'editor.lineHighlightBackground': '#1E293B66',
                'editorLineNumber.foreground': '#475569',
                'editorGutter.background': '#0B0F19'
            }
        });

        // 1. Completion Provider (Pure LSP token-by-token completions)
        monaco.languages.registerCompletionItemProvider('dashspec', {
            triggerCharacters: [' ', '.', '"', '/', ':'],
            provideCompletionItems: async function (model, position) {
                const word = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: word.startColumn,
                    endColumn: word.endColumn
                };

                const suggestions = [];

                try {
                    const resp = await fetch('/api/studio/lsp/completions', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            relativePath: currentRelativePath,
                            text: model.getValue(),
                            lineNumber: position.lineNumber,
                            column: position.column
                        })
                    });
                    if (resp.ok) {
                        const items = await resp.json();
                        items.forEach(function (it) {
                            suggestions.push({
                                label: it.label,
                                kind: it.kind === 'diagram' || it.kind === 'flow' || it.kind === 'port' || it.kind === 'filter' || it.kind === 'card'
                                    ? monaco.languages.CompletionItemKind.Reference
                                    : it.kind === 'namespace'
                                        ? monaco.languages.CompletionItemKind.Module
                                        : it.kind === 'property'
                                            ? monaco.languages.CompletionItemKind.Property
                                            : it.kind === 'type'
                                                ? monaco.languages.CompletionItemKind.Struct
                                                : it.kind === 'value'
                                                    ? monaco.languages.CompletionItemKind.Value
                                                    : monaco.languages.CompletionItemKind.Keyword,
                                detail: it.detail || it.kind,
                                insertText: it.insertText,
                                range: range
                            });
                        });
                    }
                } catch (e) {
                    console.warn('[dashspecStudio] completion error:', e);
                }

                return { suggestions: suggestions };
            }
        });

        // 2. Go-to-Definition Provider (Ctrl+Click / F12 — navigates across files!)
        monaco.languages.registerDefinitionProvider('dashspec', {
            provideDefinition: async function (model, position) {
                try {
                    const resp = await fetch('/api/studio/lsp/definition', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            relativePath: currentRelativePath,
                            text: model.getValue(),
                            lineNumber: position.lineNumber,
                            column: position.column
                        })
                    });
                    if (!resp.ok) {
                        return null;
                    }
                    const def = await resp.json();
                    if (!def || !def.relativePath) {
                        return null;
                    }

                    if (def.relativePath.toLowerCase() !== currentRelativePath.toLowerCase() && dotnetRef) {
                        setTimeout(function () {
                            dotnetRef.invokeMethodAsync('OnNavigateToDefinitionAsync', def.relativePath, def.lineNumber, def.column);
                        }, 25);
                    }

                    return {
                        uri: model.uri,
                        range: new monaco.Range(def.lineNumber, def.column, def.lineNumber, def.column + 1)
                    };
                } catch (e) {
                    return null;
                }
            }
        });

        // 3. Document Symbol Provider (Outline & Breadcrumbs)
        monaco.languages.registerDocumentSymbolProvider('dashspec', {
            provideDocumentSymbols: async function (model) {
                try {
                    const resp = await fetch('/api/studio/lsp/symbols', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            relativePath: currentRelativePath,
                            text: model.getValue(),
                            lineNumber: 1,
                            column: 1
                        })
                    });
                    if (!resp.ok) {
                        return [];
                    }
                    const syms = await resp.json();
                    return syms.map(function (s) {
                        const r = new monaco.Range(s.startLineNumber, s.startColumn, s.endLineNumber, s.endColumn);
                        return {
                            name: s.name,
                            detail: s.kind,
                            kind: monaco.languages.SymbolKind.Class,
                            range: r,
                            selectionRange: r
                        };
                    });
                } catch (e) {
                    return [];
                }
            }
        });

        // 4. Reference Provider (Shift+F12 / Find All References via DashSpecLanguageBackend.FindUsagesAsync)
        monaco.languages.registerReferenceProvider('dashspec', {
            provideReferences: async function (model, position) {
                try {
                    const resp = await fetch('/api/studio/lsp/references', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            relativePath: currentRelativePath,
                            text: model.getValue(),
                            lineNumber: position.lineNumber,
                            column: position.column
                        })
                    });
                    if (!resp.ok) {
                        return [];
                    }
                    const refs = await resp.json();
                    if (dotnetRef) {
                        setTimeout(function () {
                            dotnetRef.invokeMethodAsync('OnFindReferencesAtCursorAsync', position.lineNumber, position.column);
                        }, 15);
                    }
                    return refs
                        .filter(function (r) {
                            return (r.relativePath || '').toLowerCase() === currentRelativePath.toLowerCase();
                        })
                        .map(function (r) {
                            return {
                                uri: model.uri,
                                range: new monaco.Range(r.lineNumber, r.column, r.lineNumber, r.column + 1)
                            };
                        });
                } catch (e) {
                    return [];
                }
            }
        });

        // 5. Hover Provider (via DashSpecLanguageBackend.GetSymbolAtPositionAsync + GoToDefinitionAsync)
        monaco.languages.registerHoverProvider('dashspec', {
            provideHover: async function (model, position) {
                try {
                    const resp = await fetch('/api/studio/lsp/hover', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            relativePath: currentRelativePath,
                            text: model.getValue(),
                            lineNumber: position.lineNumber,
                            column: position.column
                        })
                    });
                    if (!resp.ok) {
                        return null;
                    }
                    const hover = await resp.json();
                    if (!hover || !hover.title) {
                        return null;
                    }
                    return {
                        contents: [
                            { value: '**`' + hover.title + '`**' },
                            { value: hover.detail || '' }
                        ]
                    };
                } catch (e) {
                    return null;
                }
            }
        });

        // 6. Rename Provider (F2 via DashSpecLanguageBackend.RenameSymbolAsync)
        monaco.languages.registerRenameProvider('dashspec', {
            provideRenameEdits: async function (model, position, newName) {
                try {
                    const resp = await fetch('/api/studio/lsp/rename', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            relativePath: currentRelativePath,
                            text: model.getValue(),
                            lineNumber: position.lineNumber,
                            column: position.column,
                            newName: newName
                        })
                    });
                    if (!resp.ok) {
                        return { edits: [] };
                    }
                    const res = await resp.json();
                    if (!res || !res.newText || res.newText === model.getValue()) {
                        return { edits: [] };
                    }
                    const lineCount = model.getLineCount();
                    return {
                        edits: [
                            {
                                resource: model.uri,
                                textEdit: {
                                    range: new monaco.Range(1, 1, lineCount, model.getLineMaxColumn(lineCount)),
                                    text: res.newText
                                }
                            }
                        ]
                    };
                } catch (e) {
                    return { edits: [] };
                }
            }
        });
    }

    window.dashspecStudio = {
        mountEditor: async function (containerId, relativePath, initialText, markersJson, dotnet) {
            dotnetRef = dotnet;
            currentRelativePath = relativePath;
            const monaco = await ensureMonacoLoaded();
            const container = document.getElementById(containerId);
            if (!container) {
                return;
            }

            if (editorInstance && (!editorInstance.getDomNode() || !container.contains(editorInstance.getDomNode()))) {
                editorInstance.dispose();
                editorInstance = null;
            }

            if (!editorInstance) {
                editorInstance = monaco.editor.create(container, {
                    value: initialText || '',
                    language: 'dashspec',
                    theme: 'dashspec-dark',
                    automaticLayout: true,
                    fontSize: 13.5,
                    fontFamily: '"Cascadia Code", Consolas, ui-monospace, monospace',
                    minimap: { enabled: false },
                    scrollBeyondLastLine: false,
                    tabSize: 2,
                    insertSpaces: true,
                    'semanticHighlighting.enabled': true,
                    bracketPairColorization: { enabled: true },
                    guides: { indentation: true, bracketPairs: true }
                });

                editorInstance.addAction({
                    id: 'dashspec-focus-symbol-graph',
                    label: 'DashSpec: Показать связи символа на графе',
                    keybindings: [monaco.KeyMod.Alt | monaco.KeyCode.KeyG],
                    contextMenuGroupId: 'navigation',
                    contextMenuOrder: 1.5,
                    run: function (ed) {
                        const pos = ed.getPosition();
                        if (pos && dotnetRef) {
                            dotnetRef.invokeMethodAsync('OnFocusSymbolGraphAtCursorAsync', pos.lineNumber, pos.column);
                        }
                    }
                });

                editorInstance.addAction({
                    id: 'dashspec-find-all-references',
                    label: 'DashSpec: Find All References across Project',
                    contextMenuGroupId: 'navigation',
                    contextMenuOrder: 1.6,
                    run: function (ed) {
                        const pos = ed.getPosition();
                        if (pos && dotnetRef) {
                            dotnetRef.invokeMethodAsync('OnFindReferencesAtCursorAsync', pos.lineNumber, pos.column);
                        }
                    }
                });

                let cursorTimer = null;
                editorInstance.onDidChangeCursorPosition(function (e) {
                    if (suppressChangeEvent || !dotnetRef || !e || !e.position) {
                        return;
                    }
                    if (cursorTimer) {
                        clearTimeout(cursorTimer);
                    }
                    const ln = e.position.lineNumber;
                    const col = e.position.column;
                    cursorTimer = setTimeout(function () {
                        try {
                            dotnetRef.invokeMethodAsync('OnMonacoCursorChangedAsync', ln, col);
                        } catch (err) {}
                    }, 120);
                });

                let debounceTimer = null;
                editorInstance.onDidChangeModelContent(function () {
                    if (suppressChangeEvent) {
                        return;
                    }
                    if (debounceTimer) {
                        clearTimeout(debounceTimer);
                    }
                    const val = editorInstance.getValue();
                    debounceTimer = setTimeout(async function () {
                        // 1. Direct HTTP LSP diagnostics check so red squiggles work even if SignalR circuit lags
                        try {
                            const resp = await fetch('/api/studio/lsp/diagnostics', {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify({
                                    relativePath: currentRelativePath,
                                    text: val,
                                    lineNumber: 1,
                                    column: 1
                                })
                            });
                            if (resp.ok) {
                                const diags = await resp.json();
                                window.dashspecStudio.setMarkers(JSON.stringify(diags));
                            }
                        } catch (e) {
                            console.warn('[dashspecStudio] diagnostics fetch error:', e);
                        }

                        // 2. Notify Blazor Server component for Problems Drawer & Draft badge
                        if (dotnetRef) {
                            try {
                                await dotnetRef.invokeMethodAsync('OnMonacoTextChangedAsync', val);
                            } catch (e) {
                                console.warn('[dashspecStudio] Blazor OnMonacoTextChangedAsync error:', e);
                            }
                        }
                    }, 180);
                });
            } else {
                if (editorInstance.getValue() !== initialText) {
                    suppressChangeEvent = true;
                    const pos = editorInstance.getPosition();
                    editorInstance.setValue(initialText || '');
                    if (pos) {
                        editorInstance.setPosition(pos);
                    }
                    suppressChangeEvent = false;
                }
            }

            this.setMarkers(markersJson);
        },

        setValue: function (text, markersJson, relativePath) {
            if (relativePath) {
                currentRelativePath = relativePath;
            }
            if (editorInstance && editorInstance.getValue() !== text) {
                suppressChangeEvent = true;
                const pos = editorInstance.getPosition();
                editorInstance.setValue(text || '');
                if (pos) {
                    editorInstance.setPosition(pos);
                }
                suppressChangeEvent = false;
            }
            if (markersJson) {
                this.setMarkers(markersJson);
            }
        },

        revealLine: function (lineNumber, column) {
            if (!editorInstance) {
                return;
            }
            const ln = Math.max(1, lineNumber || 1);
            const col = Math.max(1, column || 1);
            editorInstance.revealLineInCenter(ln);
            editorInstance.setPosition({ lineNumber: ln, column: col });
            editorInstance.focus();
        },

        setMarkers: function (markersJson) {
            if (!window.monaco || !editorInstance) {
                return;
            }
            const model = editorInstance.getModel();
            if (!model) {
                return;
            }
            try {
                const raw = JSON.parse(markersJson || '[]');
                const markers = raw.map(function (m) {
                    return {
                        startLineNumber: m.startLineNumber || 1,
                        startColumn: m.startColumn || 1,
                        endLineNumber: m.endLineNumber || m.startLineNumber || 1,
                        endColumn: m.endColumn || (m.startColumn + 1) || 2,
                        message: m.message,
                        code: m.code || 'DSPEC',
                        severity: m.severity === 'error'
                            ? window.monaco.MarkerSeverity.Error
                            : m.severity === 'warning'
                                ? window.monaco.MarkerSeverity.Warning
                                : window.monaco.MarkerSeverity.Info
                    };
                });
                window.monaco.editor.setModelMarkers(model, 'dashspec-lsp', markers);
            } catch (e) {
                console.warn('[dashspecStudio] setMarkers error:', e);
            }
        },

        mountDiffEditor: async function (containerId, diskText, draftText) {
            const monaco = await ensureMonacoLoaded();
            const container = document.getElementById(containerId);
            if (!container) {
                return;
            }

            if (diffEditorInstance) {
                diffEditorInstance.dispose();
                diffEditorInstance = null;
            }

            container.innerHTML = '';
            diffEditorInstance = monaco.editor.createDiffEditor(container, {
                theme: 'dashspec-dark',
                automaticLayout: true,
                readOnly: true,
                renderSideBySide: true,
                fontSize: 12.5,
                fontFamily: '"Cascadia Code", Consolas, monospace',
                minimap: { enabled: false }
            });

            const originalModel = monaco.editor.createModel(diskText || '', 'dashspec');
            const modifiedModel = monaco.editor.createModel(draftText || '', 'dashspec');
            diffEditorInstance.setModel({
                original: originalModel,
                modified: modifiedModel
            });
        },

        renderGraph: function (containerId, optionJson) {
            try {
                const container = document.getElementById(containerId);
                if (!container || !window.echarts) {
                    return;
                }
                if (graphInstance) {
                    graphInstance.dispose();
                    graphInstance = null;
                }
                graphInstance = window.echarts.init(container, null, { renderer: 'canvas' });
                const option = JSON.parse(optionJson);
                graphInstance.setOption(option);

                graphInstance.on('click', function (params) {
                    if (params.dataType === 'node' && params.data && params.data.name && dotnetRef) {
                        dotnetRef.invokeMethodAsync('OnGraphNodeClickedAsync', String(params.data.name));
                    }
                });

                if (container.__studioResizeObserver) {
                    container.__studioResizeObserver.disconnect();
                }
                container.__studioResizeObserver = new ResizeObserver(function () {
                    if (graphInstance) {
                        graphInstance.resize();
                    }
                });
                container.__studioResizeObserver.observe(container);
            } catch (e) {
                console.error('[dashspecStudio] renderGraph error:', e);
            }
        },

        disposeAll: function () {
            if (editorInstance) {
                editorInstance.dispose();
                editorInstance = null;
            }
            if (diffEditorInstance) {
                diffEditorInstance.dispose();
                diffEditorInstance = null;
            }
            if (graphInstance) {
                graphInstance.dispose();
                graphInstance = null;
            }
        }
    };
})();
