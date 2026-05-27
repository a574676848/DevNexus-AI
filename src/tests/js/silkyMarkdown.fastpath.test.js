const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const rendererPath = path.resolve(
    __dirname,
    '../../client/DevNexus.Client.Shared/wwwroot/js/silkyMarkdown.renderer.js');
const apiPath = path.resolve(
    __dirname,
    '../../client/DevNexus.Client.Shared/wwwroot/js/silkyMarkdown.api.js');
const elementsById = new Map();

const sandbox = {
    console,
    document: {
        getElementById: function (id) {
            return elementsById.get(id) || null;
        },
        createElement: function () {
            return new FakeElement();
        },
        createTextNode: function (value) {
            return new FakeTextNode(value);
        }
    },
    window: {
        markdownit: function () {
            return {
                renderer: { rules: {} },
                render: function (content) {
                    return '<p>' + content + '</p>';
                }
            };
        },
        devnexusSilkyMarkdown: {
            _shared: {
                md: null,
                instances: new Map()
            }
        }
    }
};

function FakeTextNode(value) {
    this.data = value || '';
}

FakeTextNode.prototype.appendData = function (value) {
    this.data += value || '';
};

function FakeElement() {
    this.children = [];
    this.className = '';
}

Object.defineProperty(FakeElement.prototype, 'firstChild', {
    get: function () {
        return this.children[0] || null;
    }
});

Object.defineProperty(FakeElement.prototype, 'textContent', {
    get: function () {
        return this.children.map(child => child.data || child.textContent || '').join('');
    },
    set: function (value) {
        this.children = value ? [new FakeTextNode(value)] : [];
    }
});

FakeElement.prototype.appendChild = function (child) {
    this.children.push(child);
    return child;
};

FakeElement.prototype.contains = function (child) {
    return this.children.includes(child);
};

vm.runInNewContext(fs.readFileSync(rendererPath, 'utf8'), sandbox);
vm.runInNewContext(fs.readFileSync(apiPath, 'utf8'), sandbox);

const canUseFastPath = sandbox.window.devnexusSilkyMarkdown.canUseStreamingTextFastPath;
const renderFastPath = sandbox.window.devnexusSilkyMarkdown.renderStreamingTextFastPath;

assert.equal(canUseFastPath('这是一段普通中文回复，应该保持持续打字。'), true);
assert.equal(canUseFastPath('第一行\n第二行继续输出'), true);
assert.equal(canUseFastPath('**重点** 内容'), false);
assert.equal(canUseFastPath('```csharp\nConsole.WriteLine();'), false);
assert.equal(canUseFastPath('- 列表项'), false);
assert.equal(canUseFastPath('https://example.com'), false);

const instance = { container: new FakeElement() };
renderFastPath(instance, '你好');
renderFastPath(instance, '你好，世界');
assert.equal(instance.container.textContent, '你好，世界');
assert.equal(instance.lastRenderMode, 'streamText');
assert.equal(instance.lastStreamingTextContent, '你好，世界');

elementsById.set('final-state', new FakeElement());
sandbox.window.devnexusSilkyMarkdown.init('final-state');
const finalInstance = sandbox.window.devnexusSilkyMarkdown._shared.instances.get('final-state');
sandbox.window.devnexusSilkyMarkdown.render('final-state', '普通回复', true);
assert.equal(finalInstance.lastRenderMode, 'streamText');
sandbox.window.devnexusSilkyMarkdown.render('final-state', '普通回复', false);
assert.equal(finalInstance.lastRenderMode, 'markdown');
assert.equal(finalInstance.lastIsStreaming, false);
assert.equal(finalInstance.lastHtml, '<p>普通回复</p>');

console.log('silkyMarkdown fast path tests passed');
