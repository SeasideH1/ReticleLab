'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const M = window.LabModel;
  let state = structuredClone(M.defaults), tab = 'crosshair', effects = [], time = 0, total = 900, playing = false, lastTick = 0, eventId = 0;
  let toastTimeout, saveTimeout;
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const key = 'reticle-lab-v1';
  const titles = { crosshair: '准心反馈', kill: '击杀提示', stats: '实时统计', input: '键鼠显示', all: '组合预览' };
  const notes = { crosshair: '每次事件独立播放。连续命中时，新的标记叠加，不打断已有动效。', kill: '武器 + 伤害统计。每次击杀新增独立提示条，支持连续击杀叠放与独立定位。', stats: '支持一直显示或仅回合开始显示。可选择统计项、修改标题并拖动布局；购买菜单打开时降低整体不透明度。', input: '点击画布检测真实键鼠。编辑布局时可拖动各按键、鼠标与轨迹面板；失焦自动释放。', all: '四个控件各自保留参数。先独立调校，再在这里检查组合效果与视觉层次。' };
  try {
    const saved = JSON.parse(localStorage.getItem(key));
    if (saved) for (const group of Object.keys(state)) for (const k of Object.keys(state[group])) {
      const v = saved[group]?.[k], d = state[group][k];
      if (v !== null && typeof d !== 'object' && typeof v === typeof d && (typeof v !== 'number' || Number.isFinite(v)) && (typeof v !== 'string' || v.length < 80)) state[group][k] = v;
    }
  } catch { /* File mode and private browsing may deny local storage. */ }
  // Migrate the previous default yellow without resetting custom colours or layouts.
  if (state.crosshair.headKillColor === '#ffd447') state.crosshair.headKillColor = M.defaults.crosshair.headKillColor;
  state.stats.buyMenu = false;
  state.stats.roundPhase = 'live';
  const overlays = new window.LabOverlays({ stage: $('stage'), getState: () => state, getTab: () => tab, save: () => save(), toast: message => toast(message), refresh: () => { renderControls(); applyStyles(); } });
  try { overlays.restore(JSON.parse(localStorage.getItem(key))); } catch {}
  state.input.edit = state.stats.edit = false;
  function save() { clearTimeout(saveTimeout); saveTimeout = setTimeout(() => { try { localStorage.setItem(key, JSON.stringify(state)); } catch {} }, 180); }
  function toast(message) { $('toast').textContent = message; $('toast').classList.add('visible'); clearTimeout(toastTimeout); toastTimeout = setTimeout(() => $('toast').classList.remove('visible'), 2400); }
  function escape(s) { return String(s).replace(/[&<>"']/g, x => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[x])); }
  function section(title, content) { return `<section class="control-section"><h3 class="control-heading">${title}</h3>${content}</section>`; }
  function range(group, name, label, min, max, step = 1, unit = '') {
    const v = state[group][name];
    return `<label class="control-row"><span class="control-caption">${label}<output id="out-${name}">${v}${unit}</output></span><input type="range" data-group="${group}" data-key="${name}" data-unit="${unit}" min="${min}" max="${max}" step="${step}" value="${v}" aria-label="${label}"></label>`;
  }
  function number(group, name, label, min = 0, max = 99999) { return `<label class="control-row"><span class="control-caption">${label}</span><input type="number" data-group="${group}" data-key="${name}" min="${min}" max="${max}" step="1" value="${state[group][name]}"></label>`; }
  function text(group, name, label) { return `<label class="control-row"><span class="control-caption">${label}</span><input type="text" data-group="${group}" data-key="${name}" maxlength="24" value="${escape(state[group][name])}"></label>`; }
  function color(group, name, label) { return `<label class="color-row"><span class="control-caption">${label}</span><span class="color-set"><code id="out-${name}">${escape(state[group][name])}</code><input type="color" data-group="${group}" data-key="${name}" value="${escape(state[group][name])}" aria-label="${label}"></span></label>`; }
  function toggle(group, name, label) { return `<label class="toggle-row"><input type="checkbox" data-group="${group}" data-key="${name}" ${state[group][name] ? 'checked' : ''}>${label}</label>`; }
  function grid(content) { return `<div class="control-grid">${content}</div>`; }
  function position(group) { return section('位置 · 每次 1 px', grid(number(group, 'x', '水平偏移', -2000, 2000) + number(group, 'y', '垂直偏移', -2000, 2000)) + '<div class="position-pad"><button data-nudge="ArrowLeft" aria-label="左移 1 像素">←</button><button data-nudge="ArrowUp" aria-label="上移 1 像素">↑</button><button data-nudge="ArrowDown" aria-label="下移 1 像素">↓</button><button data-nudge="ArrowRight" aria-label="右移 1 像素">→</button></div><p class="control-note">零点为此控件默认锚点。画布获得焦点后，方向键也可微调。</p>'); }
  function renderControls() {
    $('inspector-title').textContent = tab === 'all' ? '组合调校' : titles[tab] + '参数';
    $('inspector-note').textContent = notes[tab];
    let content = '';
    if (tab === 'crosshair') content =
      section('样式 · COD16 LIKE', range(tab, 'size', '准心线长', 2, 24, .5, ' px') + range(tab, 'gap', '中心间隙', 0, 16, .5, ' px') + range(tab, 'thickness', '线条粗细', .5, 4, .5, ' px') + color(tab, 'color', '基础准心') + toggle(tab, 'dot', '显示中心点')) +
      section('事件动效', range(tab, 'effectSize', '命中标记尺寸', 8, 35, 1, ' px') + range(tab, 'effectWidth', '标记粗细', 1, 5, .5, ' px') + range(tab, 'duration', '命中持续时间', 120, 1000, 10, ' ms') + color(tab, 'hitColor', '普通命中') + color(tab, 'headColor', '爆头命中') + color(tab, 'killColor', '普通击杀') + color(tab, 'headKillColor', '爆头击杀')) + position(tab);
    if (tab === 'kill') content =
      section('提示内容', text(tab, 'name', '击杀使用的武器') + text(tab, 'value', '造成伤害统计（演示值）') + '<p class="control-note">原参考为“猎空 / 100”，此版按新需求使用“武器 / 伤害”。</p>') +
      section('外观', color(tab, 'color', '色块颜色') + range(tab, 'opacity', '背景不透明度', 10, 100, 1, '%') + range(tab, 'radius', '圆角', 0, 18, 1, ' px') + range(tab, 'width', '宽度', 150, 320, 1, ' px') + range(tab, 'scale', '整体缩放', 60, 150, 1, '%')) +
      section('动画节奏', range(tab, 'enter', '矩形扩宽 / 白到红', 100, 900, 10, ' ms') + range(tab, 'hold', '停留', 300, 4000, 50, ' ms') + range(tab, 'exit', '纵向收束 / 渐隐', 160, 1200, 20, ' ms') + '<p class="control-note">入场与淡出分别使用四次方缓出：1 − (1 − t)⁴。淡出宽度保持不变，仅高度收缩，矩形和文字透明度同步缓出。</p>') + position(tab);
    if (tab === 'stats') content =
      section('本回合实时样例', grid(number(tab, 'round', '当前回合', 1, 99) + number(tab, 'kills', '击杀数', 0, 99)) + grid(number(tab, 'deaths', '死亡数', 0, 99) + number(tab, 'assists', '助攻数', 0, 99)) + grid(number(tab, 'hsKills', '爆头击杀数', 0, 99) + number(tab, 'damage', '回合伤害', 0, 99999)) + grid(number(tab, 'hits', '全部命中', 0, 999) + number(tab, 'hsHits', '头部命中', 0, 999)) + grid(number(tab, 'startMoney', '开始余额 $', 0, 99999) + number(tab, 'endMoney', '当前余额 $', 0, 99999)) + number(tab, 'totalDamage', '累计伤害', 0, 999999)) +
      overlays.statsControls() +
      section('显示与购买菜单', color(tab, 'color', '统计文字颜色') + range(tab, 'opacity', '底色不透明度', 0, 100, 1, '%') + range(tab, 'scale', '整体缩放', 60, 160, 1, '%') + toggle(tab, 'conservative', 'GSI 保守预览（命中 HS 未知）') + toggle(tab, 'buyMenu', '模拟购买菜单打开 · B') + range(tab, 'buyOpacity', '购买菜单打开时整体不透明度', 0, 60, 1, '%')) + position(tab);
    if (tab === 'input') content = overlays.inputControls() + section('外观与轨迹', color(tab, 'color', '按下高亮颜色') + range(tab, 'opacity', '按键底色不透明度', 0, 100, 1, '%') + range(tab, 'scale', '整体缩放', 60, 150, 1, '%') + range(tab, 'trailLife', '轨迹保留时间', 200, 3000, 100, ' ms') + range(tab, 'idleCenter', '静止后归中等待', 500, 5000, 100, ' ms') + range(tab, 'sensitivity', '轨迹位移倍率', .2, 3, .1, '×')) + position(tab);
    if (tab === 'all') content = section('独立调校', '<div class="combined-actions"><button data-go="crosshair">01　准心与命中动效　↗</button><button data-go="kill">02　击杀提示与位置　↗</button><button data-go="stats">03　实时统计与布局　↗</button><button data-go="input">04　键鼠检测与布局　↗</button></div>') + section('场景', toggle('stats', 'buyMenu', '模拟购买菜单打开 · B') + toggle('stats', 'conservative', 'GSI 保守预览') + '<p class="control-note">点击画布检测按键与鼠标。已配置的检测键优先，不触发同名事件快捷键。其它演示操作可使用下方按钮。</p>');
    $('controls').innerHTML = content;
    $('controls').querySelectorAll('input[data-group]').forEach(input => input.addEventListener('input', () => {
      const group = input.dataset.group, name = input.dataset.key;
      let value = input.type === 'checkbox' ? input.checked : input.type === 'text' || input.type === 'color' ? input.value : Number(input.value);
      if (input.type === 'number' || input.type === 'range') {
        if (input.value === '' || !Number.isFinite(value)) return;
        value = Math.min(Number(input.max), Math.max(Number(input.min), value));
        if (input.type === 'number') value = Math.round(value);
        input.value = value;
      }
      state[group][name] = value;
      const out = $('out-' + name); if (out) out.textContent = value + (input.dataset.unit || '');
      if (group === 'stats') {
        state.stats.hsKills = Math.min(state.stats.hsKills, state.stats.kills);
        state.stats.hsHits = Math.min(state.stats.hsHits, state.stats.hits);
        for (const k of ['hsKills', 'hsHits']) { const field = $('controls').querySelector(`[data-key="${k}"]`); if (field) field.value = state.stats[k]; }
      }
      applyStyles();
      if (group === 'kill') { replay(false); playing = false; time = state.kill.enter + 150; renderFrame(); }
      renderFrame();
      save();
    }));
    overlays.bindControls();
    $('controls').querySelectorAll('[data-nudge]').forEach(button => button.addEventListener('click', () => move(button.dataset.nudge)));
    $('controls').querySelectorAll('[data-go]').forEach(button => button.addEventListener('click', () => selectTab(button.dataset.go)));
  }
  function updateMetrics() { overlays.renderStats(); }
  function applyStyles() {
    const c = state.crosshair, k = state.kill, s = state.stats;
    $('crosshair-path').setAttribute('d', `M${-c.gap-c.size} 0H${-c.gap}M${c.gap} 0H${c.gap+c.size}M0 ${-c.gap-c.size}V${-c.gap}M0 ${c.gap}V${c.gap+c.size}`);
    $('crosshair-path').setAttribute('stroke', c.color); $('crosshair-path').setAttribute('stroke-width', c.thickness);
    $('crosshair-dot').setAttribute('fill', c.color); $('crosshair-dot').style.display = c.dot ? '' : 'none';
    $('crosshair-anchor').style.transform = `translate(${c.x}px,${c.y}px)`;
    $('feed-anchor').style.transform = `translate(${k.x}px,${k.y}px) scale(${k.scale / 100})`;
    $('stage').style.setProperty('--feed-color', k.color); $('stage').style.setProperty('--feed-opacity', k.opacity / 100); $('stage').style.setProperty('--feed-radius', k.radius + 'px');
    overlays.render();
    updateMetrics();
  }
  function move(direction) {
    if (!['crosshair', 'kill', 'stats', 'input'].includes(tab)) return;
    M.nudge(state[tab], direction); applyStyles(); save();
    for (const k of ['x', 'y']) { const i = $('controls').querySelector(`[data-key="${k}"]`); if (i) i.value = state[tab][k]; }
  }
  function clearEffects() {
    effects.forEach(e => e.animations.forEach(a => a.cancel())); effects = [];
    $('effect-layer').replaceChildren(); $('kill-feed').replaceChildren();
    time = 0; total = 0;
  }
  function anim(node, frames, duration) {
    const a = node.animate(frames, { duration: Math.max(1, duration), fill: 'both', easing: 'linear' }); a.pause(); a.currentTime = 0; return a;
  }
  function addEffect(type, node, animations, duration, start) { effects.push({ id: ++eventId, type, node, animations, duration, start }); total = Math.max(total, start + duration); }
  function hitEffect(kind, start) {
    const s = state.crosshair, kill = kind === 'kill' || kind === 'headkill', head = kind === 'headshot' || kind === 'headkill';
    const tint = M.hitColor(kind, s);
    const size = s.effectSize, gap = size * .47, end = size, duration = kill ? s.duration * 1.7 : s.duration;
    const node = document.createElement('div'); node.className = 'hit-effect'; node.dataset.event = String(eventId + 1);
    const geometry = M.hitGeometry(kind, size);
    node.innerHTML = `<svg width="140" height="140" viewBox="-70 -70 140 140" aria-hidden="true"><g fill="none" stroke="${escape(tint)}" stroke-width="${s.effectWidth}" stroke-linecap="square"><path d="${geometry.cross}"/></g></svg>`;
    $('effect-layer').append(node);
    const frames = reduced ? [{ opacity: 1 }, { opacity: 1, offset: .7 }, { opacity: 0 }] : [
      { opacity: 0, transform: 'scale(1.4)', offset: 0 }, { opacity: 1, transform: 'scale(.93)', offset: .12 }, { opacity: 1, transform: 'scale(1)', offset: .3 }, { opacity: .9, transform: 'scale(1)', offset: .5 }, { opacity: 0, transform: 'scale(1.16)', offset: 1 }
    ];
    addEffect('hit', node, [anim(node, frames, duration)], duration, start);
  }
  const skull = '<svg class="kill-emblem" viewBox="0 0 40 40" aria-hidden="true"><g fill="currentColor"><path d="M3 1l9 7 18 21 7 10-11-8L7 10zM37 1l-9 7L10 29 3 39l11-8 19-21z"/><path d="M20 6C11 6 6 13 8 21c1 5 5 7 8 8v5h3v-4h2v4h3v-5c4-1 7-4 8-8 2-8-3-15-12-15zm-8 10 7 4-3 4-5-3zm16 0 1 5-5 3-3-4zm-8 7 3 4h-6z" fill-rule="evenodd"/></g></svg>';
  function feedEffect(start) {
    const s = state.kill, d = M.feedTiming(s), e = d.enter / d.total, endHold = (d.enter + d.hold) / d.total;
    const node = document.createElement('div'); node.className = 'kill-wrapper'; node.style.width = s.width + 'px'; node.style.height = s.height + 'px';
    node.innerHTML = `<div class="kill-banner"><div class="kill-surface"></div><div class="kill-content">${skull}<span class="kill-name"></span><span class="kill-number"></span></div></div>`;
    node.querySelector('.kill-name').textContent = s.name || 'AK-47'; node.querySelector('.kill-number').textContent = s.value || '0';
    // Most recent kill remains closest to the anchor. Older notifications retain independent clocks.
    $('kill-feed').prepend(node);
    let animations;
    if (reduced) {
      animations = [anim(node, M.feedContentFrames(s), d.total)];
    } else {
      animations = [
        anim(node.querySelector('.kill-surface'), M.feedSurfaceFrames(s), d.total),
        anim(node.querySelector('.kill-content'), M.feedContentFrames(s), d.total)
      ];
    }
    addEffect('feed', node, animations, d.total, start);
  }
  function emit(kind, start) {
    if (['round', 'live', 'death', 'assist'].includes(kind)) return;
    if (tab === 'crosshair' || tab === 'all') hitEffect(kind, start);
    if ((kind === 'kill' || kind === 'headkill') && (tab === 'kill' || tab === 'all')) feedEffect(start);
  }
  function trigger(kind) {
    if (tab === 'stats' || tab === 'all') {
      const kinds = kind === 'burst' ? ['hit', 'hit', 'headshot', 'kill', 'headkill'] : [kind];
      kinds.forEach(k => M.liveEvent(state.stats, k)); renderControls(); applyStyles(); save();
    }
    if (time >= total) clearEffects();
    if (kind === 'burst') {
      ['hit', 'hit', 'headshot', 'kill', 'headkill'].forEach((k, i) => emit(tab === 'kill' ? 'kill' : k, time + i * 135));
    } else emit(kind, time);
    playing = true; lastTick = performance.now(); renderFrame();
  }
  function replay(play = true) {
    clearEffects();
    if (tab === 'crosshair') emit('headkill', 0);
    if (tab === 'kill') feedEffect(0);
    if (tab === 'all') emit('headkill', 0);
    time = 0; playing = play; lastTick = performance.now(); renderFrame();
  }
  function selectTab(next) {
    tab = next;
    document.querySelectorAll('[data-tab]').forEach(b => b.setAttribute('aria-pressed', b.dataset.tab === tab ? 'true' : 'false'));
    $('preview-title').textContent = titles[tab];
    overlays.release();
    $('crosshair-anchor').hidden = !['crosshair', 'all'].includes(tab);
    $('feed-anchor').hidden = !['kill', 'all'].includes(tab);
    $('stats-anchor').hidden = !['stats', 'all'].includes(tab);
    $('input-anchor').hidden = !['input', 'all'].includes(tab);
    $('input-toolbar').hidden = !['input', 'all'].includes(tab);
    $('transport').hidden = ['stats', 'input'].includes(tab);
    document.querySelectorAll('[data-event]').forEach(b => {
      const kind = b.dataset.event;
      b.hidden = tab === 'input' || (tab === 'kill' ? !['kill', 'headkill', 'burst'].includes(kind) : tab === 'crosshair' ? ['round', 'live', 'death', 'assist'].includes(kind) : false);
    });
    $('stage-hint').textContent = tab === 'stats' ? '本回合实时统计 · B 模拟购买菜单' : tab === 'input' ? '点击画布后操作键鼠 · 编辑模式支持拖拽' : tab === 'kill' ? 'K 击杀 · L 爆头击杀 · 方向键微调位置' : 'H 命中 · J 爆头命中 · K 击杀 · L 爆头击杀';
    renderControls(); applyStyles(); replay(false);
    // Useful first frame: still preview at the hold phase; no unsolicited looping animation.
    time = tab === 'kill' ? state.kill.enter + 200 : tab === 'all' ? 150 : tab === 'crosshair' ? 150 : 0; renderFrame();
  }
  function renderFrame() {
    for (const effect of effects) {
      const local = time - effect.start;
      effect.node.style.visibility = local >= 0 && local < effect.duration ? 'visible' : 'hidden';
      if (effect.type === 'feed') effect.node.style.display = local >= 0 && local < effect.duration ? '' : 'none';
      effect.animations.forEach(a => { a.currentTime = Math.max(0, Math.min(effect.duration, local)); });
    }
    $('timeline').max = Math.max(1, total); $('timeline').value = time;
    $('timeline-time').textContent = `${Math.round(time)} / ${Math.round(total)} ms`;
    $('pause').textContent = playing ? 'Ⅱ' : '▷'; $('pause').setAttribute('aria-label', playing ? '暂停动效' : '继续动效');
    $('phase-label').textContent = time >= total ? '播放完毕' : tab === 'kill' ? (effects.length > 1 ? '连续击杀 · 各自独立播放' : M.feedPhase(time, state.kill)) : tab === 'stats' ? (state.stats.buyMenu ? '购买菜单打开 · 降低不透明度' : '实时统计 · 常驻显示') : playing ? '独立事件播放中' : '已暂停 · 可拖动时间轴';
  }
  function tick(now) {
    overlays.tick(now);
    if (playing) {
      time = Math.min(total, time + Math.min(100, now - lastTick) * Number($('speed').value));
      if (time >= total) playing = false;
      renderFrame();
    }
    lastTick = now; requestAnimationFrame(tick);
  }
  document.querySelectorAll('[data-tab]').forEach(b => b.addEventListener('click', () => selectTab(b.dataset.tab)));
  document.querySelectorAll('[data-event]').forEach(b => b.addEventListener('click', () => trigger(b.dataset.event)));
  $('replay').addEventListener('click', () => replay());
  $('pause').addEventListener('click', () => { if (time >= total) replay(); else { playing = !playing; lastTick = performance.now(); renderFrame(); } });
  $('timeline').addEventListener('input', e => { playing = false; time = Number(e.target.value); renderFrame(); });
  $('background').addEventListener('change', e => { $('stage').dataset.background = e.target.value; });
  $('guides').addEventListener('change', e => $('stage').classList.toggle('show-guides', e.target.checked));
  $('fullscreen').addEventListener('click', async () => { try { if (document.fullscreenElement) await document.exitFullscreen(); else await $('stage').requestFullscreen(); } catch { toast('当前浏览器不支持全屏，请最大化窗口预览。'); } });
  $('reset').addEventListener('click', () => { if (tab === 'all') state = structuredClone(M.defaults); else state[tab] = structuredClone(M.defaults[tab]); save(); selectTab(tab); toast('已恢复默认参数'); });
  document.addEventListener('keydown', e => {
    if (overlays.ownsKeyboard(e)) return;
    if (e.ctrlKey || e.altKey || e.metaKey || e.isComposing || /^(INPUT|TEXTAREA|SELECT|BUTTON)$/.test(e.target.tagName)) return;
    if (e.key.startsWith('Arrow') && e.target === $('stage')) { e.preventDefault(); move(e.key); return; }
    if (e.repeat) return;
    const kind = { h: 'hit', j: 'headshot', k: 'kill', l: 'headkill', r: 'round' }[e.key.toLowerCase()];
    if (kind) {
      if (tab === 'input' || tab === 'kill' && !['kill', 'headkill'].includes(kind) || tab === 'crosshair' && kind === 'round') return;
      e.preventDefault(); trigger(kind);
    }
    if (e.key.toLowerCase() === 'b' && (tab === 'stats' || tab === 'all')) { state.stats.buyMenu = !state.stats.buyMenu; renderControls(); applyStyles(); renderFrame(); }
  });
  $('export').addEventListener('click', () => {
    const data = { schemaVersion: 5, description: '四控件视觉参数与独立布局；游戏事件为模拟数据，键鼠输入限预览焦点。', units: 'CSS px', exportedAt: new Date().toISOString(), controls: state };
    const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'reticle-lab-settings.json'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); toast('参数已导出为 JSON');
  });
  const refs = [2, 3, 4, 5, 1, 6, 7, 8, 9, 10];
  const refTimes = [0.14, .45, .68, .88, 'hold', 'exit1', 'exit2', 'exit3', 'exit4', 'exit5'];
  refs.forEach((image, i) => {
    const b = document.createElement('button'); b.className = 'ref-frame'; b.setAttribute('aria-label', `参考帧 ${i + 1}：在时间轴中对照`);
    b.innerHTML = `<span>${String(i + 1).padStart(2, '0')}</span><img src="assets/motion-${String(image).padStart(2, '0')}.svg" alt="原创动效示意帧 ${image}">`;
    b.addEventListener('click', () => {
      if (tab !== 'kill') selectTab('kill'); replay(false);
      const t = refTimes[i], s = state.kill;
      time = typeof t === 'number' ? t * s.enter : t === 'hold' ? s.enter + s.hold / 2 : s.enter + s.hold + s.exit * ({ exit1: .18, exit2: .4, exit3: .6, exit4: .8, exit5: .96 }[t]);
      document.querySelectorAll('.ref-frame').forEach(x => x.classList.toggle('active', x === b)); renderFrame();
    });
    $('references').append(b);
  });
  new ResizeObserver(entries => { const r = entries[0].contentRect; $('stage-dimensions').textContent = `${Math.round(r.width)} × ${Math.round(r.height)} · CSS PX`; }).observe($('stage'));
  selectTab('crosshair'); requestAnimationFrame(tick);
})();
