/**
 * DevNexus AI - 前端通用辅助函数
 * 提供 UI 交互、性能优化、可访问性相关的 JS 互操作接口
 */

window.devnexus = window.devnexus || {};

/**
 * 将元素滚动到底部 - 用于Terminal和消息列表
 * @param {HTMLElement} element 要滚动的元素
 */
window.devnexus.scrollToBottom = function (element) {
    if (element && element.scrollHeight) {
        element.scrollTop = element.scrollHeight;
    }
};

/**
 * 初始化UI监听器（如折叠按钮的事件绑定）
 */
window.devnexus.initUIListeners = function () {
    // 自动绑定展开/收起按钮的焦点样式
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') {
            const focused = document.activeElement;
            if (focused && (focused.classList.contains('expand-btn') || focused.classList.contains('collapse-btn'))) {
                // 焦点自动处理，不需要额外逻辑
            }
        }
    });
};

/**
 * 注册展开Artifact的监听器
 * @param {DotNetObjectReference} dotNetRef C# 对象引用
 */
window.devnexus.registerOpenArtifactListener = function (dotNetRef) {
    window.devnexus._artifactListenerRef = dotNetRef;
};

/**
 * 移除展开Artifact的监听器
 */
window.devnexus.removeOpenArtifactListener = function () {
    if (window.devnexus._artifactListenerRef) {
        window.devnexus._artifactListenerRef = null;
    }
};

/**
 * 计算元素中文本的行数（用于Terminal长度检查）
 * @param {HTMLElement} element 文本容器
 * @returns {number} 行数
 */
window.devnexus.getLineCount = function (element) {
    if (!element || !element.textContent) return 0;
    return element.textContent.split('\n').length;
};

/**
 * 获取元素内容的字节大小
 * @param {string} text 文本内容
 * @returns {number} 字节数
 */
window.devnexus.getTextBytes = function (text) {
    if (!text) return 0;
    return new Blob([text]).size;
};

/**
 * 格式化字节大小为可读格式
 * @param {number} bytes 字节数
 * @returns {string} 格式化的字符串
 */
window.devnexus.formatBytes = function (bytes) {
    if (bytes < 1024) return bytes + 'B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + 'KB';
    return (bytes / (1024 * 1024)).toFixed(1) + 'MB';
};

/**
 * 性能监控：标记某个操作的开始时间
 * @param {string} name 操作名称
 */
window.devnexus.markStart = function (name) {
    window.devnexus._marks = window.devnexus._marks || {};
    window.devnexus._marks[name] = performance.now();
};

/**
 * 性能监控：计算某个操作的耗时
 * @param {string} name 操作名称
 * @returns {number} 耗时（毫秒）
 */
window.devnexus.markEnd = function (name) {
    if (!window.devnexus._marks || !window.devnexus._marks[name]) return 0;
    const duration = performance.now() - window.devnexus._marks[name];
    delete window.devnexus._marks[name];
    return duration;
};

/**
 * 防止重复点击（节流）
 * @param {function} callback 回调函数
 * @param {number} delay 延迟时间（毫秒）
 * @returns {function} 被节流的函数
 */
window.devnexus.throttle = function (callback, delay) {
    let lastCall = 0;
    return function (...args) {
        const now = Date.now();
        if (now - lastCall >= delay) {
            lastCall = now;
            callback.apply(this, args);
        }
    };
};

/**
 * 防抖（debounce）
 * @param {function} callback 回调函数
 * @param {number} delay 延迟时间（毫秒）
 * @returns {function} 被防抖的函数
 */
window.devnexus.debounce = function (callback, delay) {
    let timeoutId;
    return function (...args) {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => {
            callback.apply(this, args);
        }, delay);
    };
};

console.log('✅ DevNexus UI helpers loaded');
