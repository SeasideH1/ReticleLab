'use strict';
(() => {
  const M = window.LabModel, $ = id => document.getElementById(id);
  const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const section = (title, body) => `<section class="control-section"><h3 class="control-heading">${title}</h3>${body}</section>`;
  const select = (id, label, options, value) => `<label class="control-row"><span class="control-caption">${label}</span><select id="${id}">${options.map(([v, l]) => `<option value="${esc(v)}" ${v === value ? 'selected' : ''}>${esc(l)}</option>`).join('')}</select></label>`;
  const toggle = (id, label, checked) => `<label class="toggle-row"><input type="checkbox" id="${id}" ${checked ? 'checked' : ''}>${label}</label>`;
  const field = (id, label, value, type = 'text', min = 0, max = 410) => `<label class="control-row"><span class="control-caption">${label}</span><input id="${id}" type="${type}" value="${esc(value)}" ${type === 'number' ? `min="${min}" max="${max}" step="1"` : 'maxlength="18"'}></label>`;
  class LabOverlays {
    constructor(options) {
      Object.assign(this, options);
      this.pressed = new Set(); this.buttons = 0; this.points = []; this.traceCursor = { x: 45, y: 48 }; this.previous = null;
      this.capture = false; this.demoUntil = 0; this.drag = null; this.lastMove = null; this.scales = { stats: 1, input: 1 };
      document.addEventListener('keydown', e => this.keydown(e), true);
      document.addEventListener('keyup', e => { this.pressed.delete(e.code); this.updatePressed(); }, true);
      window.addEventListener('blur', () => this.release());
      document.addEventListener('visibilitychange', () => { if (document.hidden) this.release(); });
      this.stage.addEventListener('blur', () => this.release());
      this.stage.addEventListener('pointerdown', e => this.pointerdown(e));
      this.stage.addEventListener('pointermove', e => this.pointermove(e));
      window.addEventListener('pointerup', e => this.pointerup(e));
      window.addEventListener('pointercancel', () => this.release());
      this.stage.addEventListener('pointerleave', () => { this.previous = null; if (!this.drag) { this.buttons = 0; this.updatePressed(); } });
      this.stage.addEventListener('contextmenu', e => { if (this.active()) e.preventDefault(); });
      this.stage.addEventListener('wheel', () => { if (this.active() && document.activeElement === this.stage) this.wheelUntil = performance.now() + 140; }, { passive: true });
      this.stage.addEventListener('focus', () => this.status());
      $('input-demo').addEventListener('click', () => { this.release(); this.demoUntil = performance.now() + 3600; this.status('模拟键鼠输入 · 3.6 秒'); });
      $('input-focus').addEventListener('click', () => { this.stage.focus({ preventScroll: true }); this.status(); });
      new ResizeObserver(() => this.fit()).observe(this.stage);
    }
    restore(saved) {
      if (!saved) return;
      const s = this.getState();
      if (Array.isArray(saved.stats?.items)) {
        for (const item of s.stats.items) {
          const old = saved.stats.items.find(x => x && x.id === item.id); if (!old) continue;
          if (typeof old.label === 'string') item.label = old.label.slice(0, 18);
          if (typeof old.visible === 'boolean') item.visible = old.visible;
          if (['inherit', 'always', 'round-start', 'hidden'].includes(old.displayMode)) item.displayMode = old.displayMode;
          item.w = Math.max(65, Math.min(325, Number(old.w) || item.w));
          Object.assign(item, M.clampPosition(old.x, old.y, item.w, 48, 330, 220));
        }
      }
      if (Array.isArray(saved.input?.keys)) {
        const seen = new Set(), keys = [];
        for (const item of saved.input.keys.slice(0, 16)) {
          if (!item || typeof item.code !== 'string' || !/^[A-Za-z][A-Za-z0-9]{0,29}$/.test(item.code) || seen.has(item.code)) continue;
          seen.add(item.code);
          const w = Math.max(32, Math.min(200, Number(item.w) || 43));
          keys.push({ code: item.code, label: typeof item.label === 'string' ? item.label.slice(0, 18) : M.keyLabel(item.code), displayMode: M.keyDisplayMode(item, saved.input.displayMode), w, ...M.clampPosition(item.x, item.y, w, 42) });
        }
        if (keys.length) s.input.keys = keys;
      }
      for (const name of ['mouse', 'trace']) if (saved.input?.[name]) {
        Object.assign(s.input[name], M.clampPosition(saved.input[name].x, saved.input[name].y, name === 'mouse' ? 60 : 104, 145));
        const mode = saved.input[name].displayMode;
        if ((name === 'mouse' ? ['background', 'pressed', 'hidden'] : ['always', 'moving', 'hidden']).includes(mode)) s.input[name].displayMode = mode;
        else if (name === 'mouse') s.input.mouse.displayMode = M.keyDisplayMode({}, saved.input.displayMode);
      }
      if (!['solid', 'outline', 'rounded'].includes(s.input.style)) s.input.style = 'rounded';
      if (!['background', 'pressed'].includes(s.input.displayMode)) s.input.displayMode = 'background';
    }
    active() { return ['input', 'all'].includes(this.getTab()) && this.getState().input.enabled; }
    ownsKeyboard(e) { return this.capture || (this.active() && document.activeElement === this.stage && this.getState().input.keys.some(k => k.code === e.code)); }
    keydown(e) {
      if (this.capture) {
        e.preventDefault(); e.stopImmediatePropagation();
        if (e.key === 'Escape') { this.capture = false; this.refresh(); return; }
        if (e.repeat || !e.code || e.isComposing) return;
        const s = this.getState().input, current = s.keys.find(k => k.code === s.selected);
        if (s.keys.some(k => k.code === e.code && k !== current)) { this.toast('这个按键已存在，请选择其它按键'); return; }
        if (current) { current.code = e.code; current.label = M.keyLabel(e.code); s.selected = e.code; }
        this.capture = false; this.save(); this.refresh(); return;
      }
      if (!this.active() || document.activeElement !== this.stage || this.getState().input.edit) return;
      if (this.getState().input.keys.some(k => k.code === e.code)) {
        // Tab and browser/system combinations keep their native navigation behaviour.
        if (e.key !== 'Tab' && !e.metaKey && !e.altKey && !e.ctrlKey) e.preventDefault();
        this.pressed.add(e.code); this.updatePressed();
      }
    }
    release() {
      this.clearSnapGuides();
      this.pressed.clear(); this.buttons = 0; this.previous = null; this.demoUntil = 0; this.wheelUntil = 0; this.drag = null;
      if (this.capture) { this.capture = false; const b = $('key-capture'); if (b) b.textContent = '重新录入检测键'; }
      this.updatePressed(); this.status();
    }
    status(message) {
      const node = $('input-status'); if (!node) return;
      node.textContent = message || (this.getState().input.edit ? '编辑布局 · 拖动元素，检测暂停' : !this.getState().input.enabled ? '检测已关闭' : document.activeElement === this.stage ? '正在检测预览内输入' : '等待画布焦点');
    }
    statsControls() {
      const s = this.getState().stats, item = s.items.find(i => i.id === s.selected) || s.items[0];
      s.selected = item.id;
      return section('显示时机', select('stats-mode', '显示模式', [['always', '一直显示'], ['round-start', '仅回合开始时显示']], s.displayMode) + select('stats-phase', '模拟当前阶段', [['freezetime', '回合开始 / 准备阶段'], ['live', '交战阶段']], s.roundPhase) + '<p class="control-note">仅回合开始模式：准备阶段显示，交战阶段隐藏。编辑布局时临时显示，统计数据持续更新。</p>') + section('自由统计布局', toggle('stats-edit', '编辑布局 · 拖动统计项', s.edit) + select('stats-select', '选择统计项（含已隐藏项）', s.items.map(i => [i.id, i.label + (i.visible ? '' : ' · 隐藏')]), item.id) + toggle('stats-visible', '启用这个统计项', item.visible) + select('stats-item-mode', '所选统计项显示模式', [['inherit', '跟随整体模式'], ['always', '一直显示'], ['round-start', '仅回合开始显示'], ['hidden', '隐藏']], item.displayMode || 'inherit') + field('stats-label', '显示名称', item.label) + `<div class="control-grid">${field('stats-item-x', '项目 X', item.x, 'number', 0, 330)}${field('stats-item-y', '项目 Y', item.y, 'number', 0, 220)}</div>` + field('stats-item-width', '项目宽度', item.w, 'number', 65, 325) + '<p class="control-note">包括 K/D/A、当前金钱、击杀、死亡、助攻、两种 HS 比例、回合伤害、累计伤害和净增经济。编辑时隐藏项以虚线占位，拖动后自动保存。逐项模式可覆盖整体默认。</p>') + this.snapControls('stats');
    }
    inputControls() {
      const s = this.getState().input, item = s.keys.find(k => k.code === s.selected) || s.keys[0]; s.selected = item.code;
      return section('输入与显示方式', toggle('input-enabled', '检测真实键鼠输入', s.enabled) + select('input-mode', '新增按键默认模式', [['pressed', 'A · 按下时才显示'], ['background', 'B · 半透明底色，按下填充']], s.displayMode) + select('input-style', '按键样式', [['solid', '简洁方块'], ['outline', '描边'], ['rounded', '圆角']], s.style) + toggle('input-trail', '显示鼠标移动轨迹', s.trail) + '<p class="control-note">每个按键在下方独立设置 A/B。上方默认值仅影响新增按键，不覆盖已有按键；轨迹独立。编辑时全部临时显示。点击画布开始，切出窗口自动释放。</p>') +
        section('检测键与拖拽布局', toggle('input-edit', '编辑布局 · 拖动各元素', s.edit) + select('key-select', '选择按键', s.keys.map(k => [k.code, `${k.label} · ${k.code}`]), item.code) + select('key-mode', '所选按键显示模式', [['pressed', 'A · 按下时才显示'], ['background', 'B · 半透明底色，按下填充'], ['hidden', '隐藏']], M.keyDisplayMode(item, s.displayMode)) + field('key-label', '显示文字', item.label) + '<button id="key-capture" class="layout-button">重新录入检测键</button><div class="layout-actions"><button id="key-add">新增按键</button><button id="key-remove">删除所选</button></div>' + `<div class="control-grid">${field('key-x', '按键 X', item.x, 'number', 0, 410)}${field('key-y', '按键 Y', item.y, 'number', 0, 160)}</div>` + field('key-width', '按键宽度', item.w, 'number', 32, 200) + '<p class="control-note">支持最多 16 个按键，区分左右 Shift/Ctrl。编辑时可拖动按键、鼠标、轨迹面板。用 Esc 取消录入；系统保留组合键可能被浏览器截获。</p>') + section('鼠标与轨迹显示模式', select('mouse-mode', '鼠标图形', [['pressed', 'A · 点击时才显示'], ['background', 'B · 常驻背景，点击填充'], ['hidden', '隐藏']], s.mouse.displayMode) + select('trace-mode', '移动轨迹', [['always', '一直显示'], ['moving', '移动时显示（轨迹消失后隐藏）'], ['hidden', '隐藏']], s.trace.displayMode)) + this.snapControls('input');
    }
    bindControls() {
      const bind = (id, handler, event = 'change') => { const node = $(id); if (node) node.addEventListener(event, () => { handler(node); this.save(); this.render(); }); };
      const s = this.getState();
      bind('stats-mode', n => { s.stats.displayMode = n.value; });
      bind('stats-phase', n => { s.stats.roundPhase = n.value; });
      for (const group of ['stats', 'input']) {
        bind(group+'-snap', n => { s[group].snap = n.checked; });
        bind(group+'-grid', n => { s[group].snapGrid = +n.value; });
      }
      bind('stats-edit', n => { s.stats.edit = n.checked; this.release(); });
      bind('stats-select', n => { s.stats.selected = n.value; this.refresh(); });
      const stat = () => s.stats.items.find(i => i.id === s.stats.selected);
      bind('stats-visible', n => { stat().visible = n.checked; });
      bind('stats-item-mode', n => { stat().displayMode = n.value; });
      bind('stats-label', n => { stat().label = n.value || '统计'; }, 'input');
      for (const [id, prop] of [['stats-item-x', 'x'], ['stats-item-y', 'y'], ['stats-item-width', 'w']]) bind(id, n => { const i = stat(); i[prop] = Math.max(+n.min, Math.min(+n.max, +n.value || 0)); Object.assign(i, M.clampPosition(i.x, i.y, i.w, 48, 330, 220)); n.value = i[prop]; });
      bind('input-enabled', n => { s.input.enabled = n.checked; this.release(); });
      bind('input-mode', n => { s.input.displayMode = n.value; });
      bind('mouse-mode', n => { s.input.mouse.displayMode = n.value; });
      bind('trace-mode', n => { s.input.trace.displayMode = n.value; });
      bind('input-style', n => { s.input.style = n.value; });
      bind('input-trail', n => { s.input.trail = n.checked; });
      bind('input-edit', n => { s.input.edit = n.checked; this.release(); });
      bind('key-select', n => { s.input.selected = n.value; this.refresh(); });
      const key = () => s.input.keys.find(k => k.code === s.input.selected);
      bind('key-label', n => { key().label = n.value || M.keyLabel(key().code); }, 'input');
      bind('key-mode', n => { key().displayMode = n.value; });
      for (const [id, prop] of [['key-x', 'x'], ['key-y', 'y'], ['key-width', 'w']]) bind(id, n => { const k = key(); k[prop] = Math.max(+n.min, Math.min(+n.max, +n.value || 0)); Object.assign(k, M.clampPosition(k.x, k.y, k.w, 42)); n.value = k[prop]; });
      bind('key-capture', n => { this.capture = true; n.textContent = '现在按下目标键 · Esc 取消'; }, 'click');
      bind('key-add', () => {
        if (s.input.keys.length >= 16) { this.toast('最多显示 16 个按键'); return; }
        const code = ['KeyE', 'KeyQ', 'KeyR', 'KeyF', 'KeyB', 'KeyG', 'Digit1', 'Digit2', 'Digit3', 'Tab', 'ShiftRight', 'ControlRight', 'KeyZ', 'KeyX', 'KeyC', 'KeyV'].find(c => !s.input.keys.some(k => k.code === c));
        if (!code) return;
        s.input.keys.push({ code, label: M.keyLabel(code), displayMode: s.input.displayMode, x: 0, y: 100, w: 60 }); s.input.selected = code; this.refresh(); this.capture = true; $('key-capture').textContent = '现在按下目标键 · Esc 取消';
      }, 'click');
      bind('key-remove', () => { if (s.input.keys.length === 1) { this.toast('至少保留一个检测键'); return; } s.input.keys = s.input.keys.filter(k => k.code !== s.input.selected); s.input.selected = s.input.keys[0].code; this.refresh(); }, 'click');
    }
    renderStats() {
      const s = this.getState().stats, r = M.recap(s), pct = n => n === null ? '—' : n.toFixed(1) + '%';
      const values = { kda: `${s.kills} / ${s.deaths} / ${s.assists}`, cash: '$' + s.endMoney.toLocaleString('en-US'), kills: s.kills, deaths: s.deaths, assists: s.assists, hskill: pct(r.hsKillPercent), hshit: pct(r.hsHitPercent), net: (r.money < 0 ? '−' : '+') + '$' + Math.abs(r.money).toLocaleString('en-US'), damage: s.damage, totalDamage: s.totalDamage };
      $('stats-canvas').innerHTML = s.items.map(i => `<div class="stat-tile ${M.statItemVisible(i, s) ? '' : 'layout-hidden'} ${i.id === s.selected ? 'selected' : ''}" data-stat="${i.id}" style="left:${i.x}px;top:${i.y}px;width:${i.w}px" ${!M.statItemVisible(i, s) && !s.edit ? 'hidden' : ''}><span>${esc(i.label)}</span><strong>${esc(values[i.id])}</strong></div>`).join('');
      $('stats-anchor').classList.toggle('editing', s.edit); $('stats-anchor').style.setProperty('--stats-color', s.color); $('stats-anchor').style.setProperty('--tile-opacity', s.opacity / 100);
      $('stats-anchor').style.opacity = s.buyMenu ? s.buyOpacity / 100 : 1;
      $('stats-canvas').style.backgroundSize = `${s.snapGrid}px ${s.snapGrid}px`;
      $('stats-anchor').hidden = !M.statsVisible(s, this.getTab());
    }
    renderInput() {
      const s = this.getState().input, canvas = $('input-canvas');
      canvas.innerHTML = s.keys.map(k => `<div class="keycap ${k.code === s.selected ? 'selected' : ''}" data-key-code="${esc(k.code)}" data-mode="${M.keyDisplayMode(k, s.displayMode)}" style="left:${k.x}px;top:${k.y}px;width:${k.w}px">${esc(k.label)}</div>`).join('') +
        `<div class="mouse-panel" data-mouse="true" style="left:${s.mouse.x}px;top:${s.mouse.y}px"><svg viewBox="0 0 60 94" class="mouse-graphic" aria-label="鼠标左右键和中键"><defs><clipPath id="mouse-clip"><rect x="3" y="2" width="54" height="88" rx="27"/></clipPath></defs><g clip-path="url(#mouse-clip)"><rect class="mouse-base" x="3" y="2" width="54" height="88"/><path id="mouse-left" d="M3 2h27v37H3z"/><path id="mouse-right" d="M30 2h27v37H30z"/></g><rect x="3" y="2" width="54" height="88" rx="27" fill="none" stroke="currentColor" stroke-width="2"/><path d="M30 3v36M4 39h52" stroke="currentColor" fill="none"/><rect id="mouse-middle" x="27" y="47" width="6" height="14" rx="3"/></svg></div>` +
        `<div class="trace-panel" data-trace="true" style="left:${s.trace.x}px;top:${s.trace.y}px" ${!s.trail && !s.edit ? 'hidden' : ''}><span>MOVE</span><svg viewBox="0 0 90 96" aria-label="鼠标移动轨迹"><path d="M45 0v96M0 48h90" stroke="currentColor" opacity=".18" fill="none"/><path id="mouse-trail" stroke="currentColor" stroke-width="1.5" fill="none"/><circle id="mouse-trail-dot" r="2.5" cx="45" cy="48" fill="currentColor"/></svg><small>短时相对轨迹</small></div>`;
      const anchor = $('input-anchor'); anchor.dataset.style = s.style; anchor.dataset.mode = s.displayMode; anchor.classList.toggle('editing', s.edit); anchor.style.setProperty('--input-color', s.color); anchor.style.setProperty('--key-opacity', s.opacity / 100);
      this.updatePressed(); this.status();
      canvas.style.backgroundSize = `${s.snapGrid}px ${s.snapGrid}px`;
    }
    render() { this.renderStats(); this.renderInput(); this.fit(); }
    fit() {
      const s = this.getState(), w = this.stage.clientWidth, h = this.stage.clientHeight;
      for (const group of ['stats', 'input']) {
        const width = group === 'stats' ? 330 : 410, height = group === 'stats' ? 220 : 160;
        const combined = this.getTab() === 'all';
        const availableWidth = combined ? w * .48 - 16 : w - 32;
        const factor = Math.max(.1, Math.min(s[group].scale / 100, availableWidth / width, (h - 90) / height));
        this.scales[group] = factor;
        const anchor = $(group + '-anchor'); anchor.style.width = width * factor + 'px'; anchor.style.height = height * factor + 'px';
        anchor.style.transform = `translate(${s[group].x}px,${s[group].y}px)`;
        $(group + '-canvas').style.transform = `scale(${factor})`;
      }
    }
    updatePressed() {
      const s = this.getState().input;
      $('input-canvas').querySelectorAll('[data-key-code]').forEach(n => n.classList.toggle('is-down', this.pressed.has(n.dataset.keyCode)));
      const left = $('mouse-left'), right = $('mouse-right'), middle = $('mouse-middle');
      if (left) {
        left.classList.toggle('is-down', !!(this.buttons & 1)); right.classList.toggle('is-down', !!(this.buttons & 2)); middle.classList.toggle('is-down', !!(this.buttons & 4));
        const mouse = $('input-canvas').querySelector('.mouse-panel');
        mouse.classList.toggle('is-down', this.buttons !== 0 || performance.now() < (this.wheelUntil || 0));
        mouse.dataset.mode = s.mouse.displayMode;
        middle.classList.toggle('wheel-active', performance.now() < (this.wheelUntil || 0));
      }
    }
    pointerdown(e) {
      if (!(e.target instanceof Element)) return;
      const stat = e.target.closest('[data-stat]'), key = e.target.closest('[data-key-code]'), mouse = e.target.closest('[data-mouse]'), trace = e.target.closest('[data-trace]');
      const state = this.getState(), group = stat ? 'stats' : 'input';
      if ((stat || key || mouse || trace) && state[group].edit && e.button === 0) {
        const item = stat ? state.stats.items.find(i => i.id === stat.dataset.stat) : key ? state.input.keys.find(k => k.code === key.dataset.keyCode) : mouse ? state.input.mouse : state.input.trace;
        const node = stat || key || mouse || trace;
        if (stat) state.stats.selected = item.id; if (key) state.input.selected = item.code;
        this.drag = { item, group, x: e.clientX, y: e.clientY, initialX: item.x, initialY: item.y, w: stat || key ? item.w : mouse ? 60 : 104, h: stat ? 48 : key ? 42 : 145, node };
        this.stage.setPointerCapture(e.pointerId); e.preventDefault(); return;
      }
      this.stage.focus({ preventScroll: true });
      if (!this.active() || state.input.edit) return;
      this.buttons = e.buttons;
      this.previous = { x: e.clientX, y: e.clientY }; this.updatePressed();
    }
    pointermove(e) {
      if (this.drag) {
        const d = this.drag, f = this.scales[d.group];
        const s = this.getState()[d.group];
        const items = d.group === 'stats' ? s.items.map(i => ({ item: i, ...i, h: 48 })) : [...s.keys.map(i => ({ item: i, ...i, h: 42 })), { item: s.mouse, ...s.mouse, w: 60, h: 145 }, { item: s.trace, ...s.trace, w: 104, h: 145 }];
        const result = M.snapPosition(d.initialX + (e.clientX-d.x)/f, d.initialY + (e.clientY-d.y)/f, d.w, d.h, d.group === 'stats' ? 330 : 410, d.group === 'stats' ? 220 : 160, items.filter(i => i.item !== d.item), { enabled: s.snap && !e.altKey, grid: s.snapGrid, tolerance: 6/f });
        d.item.x = result.x; d.item.y = result.y;
        this.showSnapGuides(d.group, result.guides);
        d.node.style.left = d.item.x + 'px'; d.node.style.top = d.item.y + 'px'; return;
      }
      const s = this.getState().input;
      if (!this.active() || s.edit || document.activeElement !== this.stage) return;
      this.buttons = e.buttons; this.updatePressed();
      if (this.previous && s.trail) this.addPoint((e.clientX - this.previous.x) * s.sensitivity, (e.clientY - this.previous.y) * s.sensitivity, performance.now());
      this.previous = { x: e.clientX, y: e.clientY };
    }
    pointerup(e) {
      this.clearSnapGuides();
      if (this.drag) { this.drag = null; this.save(); this.refresh(); }
      this.buttons = e.buttons || 0; this.updatePressed();
    }
    addPoint(dx, dy, now) {
      if (dx === 0 && dy === 0) return;
      this.lastMove = now;
      if (!this.points.length) this.points.push({ ...this.traceCursor, t: now });
      const x = this.traceCursor.x + dx * .3, y = this.traceCursor.y + dy * .3;
      // Pan the history as the tip approaches an edge; never discard a movement or jump to the origin.
      const shiftX = Math.max(4, Math.min(86, x)) - x, shiftY = Math.max(4, Math.min(92, y)) - y;
      for (const p of this.points) { p.x += shiftX; p.y += shiftY; }
      this.traceCursor = { x: x + shiftX, y: y + shiftY };
      this.points.push({ ...this.traceCursor, t: now }); if (this.points.length > 180) this.points.shift();
    }
    tick(now) {
      if (this.demoUntil) {
        if (now >= this.demoUntil || !['input', 'all'].includes(this.getTab())) this.release();
        else {
          const t = (3600 - (this.demoUntil - now)), codes = this.getState().input.keys.map(k => k.code);
          this.pressed = new Set(codes.filter((_, i) => Math.floor(t / 240) % Math.max(1, codes.length) === i));
          const buttons = Math.floor(t / 300) % 3 === 0 ? 1 : Math.floor(t / 300) % 3 === 1 ? 2 : 0;
          this.buttons = buttons;
          this.addPoint(Math.cos(t / 210) * 2, Math.sin(t / 230) * 2, now); this.updatePressed();
        }
      }
      this.centerWhenIdle(now);
      this.points = this.points.filter(p => now - p.t <= this.getState().input.trailLife);
      const path = $('mouse-trail'), dot = $('mouse-trail-dot');
      if (path) {
        const s = this.getState().input, panel = $('input-canvas').querySelector('.trace-panel');
        panel.hidden = !s.edit && (!s.trail || s.trace.displayMode === 'hidden' || (s.trace.displayMode === 'moving' && !this.points.length));
        path.setAttribute('d', this.points.map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(''));
        dot.setAttribute('cx', this.traceCursor.x); dot.setAttribute('cy', this.traceCursor.y); dot.style.opacity = this.points.length ? '1' : '.25';
      }
      if (this.wheelUntil) { this.updatePressed(); if (now >= this.wheelUntil) this.wheelUntil = 0; }
    }
    centerWhenIdle(now) {
      const delay = Math.max(500, Number(this.getState().input.idleCenter) || 2000);
      if (this.lastMove == null || now - this.lastMove < delay) return;
      this.traceCursor = { x: 45, y: 48 }; this.points = []; this.previous = null; this.lastMove = null;
    }
    snapControls(group) {
      const s = this.getState()[group];
      return section('拖拽吸附', toggle(group+'-snap', '启用网格 / 边缘 / 中心吸附', s.snap) + select(group+'-grid', '网格间距', ['2', '5', '10', '20'].map(v => [v, v+' px']), String(s.snapGrid)) + '<p class="control-note">靠近元素或画布边缘、中心时自动对齐并显示辅助线；按住 Alt 临时自由拖动。数值输入仍可精确定位。</p>');
    }
    clearSnapGuides() {
      for (const group of ['stats', 'input']) { const canvas = $(group+'-canvas'); if (canvas) canvas.querySelectorAll('.snap-guide').forEach(n => n.remove()); }
    }
    showSnapGuides(group, guides) {
      this.clearSnapGuides();
      const canvas = $(group+'-canvas'); if (!canvas) return;
      for (const axis of ['x', 'y']) if (Number.isFinite(guides[axis])) {
        const line = document.createElement('div'); line.className = 'snap-guide snap-'+axis;
        line.style[axis === 'x' ? 'left' : 'top'] = guides[axis]+'px'; canvas.append(line);
      }
    }
  }
  window.LabOverlays = LabOverlays;
})();
