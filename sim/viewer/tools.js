/* Shared navigation for the loopback tools. Recorded pages remain usable without this file. */
(() => {
  "use strict";
  const ROOT = "/sim/viewer/";
  const PAGES = { tools: "tools.html", dashboard: "index.html", speech: "speech.html", recordings: "morning.html" };

  function recordingUrl(value, base) {
    const url = new URL(value, base), origin = new URL(base);
    if (!/^https?:$/.test(url.protocol) || url.origin !== origin.origin || url.username || url.password)
      throw new Error("Choose a recording from this preview server.");
    url.hash = "";
    return url.href;
  }
  function toolUrl(page, source, state, base) {
    if (!Object.hasOwn(PAGES, page)) throw new Error("Unknown Under Glass tool.");
    const url = new URL(ROOT + PAGES[page], base);
    if (source) url.searchParams.set("run", recordingUrl(source, base));
    if (state) url.hash = new URLSearchParams(state).toString();
    return url.href;
  }
  function readState(hash) {
    const params = new URLSearchParams(hash.replace(/^#/, "")), state = {};
    const tick = Number(params.get("t"));
    if (params.has("t") && Number.isFinite(tick) && tick >= 0) state.t = Math.floor(tick);
    if (params.has("person")) state.person = params.get("person");
    if (["town", "inner", "people", "rel", "stories", "explain"].includes(params.get("tab"))) state.tab = params.get("tab");
    return state;
  }
  function parseRecording(text) {
    let run;
    if (text.trimStart().startsWith("<")) {
      // Parse only the inert JSON node. Never execute a saved page's scripts or insert its markup.
      const node = new DOMParser().parseFromString(text, "text/html").querySelector('script#run-data[type="application/json"]');
      if (!node?.textContent.trim()) throw new Error("That page does not contain a saved town recording.");
      run = JSON.parse(node.textContent);
    } else run = JSON.parse(text);
    if (run?.format !== "under-glass-run" || run.version !== 1 || !Array.isArray(run.names))
      throw new Error("Choose a version 1 Under Glass recording (JSON or saved dashboard HTML).");
    return run;
  }
  async function readRecording(source, base) {
    const url = recordingUrl(source, base);
    // Redirects could leave the local server; refuse them before another request is made.
    const response = await fetch(url, { cache: "no-store", redirect: "error" });
    if (!response.ok) throw new Error(`Could not open the recording (${response.status}).`);
    return parseRecording(await response.text());
  }
  // Exposed for the pages and dependency-free contract tests.
  window.UnderGlassTools = { recordingUrl, toolUrl, readState, parseRecording, readRecording };

  async function mount() {
    const page = window.underGlassToolPage;
    if (!page || !/^https?:$/.test(location.protocol)) return;
    const style = document.createElement("style");
    style.textContent = `
      #ug-tools {font:var(--step--1,13px)/1.5 var(--font-body,system-ui),sans-serif; color:var(--ink,#211b16); background:var(--bg,#e8e3d9); border-bottom:1px solid var(--line,#bab2a4); padding:10px 20px; display:flex; flex-wrap:wrap; align-items:center; gap:8px 20px}
      #ug-tools .ug-links {display:flex; flex-wrap:wrap; align-items:center; gap:4px; padding:4px; background:var(--surface-2,#d2cbbe); border-radius:11px}
      #ug-tools a {color:inherit; padding:5px 10px; border:1px solid transparent; border-radius:8px; text-decoration:none; font-weight:600}
      #ug-tools a:hover {text-decoration:underline; text-underline-offset:3px}
      #ug-tools a[aria-current=page] {background:var(--key,var(--surface-hi,#f2eee6)); border-color:var(--line,#bab2a4); box-shadow:var(--key-shadow); color:var(--ink,#211b16)}
      #ug-tools a:focus-visible {outline:2px solid var(--accent,#bd4206); outline-offset:2px}
      #ug-tools .ug-context {color:var(--muted,#655e53); font:var(--step--2,11px)/1.5 var(--font-data,monospace); overflow-wrap:anywhere; flex:1 1 220px}
      #ug-tools .ug-error {color:var(--accent,#bd4206)}
      #ug-tools .theme {margin-inline-start:auto; flex:none; font-size:var(--step--1,13px)}
    `;
    document.head.append(style);
    const nav = document.createElement("nav"); nav.id = "ug-tools"; nav.setAttribute("aria-label", "Under Glass tools");
    const links = document.createElement("div"); links.className = "ug-links";
    for (const [key, label] of Object.entries({ tools: "Tools", dashboard: "Dashboard", speech: "Speech editor", recordings: "Recordings" })) {
      const link = document.createElement("a"); link.dataset.toolLink = key; link.textContent = label;
      if (key === page.page) link.setAttribute("aria-current", "page");
      links.append(link);
    }
    const context = document.createElement("span"); context.className = "ug-context";
    const error = document.createElement("span"); error.className = "ug-error"; error.setAttribute("role", "status");
    nav.append(links, context, error); document.body.prepend(nav);
    // Move the existing control so its listeners, pressed state and disk-only fallback survive.
    const theme = document.querySelector("#themeBtn, [data-theme-toggle]");
    if (theme) nav.append(theme);
    let source = null;
    const state = readState(location.hash);
    function refresh() {
      const supplied = page.source?.();
      const active = supplied === undefined ? source : supplied;
      const view = page.hasRecording?.() && page.state ? page.state() : state;
      for (const link of document.querySelectorAll("[data-tool-link]"))
        link.href = toolUrl(link.dataset.toolLink, active, view, location.href);
      context.textContent = active ? `Recording: ${new URL(active).pathname.split("/").pop()}`
        : page.hasRecording?.() ? "Local file: open it separately in each tool." : "Choose a recording or open a local file.";
    }
    window.addEventListener("under-glass-recording", refresh);
    for (const link of document.querySelectorAll("a[data-recording]"))
      link.href = toolUrl("dashboard", link.href, null, location.href);
    // Resolve the latest paused/playing time when a link is used, including keyboard and new-tab navigation.
    for (const event of ["pointerdown", "focusin", "click"]) document.addEventListener(event, (e) => {
      if (e.target.closest?.("[data-tool-link]")) refresh();
    }, true);
    try {
      const requested = new URLSearchParams(location.search).get("run");
      if (requested) source = recordingUrl(requested, location.href);
      refresh();
      if (source && page.load) {
        error.textContent = "Opening recording…";
        await page.ready;
        const run = await readRecording(source, location.href);
        await page.load(run, source, state);
        error.textContent = "";
      }
    } catch (err) { error.textContent = err.message; }
    refresh();
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", mount, { once: true });
  else mount();
})();
