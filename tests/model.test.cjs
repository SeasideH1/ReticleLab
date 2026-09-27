const { test } = require('node:test');
const assert = require('node:assert/strict');
const M = require('../model.js');
test('上一回合统计：击杀爆头率、命中爆头率和包含支出的净经济', () => {
  const r = M.recap(M.defaults.stats);
  assert.equal(r.kills, 3); assert.equal(r.hsKills, 2);
  assert.equal(r.hsKillPercent.toFixed(1), '66.7'); assert.equal(r.hsHitPercent, 40);
  assert.equal(r.damage, 342); assert.equal(r.money, 1450);
});
test('零击杀零命中与 GSI 未知值不显示伪造百分比', () => {
  const r = M.recap({ kills: 0, hsKills: 0, hits: 0, hsHits: 0, startMoney: 4000, endMoney: 1500 });
  assert.equal(r.hsKillPercent, null); assert.equal(r.hsHitPercent, null); assert.equal(r.money, -2500);
  assert.equal(M.recap({ ...M.defaults.stats, conservative: true }).hsHitPercent, null);
});
test('缩小总数时头部子集保持有效，不超过 100%', () => {
  const r = M.recap({ kills: 1, hsKills: 3, hits: 2, hsHits: 7 });
  assert.equal(r.hsKillPercent, 100); assert.equal(r.hsHitPercent, 100);
});
test('四方向每次严格移动一个预览像素', () => {
  const p = { x: 0, y: 0 };
  M.nudge(p, 'ArrowRight'); assert.deepEqual(p, { x: 1, y: 0 });
  M.nudge(p, 'ArrowUp'); assert.deepEqual(p, { x: 1, y: -1 });
  M.nudge(p, 'ArrowLeft'); M.nudge(p, 'ArrowDown'); assert.deepEqual(p, { x: 0, y: 0 });
});
test('可调入场、停留、退场的完整时间轴边界', () => {
  const s = M.defaults.kill, { total } = M.feedTiming(s);
  assert.equal(total, 2240); assert.equal(M.feedPhase(0, s), '白色矩形横向扩宽');
  assert.equal(M.feedPhase(s.enter + 1, s), '停留');
  assert.equal(M.feedPhase(s.enter + s.hold + 1, s), '压缩收束');
  assert.equal(M.feedPhase(total - 1, s), '矩形渐隐');
  assert.equal(M.feedPhase(total, s), '播放完毕');
});
test('普通击杀和爆头击杀使用完全相同的无外围标记几何', () => {
  assert.deepEqual(M.hitGeometry('kill', 15), M.hitGeometry('headkill', 15));
  assert.equal(M.hitGeometry('headkill', 15).diamond, null);
  assert.equal((M.hitGeometry('kill', 15).cross.match(/M/g) || []).length, 4);
  assert.equal(M.hitGeometry('headshot', 15).diamond, null);
});
test('普通击杀和爆头击杀均默认红色，保留独立调色', () => {
  assert.equal(M.hitColor('headkill', M.defaults.crosshair), '#ef554b');
  assert.equal(M.hitColor('kill', M.defaults.crosshair), '#ef554b');
});
test('单矩形扩宽、白到红渐变与退场使用连续有序关键帧', () => {
  const s = M.defaults.kill, frames = M.feedSurfaceFrames(s);
  assert.equal(frames[0].backgroundColor, '#ffffff');
  const entryEnd = frames.find(f => f.offset === s.enter / M.feedTiming(s).total);
  assert.equal(entryEnd.backgroundColor, s.color); assert.equal(entryEnd.transform, 'scaleX(1) scaleY(1)');
  assert.equal(entryEnd.opacity, s.opacity / 100); assert.equal(frames.at(-1).opacity, 0);
  assert.ok(frames.every((f, i) => f.offset >= 0 && f.offset <= 1 && (!i || f.offset >= frames[i-1].offset)));
});
test('淡出全程宽度为 1，纵向收缩与透明度遵循四次方缓出', () => {
  const s = M.defaults.kill, start = (s.enter + s.hold) / M.feedTiming(s).total;
  const exit = M.feedSurfaceFrames(s).filter(f => f.offset >= start);
  assert.ok(exit.every(f => f.transform.startsWith('scaleX(1) ')));
  assert.equal(M.quartOut(.5), .9375);
  assert.equal(exit[32].transform, 'scaleX(1) scaleY(0.0859375)');
  assert.equal(exit[32].opacity, s.opacity / 100 * .0625);
  const textExit = M.feedContentFrames(s).filter(f => f.offset >= start);
  assert.equal(textExit[32].opacity, .0625);
});
test('统计显示模式：准备阶段显示、交战阶段隐藏，常驻和编辑可覆盖', () => {
  const s = { ...M.defaults.stats, displayMode: 'round-start' };
  assert.equal(M.statsVisible(s, 'stats'), false);
  M.liveEvent(s, 'round'); assert.equal(M.statsVisible(s, 'stats'), true);
  M.liveEvent(s, 'live'); assert.equal(M.statsVisible(s, 'all'), false);
  s.edit = true; assert.equal(M.statsVisible(s, 'stats'), true);
  s.edit = false; s.displayMode = 'always'; assert.equal(M.statsVisible(s, 'all'), true);
  assert.equal(M.statsVisible(s, 'kill'), false);
});
test('逐键 A/B 互不影响，显式选择优先于新增默认', () => {
  const a = { displayMode: 'pressed' }, b = { displayMode: 'background' };
  assert.equal(M.keyDisplayMode(a, 'background'), 'pressed');
  assert.equal(M.keyDisplayMode(b, 'pressed'), 'background');
  assert.equal(M.keyDisplayMode({}, 'pressed'), 'pressed');
});
test('真实场景样例：命中、爆头击杀、死亡和助攻即时更新统计', () => {
  const s = structuredClone(M.defaults.stats);
  M.liveEvent(s, 'hit'); M.liveEvent(s, 'headkill'); M.liveEvent(s, 'death'); M.liveEvent(s, 'assist');
  assert.equal(s.hits, 17); assert.equal(s.hsHits, 7); assert.equal(s.kills, 4); assert.equal(s.hsKills, 3);
  assert.equal(s.damage, 470); assert.equal(s.totalDamage, 1407); assert.equal(s.endMoney, 4100);
  assert.equal(s.deaths, 3); assert.equal(s.assists, 1);
  assert.equal(M.recap(s).hsKillPercent, 75);
});
test('新回合只重置本回合统计并更换经济基准，累计伤害保留', () => {
  const s = structuredClone(M.defaults.stats); M.liveEvent(s, 'round');
  assert.equal(s.round, 9); assert.equal(s.damage, 0); assert.equal(s.totalDamage, 1279);
  assert.equal(s.hits, 0); assert.equal(s.kills, 0); assert.equal(s.deaths, 0); assert.equal(s.assists, 0);
  assert.equal(M.recap(s).money, 0); assert.equal(s.buyMenu, true);
});
test('拖动布局边界：元素不能超出画布，按像素取整', () => {
  assert.deepEqual(M.clampPosition(-10, -50, 43, 42), { x: 0, y: 0 });
  assert.deepEqual(M.clampPosition(999, 999, 43, 42), { x: 367, y: 118 });
  assert.deepEqual(M.clampPosition(100.6, 40.2, 155, 48, 330, 220), { x: 101, y: 40 });
});
test('吸附网格、画布中心和其它元素边缘', () => {
  const grid = M.snapPosition(33, 47, 40, 40, 330, 220, [], { grid: 10, tolerance: 2 });
  assert.equal(grid.x, 30); assert.equal(grid.y, 50);
  const center = M.snapPosition(143, 88, 40, 40, 330, 220, [], { grid: 10, tolerance: 6 });
  assert.equal(center.x, 145); assert.equal(center.y, 90); assert.equal(center.guides.x, 165);
  const edge = M.snapPosition(102, 52, 40, 40, 330, 220, [{ x: 100, y: 50, w: 40, h: 40 }]);
  assert.equal(edge.x, 100); assert.equal(edge.y, 50);
  const free = M.snapPosition(33, 47, 40, 40, 330, 220, [], { enabled: false });
  assert.equal(free.x, 33); assert.equal(free.y, 47);
});
test('逐项显示可覆盖整体规则，隐藏项不使父容器误显示', () => {
  const s = structuredClone(M.defaults.stats); s.displayMode = 'round-start'; s.roundPhase = 'live';
  const kda = s.items[0]; kda.displayMode = 'always';
  assert.equal(M.statItemVisible(kda, s), true); assert.equal(M.statsVisible(s, 'stats'), true);
  kda.displayMode = 'hidden'; assert.equal(M.statsVisible(s, 'stats'), false);
  s.displayMode = 'always'; const damage = s.items.find(i => i.id === 'damage'); damage.displayMode = 'round-start';
  assert.equal(M.statItemVisible(damage, s), false);
  s.roundPhase = 'freezetime'; assert.equal(M.statItemVisible(damage, s), true);
});
test('淡出多个采样时刻均遵循四次方缓出而非线性', () => {
  const s = M.defaults.kill, exitStart = (s.enter+s.hold)/M.feedTiming(s).total;
  const surface = M.feedSurfaceFrames(s).filter(f => f.offset >= exitStart), content = M.feedContentFrames(s).filter(f => f.offset >= exitStart);
  for (const i of [16, 32, 48]) {
    const remaining = (1-i/64)**4;
    assert.equal(content[i].opacity, remaining);
    assert.equal(surface[i].opacity, s.opacity/100*remaining);
    assert.ok(surface[i].transform.startsWith('scaleX(1)'));
  }
});
