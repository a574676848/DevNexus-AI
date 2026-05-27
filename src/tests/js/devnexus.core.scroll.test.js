const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const corePath = path.resolve(
    __dirname,
    '../../client/DevNexus.Client.Shared/wwwroot/js/devnexus.core.js');

let now = 0;
const frames = [];

function requestAnimationFrame(callback) {
    frames.push(callback);
    return frames.length;
}

async function flushFrame() {
    const callback = frames.shift();
    assert.ok(callback, 'expected a pending animation frame');
    now += 16;
    callback(now);
    await Promise.resolve();
}

const sandbox = {
    Blob,
    console,
    performance: {
        now: function () {
            return now;
        }
    },
    requestAnimationFrame,
    window: {
        devnexus: {},
        requestAnimationFrame
    }
};

vm.runInNewContext(fs.readFileSync(corePath, 'utf8'), sandbox);

async function main() {
    const element = {
        clientHeight: 40,
        scrollHeight: 100,
        scrollTop: 0
    };

    const first = sandbox.window.scrollToBottomWhileStable(element, 32);
    const second = sandbox.window.scrollToBottomWhileStable(element, 32);

    assert.equal(await first, false);
    await flushFrame();
    element.scrollHeight = 140;
    await flushFrame();
    await flushFrame();

    assert.equal(await second, true);
    assert.equal(element.scrollTop, 140);
    assert.equal(sandbox.window.devnexus._stableScrollControllers.has(element), false);

    console.log('devnexus core scroll tests passed');
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
