/**
 * 现代极简风格 Chart.js 配置
 * 极简、扁平化、无阴影设计
 */

window.devnexus = window.devnexus || {};
window.devnexus.charts = {
    /**
     * 极简配色方案
     */
    colors: {
        primary: '#07c160',      // 品牌绿
        secondary: '#576b95',    // 链接蓝
        warning: '#fa5151',      // 警告红
        info: '#10aeff',         // 信息蓝
        success: '#07c160',      // 成功绿
        gray: '#999999',         // 灰色
        lightGray: '#e7e7e7',    // 浅灰
        text: '#000000',         // 文本黑
        textSecondary: '#999999' // 次要文本
    },

    /**
     * 获取多色调色板（用于饼图、柱状图等）
     */
    getColorPalette: function () {
        return [
            '#07c160', // 品牌绿
            '#576b95', // 链接蓝
            '#fa5151', // 警告红
            '#10aeff', // 信息蓝
            '#ff976a', // 橙色
            '#8b5cf6', // 紫色
            '#06ad56', // 深绿
            '#4a5568'  // 深灰
        ];
    },

    /**
     * 现代极简主题配置
     */
    getDefaultConfig: function () {
        return {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: true,
                    position: 'bottom',
                    labels: {
                        boxWidth: 12,
                        boxHeight: 12,
                        padding: 15,
                        font: {
                            size: 12,
                            family: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
                        },
                        color: '#000000',
                        usePointStyle: true,
                        pointStyle: 'circle'
                    }
                },
                tooltip: {
                    enabled: true,
                    backgroundColor: 'rgba(0, 0, 0, 0.8)',
                    titleColor: '#ffffff',
                    bodyColor: '#ffffff',
                    borderColor: '#e7e7e7',
                    borderWidth: 0,
                    padding: 12,
                    cornerRadius: 4,
                    displayColors: true,
                    boxWidth: 12,
                    boxHeight: 12,
                    boxPadding: 6,
                    titleFont: {
                        size: 13,
                        weight: '600'
                    },
                    bodyFont: {
                        size: 12
                    }
                }
            },
            interaction: {
                mode: 'index',
                intersect: false
            },
            animation: {
                duration: 400,
                easing: 'easeOutQuart'
            }
        };
    },

    /**
     * 折线图配置
     */
    getLineChartConfig: function (data, options = {}) {
        const config = this.getDefaultConfig();

        return {
            type: 'line',
            data: data,
            options: {
                ...config,
                scales: {
                    x: {
                        grid: {
                            display: false,
                            drawBorder: true,
                            borderColor: '#e7e7e7',
                            borderWidth: 1
                        },
                        ticks: {
                            font: {
                                size: 11
                            },
                            color: '#999999'
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: '#f5f5f5',
                            drawBorder: true,
                            borderColor: '#e7e7e7',
                            borderWidth: 1,
                            lineWidth: 1
                        },
                        ticks: {
                            font: {
                                size: 11
                            },
                            color: '#999999',
                            padding: 8
                        }
                    }
                },
                elements: {
                    line: {
                        tension: 0.4,
                        borderWidth: 2,
                        fill: true
                    },
                    point: {
                        radius: 3,
                        hoverRadius: 5,
                        borderWidth: 2,
                        backgroundColor: '#ffffff'
                    }
                },
                ...options
            }
        };
    },

    /**
     * 柱状图配置
     */
    getBarChartConfig: function (data, options = {}) {
        const config = this.getDefaultConfig();

        return {
            type: 'bar',
            data: data,
            options: {
                ...config,
                scales: {
                    x: {
                        grid: {
                            display: false,
                            drawBorder: true,
                            borderColor: '#e7e7e7'
                        },
                        ticks: {
                            font: {
                                size: 11
                            },
                            color: '#999999'
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: '#f5f5f5',
                            drawBorder: true,
                            borderColor: '#e7e7e7'
                        },
                        ticks: {
                            font: {
                                size: 11
                            },
                            color: '#999999'
                        }
                    }
                },
                elements: {
                    bar: {
                        borderRadius: 4,
                        borderSkipped: false
                    }
                },
                ...options
            }
        };
    },

    /**
     * 饼图/环形图配置
     */
    getDoughnutChartConfig: function (data, options = {}) {
        const config = this.getDefaultConfig();

        return {
            type: 'doughnut',
            data: data,
            options: {
                ...config,
                cutout: '60%',
                elements: {
                    arc: {
                        borderWidth: 0
                    }
                },
                ...options
            }
        };
    },

    /**
     * 渲染图表（通用方法）
     * 添加了对 Chart.js 库加载状态的检查和自动重试机制
     */
    renderChart: function (canvasId, data, type = 'line', customOptions = {}) {
        // 检查 Chart.js 是否已加载
        if (typeof Chart === 'undefined') {
            console.warn(`Chart.js library not loaded yet, retrying in 200ms for canvas "${canvasId}"...`);
            // 自动重试，最多重试 10 次（2秒）
            const self = this;
            const maxRetries = 10;
            let retryCount = 0;

            const retry = function () {
                retryCount++;
                if (typeof Chart !== 'undefined') {
                    console.log(`Chart.js now available, rendering "${canvasId}" (after ${retryCount} retries)`);
                    self.renderChart(canvasId, data, type, customOptions);
                } else if (retryCount < maxRetries) {
                    setTimeout(retry, 200);
                } else {
                    console.error(`Chart.js library failed to load after ${maxRetries} retries for canvas "${canvasId}"`);
                }
            };

            setTimeout(retry, 200);
            return null;
        }

        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.warn(`Canvas element with id "${canvasId}" not found (this is normal if the chart is not on current page)`);
            return null;
        }

        // 销毁已存在的图表实例
        const existingChart = Chart.getChart(canvasId);
        if (existingChart) {
            existingChart.destroy();
        }

        let chartConfig;
        switch (type) {
            case 'line':
                chartConfig = this.getLineChartConfig(data, customOptions);
                break;
            case 'bar':
                chartConfig = this.getBarChartConfig(data, customOptions);
                break;
            case 'doughnut':
            case 'pie':
                chartConfig = this.getDoughnutChartConfig(data, customOptions);
                break;
            default:
                console.error(`Unknown chart type: ${type}`);
                return null;
        }

        return new Chart(canvas, chartConfig);
    },

    /**
     * 创建渐变色（用于折线图填充）
     */
    createGradient: function (ctx, color, alpha = 0.1) {
        const gradient = ctx.createLinearGradient(0, 0, 0, 400);
        gradient.addColorStop(0, color.replace(')', `, ${alpha})`).replace('rgb', 'rgba'));
        gradient.addColorStop(1, color.replace(')', ', 0)').replace('rgb', 'rgba'));
        return gradient;
    },

    /**
     * 格式化数字（K, M, B）
     */
    formatNumber: function (num) {
        if (num >= 1000000000) {
            return (num / 1000000000).toFixed(1) + 'B';
        }
        if (num >= 1000000) {
            return (num / 1000000).toFixed(1) + 'M';
        }
        if (num >= 1000) {
            return (num / 1000).toFixed(1) + 'K';
        }
        return num.toString();
    },

    /**
     * 格式化货币
     */
    formatCurrency: function (num) {
        return '$' + num.toFixed(2);
    }
};

// 设置 Chart.js 全局默认值
if (typeof Chart !== 'undefined') {
    Chart.defaults.font.family = '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    Chart.defaults.font.size = 12;
    Chart.defaults.color = '#000000';
    Chart.defaults.borderColor = '#e7e7e7';
    Chart.defaults.plugins.legend.display = true;
    Chart.defaults.plugins.tooltip.enabled = true;
}
