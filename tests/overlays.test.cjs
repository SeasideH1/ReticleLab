// Node-only event-handler tests. No browser or layout engine is used.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const M = require('../model.js');
function setup() {
  const document = { activeElement: null, getElementById: () => null };
  const context = { window: { LabModel: M }, document, performance: { now: () => 1000 }, Set, Element: class {} };
  vm.runInNewContext(fs.readFileSync(require.resolve('../overlays.js'), 'utf8'), context);
  const o = Object.create(context.window.LabOverlays.prototype), state = structuredClone(M.defaults);
  Object.assign(o, { stage: {}, getState: () => state, getTab: () => 'input', pressed: new Set(), buttons: 0, points: [], traceCursor: { x: 45, y: 48 }, capture: false, updatePressed() {}, status() {}, refresh() {}, save() {}, toast() {} });
  document.activeElement = o.stage;
  return { o, state, document };
}
function key(code) { return { code, key: code.replace('Key', ''), preventDefault() { this.prevented = true; }, stopImmediatePropagation() { this.stopped = true; } }; }
test('画布焦点内多键同时按下；失焦清空键与鼠标按钮', () => {
  const { o } = setup(); o.keydown(key('KeyW')); o.keydown(key('ShiftLeft'));
  assert.deepEqual([...o.pressed], ['KeyW', 'ShiftLeft']); o.buttons = 3; o.release();
  assert.equal(o.pressed.size, 0); assert.equal(o.buttons, 0);
});
test('输入框焦点、检测关闭与编辑模式均不检测键盘', () => {
  const { o, state, document } = setup(); document.activeElement = {};
  o.keydown(key('KeyW')); assert.equal(o.pressed.size, 0);
  document.activeElement = o.stage; state.input.enabled = false;
  o.keydown(key('KeyW')); assert.equal(o.pressed.size, 0);
  state.input.enabled = true; state.input.edit = true;
  o.keydown(key('KeyW')); assert.equal(o.pressed.size, 0);
});
test('重新录入检测键，拒绝重复键，Escape 取消', () => {
  const { o, state } = setup(); o.capture = true;
  const e = key('KeyF'); o.keydown(e);
  assert.equal(state.input.selected, 'KeyF'); assert.equal(o.capture, false); assert.equal(e.stopped, true);
  o.capture = true; o.keydown(key('KeyA')); assert.equal(o.capture, true); assert.equal(state.input.selected, 'KeyF');
  o.keydown({ ...key('Escape'), key: 'Escape' }); assert.equal(o.capture, false);
});
test('恢复布局保持检测键唯一、规范坐标并保留隐藏统计项', () => {
  const { o, state } = setup();
  o.restore({ input: { keys: [{ code: 'KeyF', label: 'F', x: -5, y: 500, w: 43 }, { code: 'KeyF' }], mouse: { x: 1000, y: 1000 } }, stats: { items: [{ id: 'kda', label: '战绩', x: -100, y: 20, w: 155, visible: false }] } });
  assert.equal(state.input.keys.length, 1); assert.equal(state.input.keys[0].x, 0); assert.equal(state.input.keys[0].y, 118);
  assert.equal(state.input.mouse.x, 350); assert.equal(state.stats.items[0].visible, false); assert.equal(state.stats.items[0].label, '战绩');
});
test('拖动时按实际缩放换算坐标并在边界内落点', () => {
  const { o, state } = setup(), item = state.input.keys[1], node = { style: {} };
  state.input.snap = false;
  o.scales = { input: .5 }; o.drag = { item, node, group: 'input', x: 100, y: 100, initialX: item.x, initialY: item.y, w: item.w, h: 42 };
  o.pointermove({ clientX: 110, clientY: 110 });
  assert.equal(item.x, 142); assert.equal(item.y, 20); assert.equal(node.style.left, '142px');
});
test('拖拽吸附实际事件与 Alt 临时解除', () => {
  const { o, state } = setup(), item = state.input.keys[1], node = { style: {} };
  o.scales = { input: 1 }; o.drag = { item, node, group: 'input', x: 0, y: 0, initialX: item.x, initialY: item.y, w: item.w, h: 42 };
  o.pointermove({ clientX: 3, clientY: 3 });
  assert.equal(item.x, 122); assert.equal(item.y, 0);
  o.pointermove({ clientX: 3, clientY: 3, altKey: true });
  assert.equal(item.x, 125); assert.equal(item.y, 3);
});
test('逐项统计与鼠标轨迹模式保存恢复', () => {
  const { o, state } = setup();
  o.restore({ stats: { items: [{ id: 'kda', displayMode: 'always' }, { id: 'damage', displayMode: 'round-start' }] }, input: { mouse: { x: 200, y: 0, displayMode: 'pressed' }, trace: { x: 300, y: 0, displayMode: 'moving' } } });
  assert.equal(state.stats.items.find(i => i.id === 'kda').displayMode, 'always');
  assert.equal(state.stats.items.find(i => i.id === 'damage').displayMode, 'round-start');
  assert.equal(state.input.mouse.displayMode, 'pressed'); assert.equal(state.input.trace.displayMode, 'moving');
});
test('轨迹限制内存，大幅移动保留历史并平移显示范围，不归中', () => {
  const { o } = setup();
  for (let i = 0; i < 220; i++) o.addPoint(i % 2 ? .1 : -.1, 0, i);
  assert.equal(o.points.length, 180); o.addPoint(1000, 1000, 300);
  assert.equal(o.points.length, 180); assert.equal(o.traceCursor.x, 86); assert.equal(o.traceCursor.y, 92);
  const last = o.points.at(-1), prev = o.points.at(-2);
  assert.ok(Math.abs(last.x-prev.x-300) < 1e-9); assert.ok(Math.abs(last.y-prev.y-300) < 1e-9);
});
test('连续大幅移动与反向移动保持方向和位移，不清空历史', () => {
  const { o } = setup();
  o.addPoint(1000, 0, 100); o.addPoint(1000, 0, 200); o.addPoint(-500, 0, 300);
  assert.equal(o.points.length, 4); assert.equal(o.traceCursor.x, 4); assert.equal(o.traceCursor.y, 48);
  assert.equal(o.points.at(-1).x - o.points.at(-2).x, -150);
  o.centerWhenIdle(2299); assert.equal(o.traceCursor.x, 4);
  o.centerWhenIdle(2300); assert.equal(o.traceCursor.x, 45);
});
test('鼠标静止满阈值归中，清除旧路径并重置下一次位移基准', () => {
  const { o, state } = setup(); state.input.idleCenter = 2000;
  o.addPoint(20, 10, 100); o.previous = { x: 700, y: 200 };
  o.centerWhenIdle(2099); assert.equal(o.traceCursor.x, 51);
  o.addPoint(0, 0, 2099); // stationary pointer reports do not delay centering
  o.centerWhenIdle(2100); assert.equal(o.traceCursor.x, 45); assert.equal(o.traceCursor.y, 48);
  assert.equal(o.points.length, 0); assert.equal(o.previous, null);
  o.addPoint(10, 0, 2200); assert.equal(o.traceCursor.x, 48);
});
test('连续移动延后归中，可调整静止阈值', () => {
  const { o, state } = setup(); state.input.idleCenter = 500;
  o.addPoint(10, 0, 100); o.addPoint(10, 0, 550); o.centerWhenIdle(600);
  assert.equal(o.traceCursor.x, 51); o.centerWhenIdle(1050); assert.equal(o.traceCursor.x, 45);
});
test('恢复不同按键的独立 A/B 设置，并兼容旧版全局模式', () => {
  const { o, state } = setup();
  o.restore({ input: { displayMode: 'pressed', keys: [{ code: 'KeyW', displayMode: 'background' }, { code: 'KeyA', displayMode: 'pressed' }, { code: 'KeyS' }] } });
  assert.deepEqual(Array.from(state.input.keys, k => k.displayMode), ['background', 'pressed', 'pressed']);
});
