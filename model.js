(function (root) {
  'use strict';
  const defaults = {
    crosshair: { size: 7, gap: 5, thickness: 1.5, color: '#e6f0c3', dot: false, x: 0, y: 0, effectSize: 15, effectWidth: 2, duration: 420, hitColor: '#ffffff', killColor: '#ef554b', headColor: '#f3cf77', headKillColor: '#ef554b' },
    kill: { name: 'AK-47', value: '100', color: '#f36e87', opacity: 75, radius: 3, width: 210, height: 40, x: 0, y: 0, scale: 100, hold: 1500, enter: 260, exit: 480 },
    stats: { round: 8, kills: 3, deaths: 2, assists: 0, hsKills: 2, hits: 15, hsHits: 6, damage: 342, totalDamage: 1279, startMoney: 2350, endMoney: 3800, conservative: false, opacity: 30, color: '#df72bd', buyOpacity: 20, buyMenu: false, displayMode: 'always', roundPhase: 'live', x: 0, y: 0, scale: 100, edit: false, selected: 'kda', items: [
      { id: 'kda', label: 'K / D / A', x: 0, y: 0, w: 155, visible: true }, { id: 'cash', label: '当前金钱', x: 170, y: 0, w: 155, visible: true },
      { id: 'kills', label: '击杀', x: 0, y: 55, w: 100, visible: true }, { id: 'deaths', label: '死亡', x: 110, y: 55, w: 100, visible: true }, { id: 'assists', label: '助攻', x: 220, y: 55, w: 100, visible: true },
      { id: 'hskill', label: 'HS KILL', x: 0, y: 110, w: 100, visible: true }, { id: 'hshit', label: '命中 HS', x: 110, y: 110, w: 100, visible: true }, { id: 'net', label: '回合净增', x: 220, y: 110, w: 100, visible: true },
      { id: 'damage', label: '回合伤害', x: 0, y: 165, w: 155, visible: true }, { id: 'totalDamage', label: '累计伤害', x: 170, y: 165, w: 155, visible: true }
    ] },
    input: { style: 'rounded', displayMode: 'background', color: '#d657ae', opacity: 32, scale: 100, x: 0, y: 0, edit: false, enabled: true, trail: true, trailLife: 1200, idleCenter: 2000, sensitivity: 1, selected: 'KeyW', keys: [
      { code: 'ShiftLeft', label: 'SHIFT', x: 0, y: 0, w: 65 }, { code: 'KeyW', label: 'W', x: 122, y: 0, w: 43 },
      { code: 'ControlLeft', label: 'CTRL', x: 0, y: 50, w: 65 }, { code: 'KeyA', label: 'A', x: 72, y: 50, w: 43 },
      { code: 'KeyS', label: 'S', x: 122, y: 50, w: 43 }, { code: 'KeyD', label: 'D', x: 172, y: 50, w: 43 },
      { code: 'Space', label: 'SPACE', x: 72, y: 100, w: 143 }
    ], mouse: { x: 234, y: 0 }, trace: { x: 302, y: 0 } }
  };
  defaults.input.keys.forEach(key => { key.displayMode = 'background'; });
  defaults.stats.items.forEach(item => { item.displayMode = 'inherit'; });
  defaults.input.mouse.displayMode = 'background';
  defaults.input.trace.displayMode = 'always';
  for (const group of ['stats', 'input']) { defaults[group].snap = true; defaults[group].snapGrid = 10; }
  function recap(s) {
    const kills = Math.max(0, Number(s.kills) || 0), hits = Math.max(0, Number(s.hits) || 0);
    const hsKills = Math.min(kills, Math.max(0, Number(s.hsKills) || 0));
    const hsHits = Math.min(hits, Math.max(0, Number(s.hsHits) || 0));
    return { kills, hsKills, hits, hsHits, hsKillPercent: kills ? 100 * hsKills / kills : null, hsHitPercent: s.conservative || !hits ? null : 100 * hsHits / hits, damage: Math.max(0, Number(s.damage) || 0), money: (Number(s.endMoney) || 0) - (Number(s.startMoney) || 0) };
  }
  function feedTiming(s) { return { enter: s.enter, hold: s.hold, exit: s.exit, total: s.enter + s.hold + s.exit }; }
  function feedPhase(ms, s) {
    if (ms < s.enter * .35) return '白色矩形横向扩宽';
    if (ms < s.enter * .72) return '矩形展开 · 白到红';
    if (ms < s.enter) return '内容显现';
    if (ms < s.enter + s.hold) return '停留';
    if (ms < s.enter + s.hold + s.exit * .5) return '压缩收束';
    if (ms < feedTiming(s).total) return '矩形渐隐';
    return '播放完毕';
  }
  function nudge(s, direction) {
    const delta = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[direction];
    if (!delta) return false;
    s.x += delta[0]; s.y += delta[1]; return true;
  }
  function liveEvent(s, kind) {
    if (kind === 'round') {
      s.round++; s.kills = s.deaths = s.assists = s.hsKills = s.hits = s.hsHits = s.damage = 0;
      s.startMoney = s.endMoney; s.buyMenu = true; s.roundPhase = 'freezetime'; return;
    }
    if (kind === 'live') { s.roundPhase = 'live'; s.buyMenu = false; return; }
    if (kind === 'death') { s.deaths++; return; }
    if (kind === 'assist') { s.assists++; return; }
    if (!['hit', 'headshot', 'kill', 'headkill'].includes(kind)) return;
    const head = kind === 'headshot' || kind === 'headkill', kill = kind === 'kill' || kind === 'headkill';
    s.hits++; if (head) s.hsHits++;
    const damage = kill ? 100 : head ? 67 : 28;
    s.damage += damage; s.totalDamage = (s.totalDamage || 0) + damage;
    if (kill) { s.kills++; if (head) s.hsKills++; s.endMoney += 300; }
  }
  function hitGeometry(kind, size) {
    const gap = size * .47;
    const cross = `M${-size} ${-size}L${-gap} ${-gap}M${size} ${-size}L${gap} ${-gap}M${-size} ${size}L${-gap} ${gap}M${size} ${size}L${gap} ${gap}`;
    return { cross, diamond: null };
  }
  function hitColor(kind, s) {
    return kind === 'headkill' ? s.headKillColor : kind === 'kill' ? s.killColor : kind === 'headshot' ? s.headColor : s.hitColor;
  }
  function keyDisplayMode(key, fallback = 'background') {
    return ['pressed', 'background', 'hidden'].includes(key.displayMode) ? key.displayMode : fallback === 'pressed' ? 'pressed' : 'background';
  }
  function feedSurfaceFrames(s) {
    const total = feedTiming(s).total, e = s.enter / total, holdEnd = (s.enter + s.hold) / total, alpha = s.opacity / 100;
    const frames = [], anchors = [
      { p: 0, x: .15, y: .045, c: '#ffffff', a: 0 },
      { p: .25, x: 1.85, y: .045, c: '#ffffff', a: 1 },
      { p: .5, x: 1.5, y: .24, c: '#ffd0d7', a: .95 },
      { p: .8, x: 1.02, y: .92, c: s.color, a: alpha },
      { p: 1, x: 1, y: 1, c: s.color, a: alpha }
    ];
    // Sample f(t)=1-(1-t)^4 per transition. The hold interval remains unwarped.
    for (let i = 0; i <= 64; i++) {
      const t = i / 64, q = quartOut(t), right = anchors.findIndex(a => a.p >= q);
      const b = anchors[Math.max(1, right)], a = anchors[Math.max(0, right - 1)], u = (q - a.p) / (b.p - a.p);
      frames.push({ transform: `scaleX(${lerp(a.x, b.x, u)}) scaleY(${lerp(a.y, b.y, u)})`, backgroundColor: mixColor(a.c, b.c, u), opacity: lerp(a.a, b.a, u), offset: t * e });
    }
    frames.push({ transform: 'scaleX(1) scaleY(1)', backgroundColor: s.color, opacity: alpha, offset: holdEnd });
    for (let i = 1; i <= 64; i++) {
      const t = i / 64, q = quartOut(t);
      frames.push({ transform: `scaleX(1) scaleY(${lerp(1, .025, q)})`, backgroundColor: mixColor(s.color, '#ffffff', q), opacity: alpha * (1 - q), offset: holdEnd + t * (1-holdEnd) });
    }
    return frames;
  }
  const lerp = (a, b, t) => a + (b - a) * t;
  function quartOut(t) { return 1 - (1 - Math.min(1, Math.max(0, t))) ** 4; }
  function mixColor(a, b, t) {
    const rgb = hex => [1, 3, 5].map(i => parseInt(hex.slice(i, i+2), 16));
    const x = rgb(a), y = rgb(b);
    return '#' + x.map((v, i) => Math.round(lerp(v, y[i], t)).toString(16).padStart(2, '0')).join('');
  }
  function feedContentFrames(s) {
    const total = feedTiming(s).total, start = s.enter * .55 / total, enterEnd = s.enter / total, exitStart = (s.enter + s.hold) / total;
    const frames = [{ opacity: 0, offset: 0 }, { opacity: 0, offset: start }];
    for (let i = 1; i <= 64; i++) frames.push({ opacity: quartOut(i / 64), offset: start + i / 64 * (enterEnd-start) });
    frames.push({ opacity: 1, offset: exitStart });
    for (let i = 1; i <= 64; i++) frames.push({ opacity: 1-quartOut(i / 64), offset: exitStart + i / 64 * (1-exitStart) });
    return frames;
  }
  function statsVisible(s, tab) {
    return ['stats', 'all'].includes(tab) && (s.edit || (s.items || [{ visible: true }]).some(item => statItemVisible(item, s)));
  }
  function statItemVisible(item, s) {
    const mode = item.displayMode && item.displayMode !== 'inherit' ? item.displayMode : s.displayMode;
    return item.visible !== false && mode !== 'hidden' && (mode !== 'round-start' || s.roundPhase === 'freezetime');
  }
  function snapPosition(x, y, w, h, boundsW, boundsH, peers, options = {}) {
    const raw = clampPosition(x, y, w, h, boundsW, boundsH);
    if (options.enabled === false) return { ...raw, guides: {} };
    const grid = Math.max(1, Number(options.grid) || 10), tolerance = Math.max(0, Number(options.tolerance) || 6);
    function axis(value, length, bound, positions) {
      let best = null;
      const lines = [0, bound/2, bound, ...positions];
      for (const line of lines) for (const anchor of [0, length/2, length]) {
        const target = line-anchor, distance = Math.abs(target-value);
        if (target >= 0 && target <= bound-length && distance <= tolerance && (!best || distance < best.distance)) best = { value: target, line, distance };
      }
      if (best) return best;
      return { value: Math.max(0, Math.min(bound-length, Math.round(value/grid)*grid)) };
    }
    const a = axis(raw.x, w, boundsW, peers.flatMap(p => [p.x, p.x+p.w/2, p.x+p.w]));
    const b = axis(raw.y, h, boundsH, peers.flatMap(p => [p.y, p.y+p.h/2, p.y+p.h]));
    return { x: a.value, y: b.value, guides: { x: a.line, y: b.line } };
  }
  function clampPosition(x, y, w, h, boundsW = 410, boundsH = 160) {
    return { x: Math.round(Math.max(0, Math.min(boundsW - w, Number(x) || 0))), y: Math.round(Math.max(0, Math.min(boundsH - h, Number(y) || 0))) };
  }
  function keyLabel(code) {
    const names = { Space: 'SPACE', ShiftLeft: 'L SHIFT', ShiftRight: 'R SHIFT', ControlLeft: 'L CTRL', ControlRight: 'R CTRL', AltLeft: 'L ALT', AltRight: 'R ALT', ArrowUp: '↑', ArrowDown: '↓', ArrowLeft: '←', ArrowRight: '→' };
    return names[code] || code.replace(/^(Key|Digit)/, '').toUpperCase().slice(0, 12);
  }
  const api = { defaults, recap, feedTiming, feedPhase, nudge, liveEvent, hitGeometry, hitColor, keyDisplayMode, quartOut, feedSurfaceFrames, feedContentFrames, statsVisible, statItemVisible, snapPosition, clampPosition, keyLabel };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.LabModel = api;
})(typeof window !== 'undefined' ? window : globalThis);
