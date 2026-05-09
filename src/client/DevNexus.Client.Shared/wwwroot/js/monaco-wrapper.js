/**
 * Monaco Editor JavaScript 封装
 * 用于 Blazor 组件的 JS 互操作
 */

(function () {
    'use strict';

    // 存储编辑器实例
    const editors = new Map();

    // Monaco Editor API
    window.monacoEditor = {
        /**
         * 创建编辑器实例
         * @param {HTMLElement} container - 容器元素
         * @param {string} editorId - 编辑器唯一标识
         * @param {object} options - 编辑器配置
         */
        create: async function (container, editorId, options) {
            if (!container) {
                console.error('[Monaco] 容器元素不存在');
                return;
            }

            // 等待 Monaco 加载
            await this._ensureMonacoLoaded();

            try {
                const editor = monaco.editor.create(container, {
                    value: options.value || '',
                    language: options.language || 'plaintext',
                    readOnly: options.readOnly || false,
                    theme: options.theme || 'vs-dark',
                    automaticLayout: options.automaticLayout !== false,
                    fontSize: options.fontSize || 14,
                    fontFamily: options.fontFamily || "'JetBrains Mono', Consolas, monospace",
                    minimap: options.minimap || { enabled: true },
                    wordWrap: options.wordWrap || 'off',
                    scrollBeyondLastLine: options.scrollBeyondLastLine !== true,
                    lineNumbers: options.lineNumbers || 'on',
                    renderLineHighlight: options.renderLineHighlight || 'line',
                    cursorBlinking: options.cursorBlinking || 'smooth',
                    smoothScrolling: options.smoothScrolling !== false,
                    padding: options.padding || { top: 12, bottom: 12 },
                    scrollbar: {
                        verticalScrollbarSize: 10,
                        horizontalScrollbarSize: 10,
                        useShadows: false,
                        verticalHasArrows: false,
                        horizontalHasArrows: false
                    }
                });

                // 注册 Ctrl+Shift+F 快捷键用于格式化
                editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyMod.Shift | monaco.KeyCode.KeyF, function () {
                    editor.getAction('editor.action.formatDocument')?.run();
                });

                // 存储粘贴前的内容，用于检测粘贴事件
                let contentBeforePaste = editor.getValue();
                let isPasting = false;

                // 监听粘贴事件（通过 DOM 事件）
                container.addEventListener('paste', function () {
                    isPasting = true;
                    contentBeforePaste = editor.getValue();
                });

                // 监听内容变化，检测粘贴后触发语言检测
                editor.onDidChangeModelContent(function (e) {
                    if (editor.__suppressContentChanged) {
                        return;
                    }

                    const changeCallback = window._monacoContentChangedCallbacks?.[editorId];
                    if (changeCallback) {
                        changeCallback.invokeMethodAsync('OnContentChangedFromJs', editor.getValue());
                    }

                    if (isPasting) {
                        isPasting = false;
                        // 延迟触发，确保内容已更新
                        setTimeout(function () {
                            const newContent = editor.getValue();
                            // 只有当内容确实增加时才触发（排除删除操作）
                            if (newContent.length > contentBeforePaste.length) {
                                // 调用已注册的回调（如果存在）
                                const callback = window._monacoPasteCallbacks?.[editorId];
                                if (callback) {
                                    callback.invokeMethodAsync('OnContentPastedFromJs');
                                }
                            }
                        }, 50);
                    }
                });

                editors.set(editorId, editor);
                console.log(`[Monaco] 编辑器 ${editorId} 创建成功`);
            } catch (error) {
                console.error('[Monaco] 创建编辑器失败:', error);
            }
        },

        /**
         * 更新编辑器内容
         * @param {string} editorId - 编辑器唯一标识
         * @param {string} content - 新内容
         */
        updateContent: function (editorId, content) {
            const editor = editors.get(editorId);
            if (!editor) {
                console.warn(`[Monaco] 编辑器 ${editorId} 不存在`);
                return;
            }

            const currentValue = editor.getValue();
            if (currentValue !== content) {
                editor.__suppressContentChanged = true;
                editor.setValue(content);
                editor.__suppressContentChanged = false;
            }
        },

        /**
         * 追加内容 (用于流式更新)
         * @param {string} editorId - 编辑器唯一标识
         * @param {string} text - 要追加的文本
         */
        appendContent: function (editorId, text) {
            const editor = editors.get(editorId);
            if (!editor) {
                console.warn(`[Monaco] 编辑器 ${editorId} 不存在`);
                return;
            }

            const model = editor.getModel();
            if (!model) return;

            const lineCount = model.getLineCount();
            const lastLineLength = model.getLineLength(lineCount);

            // 在末尾插入文本
            editor.executeEdits('', [{
                range: new monaco.Range(lineCount, lastLineLength + 1, lineCount, lastLineLength + 1),
                text: text,
                forceMoveMarkers: true
            }]);

            // 滚动到底部
            editor.revealLine(model.getLineCount());
        },

        /**
         * 获取编辑器内容
         * @param {string} editorId - 编辑器唯一标识
         * @returns {string} 编辑器内容
         */
        getContent: function (editorId) {
            const editor = editors.get(editorId);
            if (!editor) {
                console.warn(`[Monaco] 编辑器 ${editorId} 不存在`);
                return '';
            }
            return editor.getValue();
        },

        /**
         * 设置编辑器语言
         * @param {string} editorId - 编辑器唯一标识
         * @param {string} language - 语言标识
         */
        setLanguage: function (editorId, language) {
            const editor = editors.get(editorId);
            if (!editor) {
                console.warn(`[Monaco] 编辑器 ${editorId} 不存在`);
                return;
            }

            const model = editor.getModel();
            if (model) {
                monaco.editor.setModelLanguage(model, language);
            }
        },

        /**
         * 格式化文档
         * @param {string} editorId - 编辑器唯一标识
         */
        formatDocument: async function (editorId) {
            const editor = editors.get(editorId);
            if (!editor) {
                console.warn(`[Monaco] 编辑器 ${editorId} 不存在`);
                return;
            }

            await editor.getAction('editor.action.formatDocument')?.run();
        },

        /**
         * 聚焦编辑器
         * @param {string} editorId - 编辑器唯一标识
         */
        focus: function (editorId) {
            const editor = editors.get(editorId);
            if (!editor) {
                console.warn(`[Monaco] 编辑器 ${editorId} 不存在`);
                return;
            }

            editor.focus();
        },

        /**
         * 销毁编辑器实例
         * @param {string} editorId - 编辑器唯一标识
         */
        dispose: function (editorId) {
            const editor = editors.get(editorId);
            if (editor) {
                editor.dispose();
                editors.delete(editorId);
                // 清理粘贴回调
                if (window._monacoPasteCallbacks?.[editorId]) {
                    delete window._monacoPasteCallbacks[editorId];
                }
                if (window._monacoContentChangedCallbacks?.[editorId]) {
                    delete window._monacoContentChangedCallbacks[editorId];
                }
                console.log(`[Monaco] 编辑器 ${editorId} 已销毁`);
            }
        },

        /**
         * 注册粘贴事件回调
         * @param {string} editorId - 编辑器唯一标识
         * @param {object} dotNetRef - .NET 对象引用
         */
        registerPasteCallback: function (editorId, dotNetRef) {
            if (!window._monacoPasteCallbacks) {
                window._monacoPasteCallbacks = {};
            }
            window._monacoPasteCallbacks[editorId] = dotNetRef;
            console.log(`[Monaco] 编辑器 ${editorId} 粘贴回调已注册`);
        },

        /**
         * 注册内容变化回调。
         * @param {string} editorId - 编辑器唯一标识
         * @param {object} dotNetRef - .NET 对象引用
         */
        registerContentChangedCallback: function (editorId, dotNetRef) {
            if (!window._monacoContentChangedCallbacks) {
                window._monacoContentChangedCallbacks = {};
            }

            window._monacoContentChangedCallbacks[editorId] = dotNetRef;
            console.log(`[Monaco] 编辑器 ${editorId} 内容变化回调已注册`);
        },

        /**
         * 确保 Monaco 已加载
         * @private
         */
        _ensureMonacoLoaded: function () {
            return new Promise((resolve, reject) => {
                if (typeof monaco !== 'undefined') {
                    resolve();
                    return;
                }

                // 检查是否正在加载
                if (window._monacoLoading) {
                    window._monacoLoadingPromise.then(resolve).catch(reject);
                    return;
                }

                window._monacoLoading = true;
                window._monacoLoadingPromise = new Promise((res, rej) => {
                    // 使用本地已部署的 Monaco Editor 资源
                    const loaderScript = document.createElement('script');
                    loaderScript.src = '/lib/monaco-editor/min/vs/loader.js';
                    loaderScript.onload = function () {
                        require.config({
                            paths: {
                                'vs': '/lib/monaco-editor/min/vs'
                            }
                        });

                        require(['vs/editor/editor.main'], function () {
                            console.log('[Monaco] 编辑器库加载完成 (本地)');
                            res();
                            resolve();
                        }, function (err) {
                            console.error('[Monaco] 加载失败:', err);
                            rej(err);
                            reject(err);
                        });
                    };
                    loaderScript.onerror = function (err) {
                        console.error('[Monaco] 本地 loader.js 加载失败:', err);
                        rej(err);
                        reject(err);
                    };
                    document.head.appendChild(loaderScript);
                });
            });
        }
    };

    // Diff Editor API
    window.monacoDiffEditor = {
        /**
         * 创建 Diff 编辑器
         * @param {HTMLElement} container - 容器元素
         * @param {string} editorId - 编辑器唯一标识
         * @param {string} original - 原始内容
         * @param {string} modified - 修改后内容
         * @param {string} language - 语言标识
         */
        create: async function (container, editorId, original, modified, language) {
            if (!container) {
                console.error('[Monaco Diff] 容器元素不存在');
                return;
            }

            await window.monacoEditor._ensureMonacoLoaded();

            try {
                const diffEditor = monaco.editor.createDiffEditor(container, {
                    theme: 'vs-dark',
                    automaticLayout: true,
                    readOnly: true,
                    renderSideBySide: true,
                    fontSize: 14,
                    fontFamily: "'JetBrains Mono', Consolas, monospace",
                    scrollbar: {
                        verticalScrollbarSize: 10,
                        horizontalScrollbarSize: 10
                    }
                });

                diffEditor.setModel({
                    original: monaco.editor.createModel(original, language),
                    modified: monaco.editor.createModel(modified, language)
                });

                editors.set(editorId, diffEditor);
                console.log(`[Monaco Diff] 编辑器 ${editorId} 创建成功`);
            } catch (error) {
                console.error('[Monaco Diff] 创建编辑器失败:', error);
            }
        },

        /**
         * 更新 Diff 内容
         * @param {string} editorId - 编辑器唯一标识
         * @param {string} original - 原始内容
         * @param {string} modified - 修改后内容
         * @param {string} language - 语言标识
         */
        updateContent: function (editorId, original, modified, language) {
            const diffEditor = editors.get(editorId);
            if (!diffEditor) {
                console.warn(`[Monaco Diff] 编辑器 ${editorId} 不存在`);
                return;
            }

            diffEditor.setModel({
                original: monaco.editor.createModel(original, language),
                modified: monaco.editor.createModel(modified, language)
            });
        },

        /**
         * 销毁 Diff 编辑器
         * @param {string} editorId - 编辑器唯一标识
         */
        dispose: function (editorId) {
            window.monacoEditor.dispose(editorId);
        }
    };

    console.log('[Monaco] JS 封装已加载');
})();
