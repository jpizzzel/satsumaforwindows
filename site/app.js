// Satsuma landing page. The demo mirrors the real app: same wheel geometry, same options per file type,
// same output names. Drag a file, hold Shift, Ctrl or Alt (or tap the keys), let go on a slice.
(() => {
  "use strict";

  // ---------- What the wheels offer (kept in sync with the app's catalog) ----------

  const KIND_BY_EXT = {
    jpg: "image", jpeg: "image", png: "image", webp: "image", heic: "image", avif: "image", gif: "image",
    bmp: "image", tif: "image", tiff: "image", ico: "image",
    mp4: "video", mov: "video", mkv: "video", webm: "video", avi: "video",
    mp3: "audio", wav: "audio", m4a: "audio", flac: "audio", ogg: "audio", opus: "audio", aiff: "audio",
    pdf: "pdf", srt: "subtitle", vtt: "subtitle", ass: "subtitle", zip: "archive",
  };
  const CONVERT = {
    image: ["JPG", "PNG", "WEBP", "AVIF", "GIF", "BMP", "TIFF", "ICO", "PDF"],
    video: ["MP4", "MOV", "MKV", "WEBM", "AVI", "GIF", "MP3", "WAV", "M4A", "FLAC", "OGG", "OPUS", "AIFF"],
    audio: ["MP3", "WAV", "M4A", "FLAC", "OGG", "OPUS", "AIFF"],
    pdf: ["PNG", "JPG"],
    subtitle: ["SRT", "VTT", "ASS"],
    archive: [],
    folder: [],
  };
  const TOOLS = {
    image: ["Compress", "50%", "25%", "Rotate", "No metadata", "Zip"],
    video: ["Compress", "720p", "Mute", "Rotate", "Zip"],
    audio: ["Compress", "Normalize", "Trim silence", "Zip"],
    pdf: ["Split pages", "Rotate", "Zip"],
    subtitle: ["Zip"],
    archive: ["Extract", "Zip"],
    folder: ["Zip"],
  };
  const ACTIONS = ["Copy", "Move", "Copy path", "Float", "Share", "Terminal", "Show"];
  const VERBS = {
    "Compress": "Compressing", "50%": "Resizing to 50%", "25%": "Resizing to 25%", "Rotate": "Rotating",
    "No metadata": "Removing metadata", "Zip": "Zipping", "720p": "Resizing to 720p", "Mute": "Removing audio",
    "Normalize": "Normalizing loudness", "Trim silence": "Trimming silence", "Split pages": "Splitting into pages",
    "Extract": "Extracting",
  };
  const MODES = {
    convert: { title: "Convert", accent: "#ef6a1f" },
    tools: { title: "Tools", accent: "#1fa38f" },
    actions: { title: "Actions", accent: "#8b7cff" },
  };
  const SAME = { jpeg: "jpg", tif: "tiff", aif: "aiff" };
  const canonical = (ext) => SAME[ext] || ext;

  // ---------- Files in the fake Downloads folder ----------

  let nextId = 1;
  const files = [];
  const addFile = (name, fresh = false) => {
    const file = { id: nextId++, name, kind: kindOf(name), fresh, generated: fresh };
    files.push(file);
    // Keep the folder from overflowing: drop the oldest result once there are too many.
    const generated = files.filter((f) => f.generated);
    if (generated.length > 7) files.splice(files.indexOf(generated[0]), 1);
    return file;
  };
  function splitName(name) {
    const dot = name.lastIndexOf(".");
    return dot > 0 ? [name.slice(0, dot), name.slice(dot + 1).toLowerCase()] : [name, ""];
  }
  function kindOf(name) {
    const [, ext] = splitName(name);
    return ext ? KIND_BY_EXT[ext] || "folder" : "folder";
  }
  function unique(name) {
    const taken = new Set(files.map((f) => f.name.toLowerCase()));
    if (!taken.has(name.toLowerCase())) return name;
    const [stem, ext] = splitName(name);
    const isFolder = kindOf(name) === "folder";
    for (let n = 1; ; n++) {
      const candidate = isFolder ? `${name} (${n})` : `${stem} (${n}).${ext}`;
      if (!taken.has(candidate.toLowerCase())) return candidate;
    }
  }

  function optionsFor(file, mode) {
    if (mode === "actions") return ACTIONS;
    if (mode === "tools") return TOOLS[file.kind] || [];
    const [, ext] = splitName(file.name);
    return (CONVERT[file.kind] || []).filter((label) => canonical(label.toLowerCase()) !== canonical(ext));
  }

  function resultFor(file, mode, label) {
    const [stem, ext] = splitName(file.name);
    if (mode === "convert") {
      if (file.kind === "pdf") return `${stem} (pages)`;
      return `${stem}.${label.toLowerCase()}`;
    }
    switch (label) {
      case "Zip": return `${file.kind === "folder" ? file.name : stem}.zip`;
      case "Extract": return stem;
      case "Split pages": return `${stem} (pages)`;
      case "Mute": return `${stem} (muted).${ext}`;
      case "Normalize": return `${stem} (normalized).${ext}`;
      case "Trim silence": return `${stem} (trimmed).${ext}`;
    }
    const suffix = { "Compress": "compressed", "Rotate": "rotated", "No metadata": "no metadata", "720p": "720p", "50%": "50%", "25%": "25%" }[label];
    let outExt = ext;
    if (file.kind === "video" && label !== "Mute") outExt = "mp4";
    if (file.kind === "audio" && label === "Compress") outExt = "mp3";
    return `${stem} (${suffix}).${outExt}`;
  }

  // ---------- DOM ----------

  const $ = (id) => document.getElementById(id);
  const scene = $("scene");
  const list = $("files");
  const wheel = $("wheel");
  const ghost = $("ghost");
  const hud = $("hud");
  const SVG = "http://www.w3.org/2000/svg";

  function tileMarkup(file) {
    const [, ext] = splitName(file.name);
    const tag = file.kind === "folder" ? "DIR" : ext.toUpperCase();
    return `<span class="doc k-${file.kind}${file.kind === "folder" ? " folder" : ""}"><span class="sheet"></span><span class="fold"></span><span class="tag">${tag}</span></span>` +
      `<span class="name">${file.generated ? '<span class="new"></span>' : ""}${escapeHtml(file.name)}</span>`;
  }
  function escapeHtml(text) {
    return text.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);
  }
  function renderFiles() {
    list.innerHTML = "";
    for (const file of files) {
      const li = document.createElement("li");
      const button = document.createElement("button");
      button.type = "button";
      button.className = "tile" + (file.fresh ? " fresh" : "");
      button.dataset.id = String(file.id);
      button.setAttribute("aria-label", `${file.name}. Drag it, or press Enter to open the wheel.`);
      button.innerHTML = tileMarkup(file);
      li.appendChild(button);
      list.appendChild(li);
      file.fresh = false;
    }
    $("item-count").textContent = `${files.length} item${files.length === 1 ? "" : "s"}`;
  }
  const tileFor = (file) => list.querySelector(`.tile[data-id="${file.id}"]`);
  const fileFrom = (el) => files.find((f) => String(f.id) === el.closest(".tile")?.dataset.id);

  function scenePoint(event) {
    const rect = scene.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  }

  // ---------- The wheel ----------

  const HUB = 46, RING = 74, C = 202, SINGLE_RING = 10;
  let open = null; // { file, mode, slots, items, cx, cy, scale, hovered, byClick }

  function layout(count) {
    if (count <= 0) return [];
    if (count <= SINGLE_RING) return ring(0, 0, count);
    const innerMid = HUB + RING / 2, outerMid = HUB + RING * 1.5;
    const inner = Math.ceil(count * innerMid / (innerMid + outerMid));
    return [...ring(0, 0, inner), ...ring(1, inner, count - inner)];
  }
  function ring(r, first, count) {
    const sweep = 360 / count, inner = HUB + r * RING;
    return Array.from({ length: count }, (_, i) =>
      ({ index: first + i, ring: r, start: i * sweep - sweep / 2, sweep, inner, outer: inner + RING }));
  }
  function at(radius, angle) {
    const a = angle * Math.PI / 180;
    return [C + radius * Math.sin(a), C - radius * Math.cos(a)];
  }
  function sectorPath(s) {
    if (s.sweep >= 359.99) {
      return `M ${C} ${C - s.outer} A ${s.outer} ${s.outer} 0 1 1 ${C - 0.01} ${C - s.outer} Z M ${C} ${C - s.inner} A ${s.inner} ${s.inner} 0 1 0 ${C - 0.01} ${C - s.inner} Z`;
    }
    const end = s.start + s.sweep, large = s.sweep > 180 ? 1 : 0;
    const [x1, y1] = at(s.outer, s.start), [x2, y2] = at(s.outer, end);
    const [x3, y3] = at(s.inner, end), [x4, y4] = at(s.inner, s.start);
    return `M ${x1} ${y1} A ${s.outer} ${s.outer} 0 ${large} 1 ${x2} ${y2} L ${x3} ${y3} A ${s.inner} ${s.inner} 0 ${large} 0 ${x4} ${y4} Z`;
  }
  function el(tag, attrs, parent) {
    const node = document.createElementNS(SVG, tag);
    for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, v);
    parent.appendChild(node);
    return node;
  }

  function drawWheel() {
    const { file, mode } = open;
    const items = optionsFor(file, mode);
    const slots = layout(items.length);
    const accent = MODES[mode].accent;
    open.items = items;
    open.slots = slots;
    open.hovered = -1;
    wheel.replaceChildren();

    slots.forEach((s) => {
      const path = el("path", {
        d: sectorPath(s), class: "slice", fill: "#1f1e1c", "fill-rule": "evenodd",
        stroke: "rgba(255,255,255,0.13)", "stroke-width": 1, "data-index": s.index,
        role: "button", "aria-label": `${items[s.index]}`, tabindex: open.byClick ? 0 : -1,
      }, wheel);
      path.addEventListener("click", () => { if (open?.byClick) choose(s.index); });
      path.addEventListener("keydown", (e) => {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); choose(s.index); }
      });
      path.addEventListener("pointerenter", () => { if (open?.byClick) hover(s.index); });
      const [x, y] = at((s.inner + s.outer) / 2, s.start + s.sweep / 2);
      const label = items[s.index];
      const text = el("text", { x, y, class: "lbl", "text-anchor": "middle", "dominant-baseline": "central", "font-size": label.length > 6 ? 10.5 : 13 }, wheel);
      const words = label.length > 8 ? label.split(" ") : [label];
      words.forEach((w, i) => { el("tspan", { x, dy: i === 0 ? `${-(words.length - 1) * 0.55}em` : "1.1em" }, text).textContent = w; });
    });

    el("circle", { cx: C, cy: C, r: HUB - 3, fill: "#151413", stroke: accent, "stroke-width": 2.5 }, wheel);
    el("text", { x: C, y: C - (items.length ? 7 : 12), class: "hub-t", "text-anchor": "middle", fill: accent, "font-size": 14 }, wheel).textContent = MODES[mode].title;
    const sub = items.length ? (file.name.length > 16 ? file.name.slice(0, 15) + "\u2026" : file.name) : "Nothing to do";
    el("text", { x: C, y: C + (items.length ? 11 : 6), class: "hub-s", "text-anchor": "middle", "font-size": 9.5 }, wheel).textContent = sub;
    if (!items.length) el("text", { x: C, y: C + 20, class: "hub-s", "text-anchor": "middle", "font-size": 9.5 }, wheel).textContent = "for this file";
  }

  function hover(index) {
    if (!open || open.hovered === index) return;
    const paths = wheel.querySelectorAll(".slice");
    if (open.hovered >= 0 && paths[open.hovered]) paths[open.hovered].setAttribute("fill", "#1f1e1c");
    if (index >= 0 && paths[index]) paths[index].setAttribute("fill", MODES[open.mode].accent);
    open.hovered = index;
  }

  function openWheel(file, mode, point, byClick, byKeyboard = false) {
    open = { file, mode, byClick, byKeyboard, cx: 0, cy: 0, scale: 1 };
    drawWheel();
    wheel.removeAttribute("hidden"); // SVG elements have no .hidden property, so use the attribute.
    wheel.classList.remove("opening");
    void wheel.getBoundingClientRect();
    wheel.classList.add("opening");
    place(point);
    scene.dataset.busy = "true";
    if (byKeyboard) wheel.querySelector(".slice")?.focus({ preventScroll: true });
  }
  function place(point) {
    // The layout size, not getBoundingClientRect: that one is shrunk while the opening animation plays.
    const size = parseFloat(getComputedStyle(wheel).width);
    const w = scene.clientWidth, h = scene.clientHeight;
    // Keep the whole wheel inside the picture, the same way the app keeps it on screen.
    const cx = w <= size ? w / 2 : Math.min(Math.max(point.x, size / 2), w - size / 2);
    const cy = h <= size ? h / 2 : Math.min(Math.max(point.y, size / 2), h - size / 2);
    wheel.style.left = `${cx}px`;
    wheel.style.top = `${cy}px`;
    Object.assign(open, { cx, cy, scale: size / 404 });
  }
  function closeWheel() {
    if (!open) return;
    const back = open.byKeyboard ? tileFor(open.file) : null;
    open = null;
    wheel.setAttribute("hidden", "");
    wheel.replaceChildren();
    if (hud.dataset.state === "idle") scene.dataset.busy = "false";
    back?.focus({ preventScroll: true });
  }
  function hitTest(point) {
    if (!open || !open.slots.length) return -1;
    const x = (point.x - open.cx) / open.scale, y = (point.y - open.cy) / open.scale;
    const distance = Math.hypot(x, y);
    if (distance < HUB) return -1;
    const rings = open.slots[open.slots.length - 1].ring + 1;
    const r = Math.min(Math.floor((distance - HUB) / RING), rings - 1);
    const angle = Math.atan2(x, -y) * 180 / Math.PI;
    const slot = open.slots.find((s) => s.ring === r && (((angle - s.start) % 360) + 360) % 360 < s.sweep);
    return slot ? slot.index : -1;
  }
  function choose(index) {
    if (!open || index < 0) return closeWheel();
    const { file, mode, items } = open;
    const label = items[index];
    closeWheel();
    run(file, mode, label);
  }

  // ---------- Running a choice (the progress card, like the app's) ----------

  let hudTimer = 0, progressFrame = 0;
  function showHud(state, title, detail) {
    clearTimeout(hudTimer);
    hud.dataset.state = state;
    $("hud-title").textContent = title;
    $("hud-detail").textContent = detail;
    scene.dataset.busy = "true";
    if (state === "done") {
      hudTimer = setTimeout(() => {
        hud.dataset.state = "idle";
        if (!open) scene.dataset.busy = "false";
      }, 3200);
    }
  }
  function run(file, mode, label) {
    if (mode === "actions") return act(file, label);
    const verb = mode === "convert" ? `Converting to ${label}` : VERBS[label] || label;
    const output = unique(resultFor(file, mode, label));
    showHud("working", verb, file.name);
    const bar = $("hud-progress");
    const duration = file.kind === "video" ? 1500 : 800;
    const started = performance.now();
    cancelAnimationFrame(progressFrame);
    const step = (now) => {
      const t = Math.min(1, (now - started) / duration);
      bar.style.width = `${Math.round(t * 100)}%`;
      if (t < 1) { progressFrame = requestAnimationFrame(step); return; }
      const created = addFile(output, true);
      renderFiles();
      showHud("done", "Saved", created.name);
    };
    if (matchMedia("(prefers-reduced-motion: reduce)").matches) step(started + duration);
    else progressFrame = requestAnimationFrame(step);
  }
  function act(file, label) {
    const where = "C:\\Users\\you\\Downloads";
    switch (label) {
      case "Copy": return showHud("done", "Copied", `${file.name}, ready to paste anywhere`);
      case "Move": return showHud("done", "Ready to move", "Paste it in another folder");
      case "Copy path": return showHud("done", "Path copied", `"${where}\\${file.name}"`);
      case "Share": return showHud("done", "Share sheet opened", "Nearby Sharing, Mail, Teams...");
      case "Terminal": return showHud("done", "Terminal opened", `PS ${where}>`);
      case "Show": {
        const tile = tileFor(file);
        tile?.classList.add("flash");
        setTimeout(() => tile?.classList.remove("flash"), 1200);
        return showHud("done", "Shown in File Explorer", file.name);
      }
      case "Float": {
        const shelf = $("float-shelf");
        const items = $("shelf-items");
        if (![...items.children].some((li) => li.textContent === file.name)) {
          const li = document.createElement("li");
          li.textContent = file.name;
          items.appendChild(li);
        }
        shelf.hidden = false;
        return showHud("done", "Floating", `${file.name} stays on top until you need it`);
      }
    }
  }
  $("shelf-close").addEventListener("click", () => {
    $("float-shelf").hidden = true;
    $("shelf-items").replaceChildren();
  });

  // ---------- Keys: real modifiers, or the on-screen ones for touch ----------

  let latched = null;
  const held = new Set();
  const keyButtons = [...document.querySelectorAll(".kcap")];
  function syncKeyButtons() {
    const active = heldMode() || latched;
    keyButtons.forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.mode === active)));
  }
  function heldMode() {
    if (held.has("Alt")) return "actions";
    if (held.has("Control") || held.has("Meta")) return "tools";
    if (held.has("Shift")) return "convert";
    return null;
  }
  // Same priority as the app: the more specific wheel wins.
  const currentMode = () => heldMode() || latched;

  keyButtons.forEach((b) => b.addEventListener("click", () => {
    latched = latched === b.dataset.mode ? null : b.dataset.mode;
    syncKeyButtons();
    if (open?.byClick && latched) { open.mode = latched; drawWheel(); }
  }));

  window.addEventListener("keydown", (e) => {
    if (["Shift", "Control", "Alt", "Meta"].includes(e.key)) {
      if (e.key === "Alt") e.preventDefault(); // Otherwise some browsers jump to their menu bar.
      held.add(e.key);
      syncKeyButtons();
      onModeChanged();
    } else if (e.key === "Escape" && open) {
      closeWheel();
    }
  });
  window.addEventListener("keyup", (e) => {
    if (e.key === "Alt") e.preventDefault();
    held.delete(e.key);
    syncKeyButtons();
    onModeChanged();
  });
  window.addEventListener("blur", () => { held.clear(); syncKeyButtons(); });

  // ---------- Dragging ----------

  let drag = null; // { file, tile, pointerId, start, point, moved }
  let swallowClick = false;

  function onModeChanged() {
    if (!drag?.moved) {
      if (open?.byClick) {
        const mode = currentMode();
        if (mode && mode !== open.mode) { open.mode = mode; drawWheel(); }
      }
      return;
    }
    const mode = currentMode();
    if (mode && !open) openWheel(drag.file, mode, drag.point, false);
    else if (!mode && open) closeWheel();
    else if (mode && open && open.mode !== mode) { open.mode = mode; drawWheel(); }
    if (open) hover(hitTest(drag.point));
  }

  function syncHeldFrom(e) {
    // Pointer events carry the modifier state too, which catches keys pressed before the page had focus.
    for (const [key, down] of [["Shift", e.shiftKey], ["Control", e.ctrlKey], ["Alt", e.altKey], ["Meta", e.metaKey]]) {
      if (down) held.add(key); else held.delete(key);
    }
    syncKeyButtons();
  }

  list.addEventListener("pointerdown", (e) => {
    const tile = e.target.closest(".tile");
    if (!tile || e.button !== 0) return;
    e.preventDefault();
    if (open) closeWheel();
    tile.setPointerCapture(e.pointerId);
    const point = scenePoint(e);
    drag = { file: fileFrom(tile), tile, pointerId: e.pointerId, start: point, point, moved: false };
  });

  list.addEventListener("pointermove", (e) => {
    if (!drag || e.pointerId !== drag.pointerId) return;
    drag.point = scenePoint(e);
    if (!drag.moved) {
      if (Math.hypot(drag.point.x - drag.start.x, drag.point.y - drag.start.y) < 6) return;
      drag.moved = true;
      drag.tile.classList.add("dragging");
      ghost.innerHTML = `<span class="tile">${tileMarkup(drag.file)}</span>`;
      ghost.hidden = false;
    }
    ghost.style.left = `${drag.point.x}px`;
    ghost.style.top = `${drag.point.y}px`;
    syncHeldFrom(e);
    onModeChanged();
  });

  function endDrag(e, cancelled) {
    if (!drag || (e && e.pointerId !== drag.pointerId)) return;
    const { moved, tile } = drag;
    tile.classList.remove("dragging");
    ghost.hidden = true;
    ghost.replaceChildren();
    if (moved) {
      swallowClick = true;
      if (open && !cancelled) choose(hitTest(drag.point));
      else closeWheel();
    }
    drag = null;
  }
  list.addEventListener("pointerup", (e) => endDrag(e, false));
  list.addEventListener("pointercancel", (e) => endDrag(e, true));
  list.addEventListener("lostpointercapture", (e) => endDrag(e, true));

  // Remember when Enter or Space opened a file, so only keyboard users get focus moved into the wheel.
  let openedByKey = false;
  list.addEventListener("keydown", (e) => { if (e.key === "Enter" || e.key === " ") openedByKey = true; });

  // A plain click (or Enter) opens the wheel on the file, for anyone not dragging.
  list.addEventListener("click", (e) => {
    const tile = e.target.closest(".tile");
    if (!tile) return;
    if (swallowClick) { swallowClick = false; return; }
    const rect = tile.getBoundingClientRect(), sceneRect = scene.getBoundingClientRect();
    const point = { x: rect.left + rect.width / 2 - sceneRect.left, y: rect.top + rect.height / 2 - sceneRect.top };
    openWheel(fileFrom(tile), currentMode() || "convert", point, true, openedByKey);
    openedByKey = false;
  });
  wheel.addEventListener("pointermove", (e) => { if (open?.byClick) hover(hitTest(scenePoint(e))); });
  scene.addEventListener("pointerdown", (e) => {
    if (open?.byClick && !wheel.contains(e.target) && !e.target.closest(".kcap")) closeWheel();
  });

  // ---------- Page chrome: coordinates, clock, download info ----------

  const coordX = $("coord-x"), coordY = $("coord-y");
  let coordFrame = 0;
  document.addEventListener("pointermove", (e) => {
    cancelAnimationFrame(coordFrame);
    coordFrame = requestAnimationFrame(() => {
      // Drafting units: millimeters at 96 dpi.
      coordX.textContent = (e.pageX * 0.26458).toFixed(2);
      coordY.textContent = (e.pageY * 0.26458).toFixed(2);
    });
  }, { passive: true });

  const clock = $("clock");
  const tick = () => { clock.textContent = new Date().toLocaleTimeString([], { hour: "numeric", minute: "2-digit" }); };
  tick();
  setInterval(tick, 30000);

  const showSoon = () => {
    $("download-soon").hidden = false;
    document.querySelectorAll("[data-download]").forEach((a) => a.setAttribute("href", "#download"));
  };
  if (new URLSearchParams(location.search).get("release") === "soon") showSoon();

  fetch("/api/latest", { headers: { Accept: "application/json" } })
    .then((r) => (r.ok || r.status === 404 ? r.json() : null))
    .then((info) => {
      if (!info) return;
      if (!info.available) return showSoon();
      const mb = Math.round(info.size / 1048576);
      $("fact-version").textContent = info.version;
      $("fact-size").textContent = `${mb} MB zip`;
      $("download-label").textContent = `Download ${info.version}`;
      $("corner-version").textContent = `${info.version} for windows 10 & 11`;
    })
    .catch(() => { /* Offline or opened as a file: keep the defaults. */ });

  // ---------- Start ----------

  ["beach.heic", "trip.mov", "lease.pdf"].forEach((name) => addFile(name));
  renderFiles();
})();
