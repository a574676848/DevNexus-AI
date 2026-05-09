/**
 * Plotly.js JavaScript 封装
 * 用于 Blazor 组件的 JS 互操作
 */

(function () {
    'use strict';

    // 存储图表实例引用
    const charts = new Map();

    // Plotly Chart API
    window.plotlyChart = {
        /**
         * 渲染图表
         * @param {HTMLElement} container - 容器元素
         * @param {string} chartId - 图表唯一标识
         * @param {string} dataJson - 数据 JSON 字符串
         * @param {string} layoutJson - 布局 JSON 字符串
         * @param {string} configJson - 配置 JSON 字符串
         */
        render: async function (container, chartId, dataJson, layoutJson, configJson) {
            if (!container) {
                console.error('[Plotly] 容器元素不存在');
                return;
            }

            // 等待 Plotly 加载
            await this._ensurePlotlyLoaded();

            try {
                // ⚠️ 强制设置容器宽度为100%,确保 Plotly 能正确计算尺寸
                container.style.width = '100%';

                let parsedData = JSON.parse(dataJson);
                const userLayout = JSON.parse(layoutJson || '{}');
                const userConfig = JSON.parse(configJson || '{}');

                // 🔧 数据格式转换:将简化的 AI 格式转换为 Plotly 原生格式
                parsedData = this._convertToPlotlyFormat(parsedData);

                // 默认极简风格浅色主题布局 (Modern Light Series)
                const defaultLayout = {
                    paper_bgcolor: 'transparent',
                    plot_bgcolor: 'transparent',
                    font: {
                        color: '#333333',
                        family: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
                        size: 12
                    },
                    showlegend: true,
                    legend: {
                        orientation: 'h',
                        yanchor: 'top',
                        y: -0.2,
                        xanchor: 'center',
                        x: 0.5,
                        font: { size: 11, color: '#888' },
                        bgcolor: 'rgba(255,255,255,0)'
                    },
                    xaxis: {
                        gridcolor: '#eeeeee',
                        zerolinecolor: '#dddddd',
                        tickcolor: '#cccccc',
                        tickfont: { color: '#888', size: 11 },
                        automargin: true
                    },
                    yaxis: {
                        gridcolor: '#eeeeee',
                        zerolinecolor: '#dddddd',
                        tickcolor: '#cccccc',
                        tickfont: { color: '#888', size: 11 },
                        automargin: true
                    },
                    margin: {
                        l: 50,
                        r: 30,
                        t: 30,
                        b: 100 // 为底部图例留出空间
                    },
                    hovermode: 'closest',
                    hoverlabel: {
                        bgcolor: '#ffffff',
                        bordercolor: '#cccccc',
                        font: { color: '#333' }
                    },
                    autosize: true
                };

                // 默认配置 - 完全隐藏 Plotly 原生工具条（UI 设计规范：移除不必要的 UI 元素）
                const defaultConfig = {
                    responsive: true,
                    displayModeBar: false,  // 隐藏工具条
                    displaylogo: false,     // 隐藏 Plotly logo
                    staticPlot: false,      // 保持交互性
                    modeBarButtonsToRemove: [], // 以防万一
                    showLink: false,        // 隐藏"编辑图表"链接
                    editable: false         // 禁用编辑功能
                };

                // 合并配置
                const layout = { ...defaultLayout, ...userLayout };
                // 强制覆盖用户配置中的工具条设置，确保符合 UI 设计规范
                const config = {
                    ...defaultConfig,
                    ...userConfig,
                    displayModeBar: false,  // 强制隐藏工具条
                    displaylogo: false,     // 强制隐藏 logo
                    showLink: false         // 强制隐藏链接
                };

                // 统一标题样式
                if (layout.title && typeof layout.title === 'string') {
                    layout.title = { text: layout.title, font: { size: 14, weight: 600, color: '#000' } };
                }

                // 渲染图表
                await Plotly.newPlot(container, parsedData, layout, config);

                charts.set(chartId, container);

                // ⚠️ 添加 ResizeObserver 监听容器大小变化,自动重新布局
                if (!container._resizeObserver) {
                    container._resizeObserver = new ResizeObserver(() => {
                        if (container.offsetWidth > 0) {
                            Plotly.Plots.resize(container);
                        }
                    });
                    container._resizeObserver.observe(container);
                }

                console.log(`[Plotly] 图表 ${chartId} 渲染成功`);
            } catch (error) {
                console.error('[Plotly] 渲染图表失败:', error);
            }
        },

        /**
         * 更新图表数据
         * @param {string} chartId - 图表唯一标识
         * @param {string} dataJson - 新数据 JSON 字符串
         * @param {string} layoutJson - 新布局 JSON 字符串
         */
        update: async function (chartId, dataJson, layoutJson) {
            const container = charts.get(chartId);
            if (!container) {
                console.warn(`[Plotly] 图表 ${chartId} 不存在`);
                return;
            }

            try {
                const data = JSON.parse(dataJson);
                const layout = JSON.parse(layoutJson || '{}');

                await Plotly.react(container, data, layout);
            } catch (error) {
                console.error('[Plotly] 更新图表失败:', error);
            }
        },

        /**
         * 导出图片
         * @param {string} chartId - 图表唯一标识
         * @param {string} filename - 文件名
         */
        exportImage: async function (chartId, filename) {
            const container = charts.get(chartId);
            if (!container) {
                console.warn(`[Plotly] 图表 ${chartId} 不存在`);
                return;
            }

            try {
                await Plotly.downloadImage(container, {
                    format: 'png',
                    width: 1200,
                    height: 800,
                    filename: filename || 'chart'
                });
            } catch (error) {
                console.error('[Plotly] 导出图片失败:', error);
            }
        },

        /**
         * 重置缩放
         * @param {string} chartId - 图表唯一标识
         */
        resetZoom: async function (chartId) {
            const container = charts.get(chartId);
            if (!container) {
                // 图表不存在，静默返回（不输出警告日志）
                return;
            }

            // 检查图表是否已完全渲染（避免 _inputDomain 错误）
            if (!container._fullLayout) {
                // 图表尚未完成渲染，静默返回
                return;
            }

            try {
                await Plotly.relayout(container, {
                    'xaxis.autorange': true,
                    'yaxis.autorange': true
                });
            } catch (error) {
                // 静默处理错误，避免控制台噪音
            }
        },

        /**
         * 销毁图表
         * @param {string} chartId - 图表唯一标识
         */
        dispose: function (chartId) {
            const container = charts.get(chartId);
            if (container) {
                try {
                    // 清理 ResizeObserver
                    if (container._resizeObserver) {
                        container._resizeObserver.disconnect();
                        delete container._resizeObserver;
                    }

                    Plotly.purge(container);
                } catch (error) {
                    console.warn('[Plotly] 销毁图表时发生错误:', error);
                }
                charts.delete(chartId);
                console.log(`[Plotly] 图表 ${chartId} 已销毁`);
            }
        },

        /**
         * 确保 Plotly 已加载
         * 注意：Plotly 已在 index.html 中预加载，此函数仅做检查
         * @private
         */
        _ensurePlotlyLoaded: function () {
            return new Promise((resolve, reject) => {
                if (typeof Plotly !== 'undefined') {
                    resolve();
                } else {
                    // Plotly 应该已在 index.html 中加载，如果未定义则报错
                    reject(new Error('Plotly 未加载，请确保 index.html 中已引入 plotly.min.js'));
                }
            });
        },

        /**
         * 将简化的 AI 生成格式转换为 Plotly 原生格式
         * 支持的输入格式：
         * 1. { type: 'bar', data: { labels: [...], values: [...] } }
         * 2. { type: 'pie', data: { labels: [...], values: [...] } }
         * 3. { type: 'line', data: { x: [...], y: [...] } }
         * 4. 已经是 Plotly 格式的数组 [{ type: 'bar', x: [...], y: [...] }]
         * @private
         */
        _convertToPlotlyFormat: function (data) {
            // 如果已经是数组，假设是 Plotly 原生格式
            if (Array.isArray(data)) {
                return data;
            }

            // 如果不是对象，返回空数组
            if (!data || typeof data !== 'object') {
                console.warn('[Plotly] 无法解析图表数据:', data);
                return [];
            }

            // 🔧 处理 ChartDto 格式：{ type: "plotly", title: "...", data: [...], layout: {...} }
            // 如果 data.data 已经是 Plotly 原生格式的数组，直接返回
            if (data.data && Array.isArray(data.data)) {
                console.log('[Plotly] 检测到 ChartDto 格式，直接使用 data.data');
                return data.data;
            }

            // 检测是否为简化的 AI 格式 { type: 'bar', data: { labels: [...], values: [...] } }
            const chartType = (data.type || 'bar').toLowerCase();
            const chartData = data.data || data;

            // 🎨 极简浅色系克制调色板 (Modern Light Palette)
            const colors = [
                '#07c160',  /* 品牌绿 --action-primary */
                '#576b95',  /* 链接蓝 */
                '#fa9d3b',  /* 橙色 */
                '#f44336',  /* 红色 */
                '#b2b2b2',  /* 灰色 */
                '#607d8b',  /* 蓝灰 */
                '#9c27b0',  /* 紫色 */
                '#00bcd4'   /* 青色 */
            ];

            // 根据类型转换
            switch (chartType) {
                case 'bar':
                    return [{
                        type: 'bar',
                        x: chartData.labels || chartData.x || [],
                        y: chartData.values || chartData.y || [],
                        name: chartData.barName || chartData.name || '数据',
                        marker: {
                            color: colors[0],
                            line: { color: colors[0], width: 0 }
                        },
                        width: 0.5
                    }];

                case 'line':
                    return [{
                        type: 'scatter',
                        mode: 'lines+markers',
                        x: chartData.labels || chartData.x || [],
                        y: chartData.values || chartData.y || [],
                        name: chartData.lineName || chartData.name || '趋势',
                        line: { color: colors[1], width: 2, shape: 'spline' },
                        marker: { color: colors[1], size: 6, line: { color: '#fff', width: 1 } },
                        fill: 'tozeroy',
                        fillcolor: 'rgba(87, 107, 149, 0.05)'
                    }];

                case 'pie':
                    return [{
                        type: 'pie',
                        labels: chartData.labels || [],
                        values: chartData.values || [],
                        domain: { x: [0, 1], y: [0.2, 1] },
                        hole: 0.45,
                        textinfo: 'percent',
                        hoverinfo: 'label+value+percent',
                        insidetextorientation: 'horizontal',
                        marker: {
                            colors: colors,
                            line: { color: '#ffffff', width: 2 }
                        },
                        pull: [0.01, 0.01, 0.01, 0.01, 0.01],
                    }];

                case 'scatter':
                    return [{
                        type: 'scatter',
                        mode: 'markers',
                        x: chartData.x || chartData.labels || [],
                        y: chartData.y || chartData.values || [],
                        name: chartData.name || '散点',
                        marker: {
                            color: colors[0],
                            size: 8,
                            opacity: 0.6
                        }
                    }];

                case 'heatmap':
                    return [{
                        type: 'heatmap',
                        z: chartData.z || chartData.values || [],
                        x: chartData.x || chartData.xLabels || [],
                        y: chartData.y || chartData.yLabels || [],
                        colorscale: [
                            [0, '#f5f5f5'],
                            [0.5, '#95ec69'],
                            [1, '#07c160']
                        ]
                    }];

                default:
                    // 尝试直接使用原始数据
                    console.log('[Plotly] 未识别的图表类型，尝试直接渲染:', chartType);
                    if (chartData.x && chartData.y) {
                        return [{
                            type: chartType,
                            x: chartData.x,
                            y: chartData.y,
                            marker: { color: colors[0] }
                        }];
                    }
                    return [data];
            }
        }
    };

    console.log('[Plotly] JS 封装已加载');
})();
