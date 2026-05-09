(function () {
    'use strict';

    window.devnexusSilkyMarkdown = window.devnexusSilkyMarkdown || {};

    function createActionButton(title, svgContent) {
        var btn = document.createElement('button');
        btn.className = 'code-action-btn';
        btn.title = title;
        btn.style.cssText = 'width: 24px; height: 24px; padding: 0; border: none; background: transparent; color: #999; border-radius: 2px; cursor: pointer; display: flex; align-items: center; justify-content: center;';
        btn.innerHTML = svgContent;
        return btn;
    }

    function showButtonSuccess(btn) {
        var originalHtml = btn.innerHTML;
        btn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#4ade80" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>';
        setTimeout(function () {
            btn.innerHTML = originalHtml;
        }, 2000);
    }

    function triggerOpenArtifact(content, type, title, isAuto) {
        document.dispatchEvent(new CustomEvent('devnexus:openArtifact', {
            detail: {
                type: type,
                language: type,
                title: title,
                content: content,
                isAuto: isAuto
            },
            bubbles: true,
            cancelable: true
        }));
    }

    function toggleCodeCollapse(preElement, wrapper, btn) {
        var isCollapsed = btn.dataset.collapsed === 'true';

        if (isCollapsed) {
            preElement.style.display = '';
            wrapper.classList.remove('collapsed');
            btn.dataset.collapsed = 'false';
            btn.title = '折叠代码块';
            btn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"></polyline></svg>';
            return;
        }

        preElement.style.display = 'none';
        wrapper.classList.add('collapsed');
        btn.dataset.collapsed = 'true';
        btn.title = '展开代码块';
        btn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>';
    }

    function checkSidekickCollapse(preElement, wrapper, collapseBtn) {
        var isSidekickVisible = window.devnexus && window.devnexus.isSidekickVisible === true;
        if (isSidekickVisible && collapseBtn.dataset.collapsed === 'false') {
            toggleCodeCollapse(preElement, wrapper, collapseBtn);
        }
    }

    function createCopyButton(codeElement) {
        var btn = createActionButton('复制代码', '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>');
        btn.classList.add('copy-code-btn');
        btn.onclick = function (e) {
            e.stopPropagation();
            navigator.clipboard.writeText(codeElement.innerText).then(function () {
                showButtonSuccess(btn);
            });
        };
        return btn;
    }

    function createCollapseButton(preElement, wrapper) {
        var btn = createActionButton('折叠代码块', '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"></polyline></svg>');
        btn.classList.add('collapse-code-btn');
        btn.dataset.collapsed = 'false';
        btn.onclick = function (e) {
            e.stopPropagation();
            toggleCodeCollapse(preElement, wrapper, btn);
        };
        return btn;
    }

    function createExpandButton(codeElement, language) {
        var btn = createActionButton('在侧屏中查看', '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="15 3 21 3 21 9"></polyline><polyline points="9 21 3 21 3 15"></polyline><line x1="21" y1="3" x2="14" y2="10"></line><line x1="3" y1="21" x2="10" y2="14"></line></svg>');
        btn.classList.add('expand-code-btn');
        btn.onclick = function (e) {
            e.stopPropagation();
            triggerOpenArtifact(codeElement.innerText, 'code', language.toUpperCase() + ' 代码', false);
        };
        return btn;
    }

    function checkAutoExpand(codeElement, language) {
        try {
            var pre = codeElement.tagName === 'PRE' ? codeElement : codeElement.parentNode;
            var codeText = codeElement.innerText || '';
            var isChart = pre.classList.contains('chart-rendered') || pre.classList.contains('mermaid-rendered');
            var isSidekickVisible = window.devnexus && window.devnexus.isSidekickVisible === true;

            if (pre.classList.contains('auto-expanded')) {
                if (isSidekickVisible) {
                    triggerOpenArtifact(codeText, isChart ? 'chart' : 'code', language, true);
                }
                return;
            }

            var lines = codeText.split('\n').length;
            var chars = codeText.length;
            var shouldAutoExpand = false;

            if (isChart) shouldAutoExpand = true;
            else if (lines >= 10 || chars >= 300) shouldAutoExpand = true;
            else if (language === 'html' && (codeText.indexOf('<!DOCTYPE') !== -1 || codeText.indexOf('<html') !== -1)) shouldAutoExpand = true;
            else if (language === 'mermaid' && lines >= 2) shouldAutoExpand = true;
            else if (language === 'json' && codeText.indexOf('"data"') !== -1 && codeText.indexOf('"type"') !== -1) shouldAutoExpand = true;
            else if (language === 'csharp' && (codeText.indexOf('class ') !== -1 || codeText.indexOf('namespace') !== -1)) shouldAutoExpand = true;

            if (shouldAutoExpand) {
                pre.classList.add('auto-expanded');
                setTimeout(function () {
                    var artifactType = (isChart || language === 'mermaid') ? 'chart' : 'code';
                    triggerOpenArtifact(codeText, artifactType, language.toUpperCase() + ' 代码', true);
                }, 100);
            }
        } catch (e) {
            console.warn('[SilkyMarkdown] checkAutoExpand error:', e);
        }
    }

    function renderMermaidBlocks(container) {
        var mermaidBlocks = container.querySelectorAll('pre code.language-mermaid');
        mermaidBlocks.forEach(function (code) {
            try {
                var pre = code.parentNode;
                if (!pre || pre.classList.contains('mermaid-rendered')) {
                    return;
                }

                pre.classList.add('mermaid-rendered');
                var mermaidDiv = document.createElement('div');
                mermaidDiv.className = 'mermaid-container';
                mermaidDiv.innerHTML = code.textContent || '';

                pre.parentNode.insertBefore(mermaidDiv, pre);
                pre.style.display = 'none';

                if (window.mermaid) {
                    mermaid.init(undefined, mermaidDiv);
                }
            } catch (e) {
                console.warn('[SilkyMarkdown] Mermaid 渲染失败:', e);
            }
        });
    }

    function renderPlotlyBlocks(container) {
        var jsonBlocks = container.querySelectorAll('pre code.language-json');
        jsonBlocks.forEach(function (code) {
            var pre = code.parentNode;
            if (pre.classList.contains('chart-rendered')) {
                return;
            }

            var jsonText = code.textContent.trim();

            try {
                var chartData = JSON.parse(jsonText);
                var isChartConfig = chartData && typeof chartData === 'object' && (
                    chartData.type === 'plotly' ||
                    (chartData.data && Array.isArray(chartData.data)) ||
                    ((chartData.type === 'bar' || chartData.type === 'line' || chartData.type === 'pie' || chartData.type === 'scatter' || chartData.type === 'heatmap') &&
                        (chartData.data || chartData.labels || chartData.values))
                );

                if (!isChartConfig) {
                    return;
                }

                pre.classList.add('chart-rendered');
                var chartWrapper = document.createElement('div');
                chartWrapper.className = 'inline-chart-wrapper';
                chartWrapper.style.cssText = 'margin: 12px 0; padding: 0; background: #fafafa; border-radius: 4px; position: relative; border: 1px solid #e7e7e7; overflow: hidden;';

                var header = document.createElement('div');
                header.style.cssText = 'display: flex; justify-content: flex-end; align-items: center; padding: 8px 12px; background: #f7f7f7; border-bottom: 1px solid #e7e7e7;';
                var toolbar = document.createElement('div');
                toolbar.className = 'chart-toolbar';
                toolbar.style.cssText = 'display: flex; gap: 4px;';

                var sidekickBtn = createActionButton('分屏展示', '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="15 3 21 3 21 9"></polyline><polyline points="9 21 3 21 3 15"></polyline><line x1="21" y1="3" x2="14" y2="10"></line><line x1="3" y1="21" x2="10" y2="14"></line></svg>');
                var copyJsonBtn = createActionButton('复制 JSON', '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>');
                var exportBtn = createActionButton('导出图片', '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect><circle cx="8.5" cy="8.5" r="1.5"></circle><polyline points="21 15 16 10 5 21"></polyline></svg>');

                copyJsonBtn.onclick = function (e) {
                    e.stopPropagation();
                    navigator.clipboard.writeText(jsonText).then(function () {
                        showButtonSuccess(copyJsonBtn);
                    });
                };

                var chartContainer = document.createElement('div');
                chartContainer.className = 'inline-chart-container';
                chartContainer.style.cssText = 'height: 320px; width: 100%; padding: 12px; background: #fafafa;';

                toolbar.appendChild(copyJsonBtn);
                toolbar.appendChild(exportBtn);
                toolbar.appendChild(sidekickBtn);
                header.appendChild(toolbar);
                chartWrapper.appendChild(header);
                chartWrapper.appendChild(chartContainer);

                pre.parentNode.insertBefore(chartWrapper, pre);
                pre.style.display = 'none';

                if (window.plotlyChart) {
                    var chartId = 'inline-chart-' + Math.random().toString(36).substr(2, 9);

                    sidekickBtn.onclick = function (e) {
                        e.stopPropagation();
                        triggerOpenArtifact(JSON.stringify(chartData), 'chart', chartData.title || '图表', false);
                    };

                    exportBtn.onclick = function (e) {
                        e.stopPropagation();
                        if (window.plotlyChart.exportImage) {
                            window.plotlyChart.exportImage(chartId, chartData.title || 'chart');
                        }
                    };

                    window.plotlyChart.render(
                        chartContainer,
                        chartId,
                        JSON.stringify(chartData),
                        JSON.stringify(chartData.layout || {}),
                        '{}'
                    ).catch(function (err) {
                        console.warn('[SilkyMarkdown] 图表渲染失败:', err);
                        pre.style.display = '';
                        chartWrapper.remove();
                        pre.classList.remove('chart-rendered');
                    });
                }
            } catch (e) {
            }
        });
    }

    function enhanceNormalCodeBlocks(container) {
        var preElements = container.querySelectorAll('pre:not(.enhanced):not(.mermaid-rendered):not(.chart-rendered)');
        preElements.forEach(function (pre) {
            pre.classList.add('enhanced');

            var code = pre.querySelector('code');
            var language = 'text';
            if (code) {
                var classes = code.className.split(' ');
                var langClass = classes.find(function (c) { return c.startsWith('language-'); });
                if (langClass) {
                    language = langClass.replace('language-', '');
                }
            }

            var wrapper = document.createElement('div');
            wrapper.className = 'code-block-wrapper';
            pre.parentNode.insertBefore(wrapper, pre);

            var header = document.createElement('div');
            header.className = 'code-block-header';

            var langLabel = document.createElement('span');
            langLabel.className = 'code-language';
            langLabel.textContent = language;

            var actions = document.createElement('div');
            actions.className = 'code-block-actions';
            actions.style.cssText = 'display: flex; gap: 4px; align-items: center;';

            var collapseBtn = createCollapseButton(pre, wrapper);
            var copyBtn = createCopyButton(code || pre);
            var expandBtn = createExpandButton(code || pre, language);

            actions.appendChild(collapseBtn);
            actions.appendChild(copyBtn);
            actions.appendChild(expandBtn);
            header.appendChild(langLabel);
            header.appendChild(actions);
            wrapper.appendChild(header);
            wrapper.appendChild(pre);

            checkAutoExpand(code || pre, language);
            checkSidekickCollapse(pre, wrapper, collapseBtn);
        });
    }

    function applyPrismHighlight(container) {
        if (!window.Prism) {
            return;
        }

        var codeBlocks = container.querySelectorAll('pre code:not(.prism-highlighted)');
        codeBlocks.forEach(function (block) {
            try {
                Prism.highlightElement(block);
                block.classList.add('prism-highlighted');
            } catch (e) {
                console.warn('[SilkyMarkdown] Prism 高亮失败:', e);
            }
        });
    }

    function enhanceCodeBlocks(container) {
        if (!container || !(container instanceof Element)) {
            return;
        }

        try {
            renderMermaidBlocks(container);
            renderPlotlyBlocks(container);
            enhanceNormalCodeBlocks(container);
            applyPrismHighlight(container);
        } catch (e) {
            console.error('[SilkyMarkdown] enhanceCodeBlocks 执行失败:', e);
        }
    }

    function syncSidekickState(isSidekickVisible) {
        window.devnexus = window.devnexus || {};
        window.devnexus.isSidekickVisible = isSidekickVisible;

        var wrappers = document.querySelectorAll('.code-block-wrapper');
        wrappers.forEach(function (wrapper) {
            var pre = wrapper.querySelector('pre');
            var collapseBtn = wrapper.querySelector('.collapse-code-btn');

            if (!pre || !collapseBtn) {
                return;
            }

            var isCurrentlyCollapsed = collapseBtn.dataset.collapsed === 'true';
            if (isSidekickVisible && !isCurrentlyCollapsed) {
                toggleCodeCollapse(pre, wrapper, collapseBtn);
            } else if (!isSidekickVisible && isCurrentlyCollapsed) {
                toggleCodeCollapse(pre, wrapper, collapseBtn);
            }
        });
    }

    window.devnexusSilkyMarkdown.enhanceCodeBlocks = enhanceCodeBlocks;
    window.devnexusSilkyMarkdown.syncSidekickState = syncSidekickState;
})();
