'use strict';

const vscode = require('vscode');
const path = require('path');
const fs = require('fs');
const { execFile } = require('child_process');
const {
  LanguageClient,
  TransportKind,
} = require('vscode-languageclient/node');

const DASHSPEC_EXTENSIONS = new Set([
  '.dashspec',
  '.dashdiagram',
  '.dashpresentation',
  '.dashlayout',
  '.dashcatalog',
  '.dashpalette',
  '.dashinclude',
]);

const DIAGNOSTIC_SOURCE = 'dashspec-cli';

/** @type {LanguageClient | undefined} */
let languageClient;

/** @type {Map<string, vscode.DiagnosticCollection>} */
const cliDiagnosticCollections = new Map();

function getConfig() {
  const cfg = vscode.workspace.getConfiguration('dashspec');
  return {
    dotnetPath: cfg.get('dotnetPath', 'dotnet'),
    hostDll: (cfg.get('hostDll', '') || '').trim(),
    validateOnSave: cfg.get('validateOnSave', false),
    languageServerEnabled: cfg.get('languageServerEnabled', true),
    languageServerPath: (cfg.get('languageServerPath', '') || '').trim(),
  };
}

function isDashSpecDocument(document) {
  const ext = path.extname(document.fileName).toLowerCase();
  return DASHSPEC_EXTENSIONS.has(ext);
}

function resolveLanguageServerDll(context) {
  const { languageServerPath } = getConfig();
  if (languageServerPath && fs.existsSync(languageServerPath)) {
    return languageServerPath;
  }

  const bundled = path.join(context.extensionPath, 'server', 'DashSpec.LanguageServer.dll');
  if (fs.existsSync(bundled)) {
    return bundled;
  }

  return '';
}

function startLanguageClient(context) {
  const { languageServerEnabled, dotnetPath } = getConfig();
  if (!languageServerEnabled) {
    return;
  }

  const serverDll = resolveLanguageServerDll(context);
  if (!serverDll) {
    vscode.window.showWarningMessage(
      'DashSpec: language server DLL not found. Run scripts/publish-language-server.ps1 or set dashspec.languageServerPath.',
    );
    return;
  }

  const serverOptions = {
    run: {
      command: dotnetPath,
      args: [serverDll],
      transport: TransportKind.stdio,
      options: { cwd: path.dirname(serverDll) },
    },
    debug: {
      command: dotnetPath,
      args: [serverDll],
      transport: TransportKind.stdio,
      options: { cwd: path.dirname(serverDll) },
    },
  };

  const clientOptions = {
    documentSelector: [{ language: 'dashspec' }],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher('**/*.{dashspec,dashdiagram,dashpresentation,dashlayout,dashcatalog,dashpalette,dashinclude}'),
    },
  };

  languageClient = new LanguageClient(
    'dashspec',
    'DashSpec Language Server',
    serverOptions,
    clientOptions,
  );

  context.subscriptions.push(languageClient.start());

  const version = context.extension.packageJSON.version;
  void vscode.window.setStatusBarMessage(`DashSpec ${version} LSP`, 4000);
}

async function restartLanguageServer(context) {
  if (languageClient) {
    await languageClient.stop();
    languageClient = undefined;
  }

  startLanguageClient(context);
  vscode.window.showInformationMessage('DashSpec language server restarted');
}

function runValidate(hostDll, filePath, dotnetPath) {
  return new Promise((resolve) => {
    execFile(
      dotnetPath,
      [hostDll, 'validate', filePath],
      { cwd: path.dirname(hostDll), maxBuffer: 1024 * 1024 },
      (error, _stdout, stderr) => {
        if (!error) {
          resolve({ ok: true, message: '' });
          return;
        }

        resolve({
          ok: false,
          message: (stderr || error.message || '').trim() || 'Validation failed',
        });
      },
    );
  });
}

function publishCliDiagnostics(document, result) {
  const collection = vscode.languages.createDiagnosticCollection(DIAGNOSTIC_SOURCE);

  if (result.ok) {
    collection.set(document.uri, []);
    return collection;
  }

  const line = 0;
  const range = new vscode.Range(line, 0, line, Math.max(1, document.lineAt(line).text.length));
  const diagnostic = new vscode.Diagnostic(range, result.message, vscode.DiagnosticSeverity.Error);
  diagnostic.source = DIAGNOSTIC_SOURCE;
  collection.set(document.uri, [diagnostic]);
  return collection;
}

async function validateDocument(document) {
  if (!isDashSpecDocument(document)) {
    return;
  }

  const { dotnetPath, hostDll } = getConfig();
  if (!hostDll) {
    return;
  }

  const filePath = document.uri.fsPath;
  const result = await runValidate(hostDll, filePath, dotnetPath);

  const prev = cliDiagnosticCollections.get(filePath);
  if (prev) {
    prev.dispose();
  }

  const collection = publishCliDiagnostics(document, result);
  cliDiagnosticCollections.set(filePath, collection);
}

async function validateActiveEditor() {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    vscode.window.showWarningMessage('DashSpec: no active editor');
    return;
  }

  const { hostDll } = getConfig();
  if (!hostDll) {
    vscode.window.showErrorMessage(
      'DashSpec: set dashspec.hostDll to DashSpec.Host.dll path in settings',
    );
    return;
  }

  await validateDocument(editor.document);
}

async function validateWorkspace() {
  const { hostDll } = getConfig();
  if (!hostDll) {
    vscode.window.showErrorMessage(
      'DashSpec: set dashspec.hostDll to DashSpec.Host.dll path in settings',
    );
    return;
  }

  if (!vscode.workspace.workspaceFolders?.length) {
    vscode.window.showWarningMessage('DashSpec: open a workspace folder first');
    return;
  }

  const files = await vscode.workspace.findFiles('**/*.{dashspec,dashdiagram,dashpresentation,dashlayout,dashcatalog,dashpalette,dashinclude}');
  let ok = 0;
  let failed = 0;

  await vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Notification,
      title: 'DashSpec validate',
      cancellable: false,
    },
    async () => {
      for (const uri of files) {
        const doc = await vscode.workspace.openTextDocument(uri);
        await validateDocument(doc);
        const coll = cliDiagnosticCollections.get(uri.fsPath);
        const hasError = coll && coll.get(uri)?.length;
        if (hasError) {
          failed++;
        } else {
          ok++;
        }
      }
    },
  );

  vscode.window.showInformationMessage(`DashSpec: ${ok} OK, ${failed} failed`);
}

function activate(context) {
  startLanguageClient(context);

  context.subscriptions.push(
    vscode.commands.registerCommand('dashspec.validate', validateActiveEditor),
    vscode.commands.registerCommand('dashspec.validateWorkspace', validateWorkspace),
    vscode.commands.registerCommand('dashspec.restartLanguageServer', () => restartLanguageClient(context)),
    vscode.workspace.onDidSaveTextDocument((doc) => {
      const { validateOnSave, hostDll } = getConfig();
      if (validateOnSave && hostDll) {
        void validateDocument(doc);
      }
    }),
  );
}

function deactivate() {
  for (const collection of cliDiagnosticCollections.values()) {
    collection.dispose();
  }
  cliDiagnosticCollections.clear();

  if (languageClient) {
    return languageClient.stop();
  }

  return undefined;
}

module.exports = { activate, deactivate };
