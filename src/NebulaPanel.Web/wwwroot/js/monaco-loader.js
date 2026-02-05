/**
 * Monaco Editor integration for Nebula Panel
 * Provides a simple API for creating and managing Monaco editor instances
 */

// Store for editor instances
const editors = new Map();
let monacoLoaded = false;
let monacoLoadPromise = null;

/**
 * Load Monaco Editor from CDN
 */
async function loadMonaco() {
    if (monacoLoaded) return;
    if (monacoLoadPromise) return monacoLoadPromise;

    monacoLoadPromise = new Promise((resolve, reject) => {
        // Check if require is already available
        if (typeof require !== 'undefined' && typeof require.config === 'function') {
            configureAndLoad(resolve, reject);
            return;
        }

        // Load require.js
        const script = document.createElement('script');
        script.src = 'https://cdnjs.cloudflare.com/ajax/libs/require.js/2.3.6/require.min.js';
        script.onload = () => configureAndLoad(resolve, reject);
        script.onerror = () => reject(new Error('Failed to load require.js'));
        document.head.appendChild(script);
    });

    await monacoLoadPromise;
    monacoLoaded = true;
}

function configureAndLoad(resolve, reject) {
    require.config({
        paths: {
            'vs': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs'
        }
    });

    // Load Monaco with worker support disabled for simplicity
    window.MonacoEnvironment = {
        getWorkerUrl: function () {
            return `data:text/javascript;charset=utf-8,${encodeURIComponent(`
                self.MonacoEnvironment = { baseUrl: 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/' };
                importScripts('https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs/base/worker/workerMain.js');
            `)}`;
        }
    };

    require(['vs/editor/editor.main'], function () {
        // Define custom theme matching Nebula dark theme
        monaco.editor.defineTheme('nebula-dark', {
            base: 'vs-dark',
            inherit: true,
            rules: [
                { token: 'comment', foreground: '6b7280' },
                { token: 'keyword', foreground: 'c084fc' },
                { token: 'string', foreground: '4ade80' },
                { token: 'number', foreground: 'fb923c' },
                { token: 'type', foreground: '60a5fa' },
            ],
            colors: {
                'editor.background': '#0a0a0f',
                'editor.foreground': '#f0f0f5',
                'editor.lineHighlightBackground': '#1a1a2e',
                'editor.selectionBackground': '#8b5cf644',
                'editorCursor.foreground': '#8b5cf6',
                'editorLineNumber.foreground': '#4b5563',
                'editorLineNumber.activeForeground': '#9ca3af',
            }
        });

        resolve();
    }, reject);
}

/**
 * Monaco Editor API exposed to Blazor
 */
window.monacoEditor = {
    /**
     * Create a new editor instance
     */
    async create(elementId, content, language, readOnly, wordWrap) {
        await loadMonaco();

        const container = document.getElementById(elementId);
        if (!container) {
            console.error(`Element not found: ${elementId}`);
            return;
        }

        // Dispose existing editor if any
        if (editors.has(elementId)) {
            editors.get(elementId).dispose();
        }

        const editor = monaco.editor.create(container, {
            value: content || '',
            language: language || 'plaintext',
            theme: 'nebula-dark',
            readOnly: readOnly || false,
            wordWrap: wordWrap ? 'on' : 'off',
            minimap: { enabled: true },
            automaticLayout: true,
            fontSize: 14,
            lineNumbers: 'on',
            scrollBeyondLastLine: false,
            roundedSelection: true,
            smoothScrolling: true,
            cursorBlinking: 'smooth',
            padding: { top: 10, bottom: 10 },
            scrollbar: {
                verticalScrollbarSize: 10,
                horizontalScrollbarSize: 10,
            },
        });

        editors.set(elementId, editor);
    },

    /**
     * Register Ctrl+S save handler
     */
    registerSaveHandler(elementId, dotNetRef) {
        const editor = editors.get(elementId);
        if (!editor) return;

        editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, function () {
            dotNetRef.invokeMethodAsync('HandleSaveShortcut');
        });
    },

    /**
     * Get editor content
     */
    getValue(elementId) {
        const editor = editors.get(elementId);
        return editor ? editor.getValue() : '';
    },

    /**
     * Set editor content
     */
    setValue(elementId, value) {
        const editor = editors.get(elementId);
        if (editor) {
            editor.setValue(value || '');
        }
    },

    /**
     * Set editor language
     */
    setLanguage(elementId, language) {
        const editor = editors.get(elementId);
        if (editor) {
            monaco.editor.setModelLanguage(editor.getModel(), language);
        }
    },

    /**
     * Set word wrap
     */
    setWordWrap(elementId, enabled) {
        const editor = editors.get(elementId);
        if (editor) {
            editor.updateOptions({ wordWrap: enabled ? 'on' : 'off' });
        }
    },

    /**
     * Dispose editor
     */
    dispose(elementId) {
        const editor = editors.get(elementId);
        if (editor) {
            editor.dispose();
            editors.delete(elementId);
        }
    }
};
