/* Use the dashboard's preference on every tool, including pages opened from disk. */
(() => {
  const root = document.documentElement;
  const watch = window.matchMedia("(prefers-color-scheme: dark)");
  const saved = () => { try { return localStorage.getItem("under-glass-mode"); } catch { return null; } };
  function follow() {
    const choice = saved(), host = root.getAttribute("data-theme");
    apply(["dark", "light"].includes(choice) ? choice : ["dark", "light"].includes(host) ? host : watch.matches ? "dark" : "light");
  }
  function apply(mode) {
    root.setAttribute("data-mode", mode);
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => button.setAttribute("aria-pressed", String(mode === "dark")));
  }
  follow();
  document.addEventListener("DOMContentLoaded", () => {
    follow();
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => button.addEventListener("click", () => {
      const mode = root.getAttribute("data-mode") === "dark" ? "light" : "dark";
      try { localStorage.setItem("under-glass-mode", mode); } catch { /* Keep this visit usable. */ }
      apply(mode);
    }));
  });
  watch.addEventListener?.("change", () => { if (!saved()) follow(); });
  window.addEventListener("storage", (event) => { if (event.key === "under-glass-mode" || event.key === null) follow(); });
  new MutationObserver(() => { if (!saved()) follow(); }).observe(root, { attributes:true, attributeFilter:["data-theme"] });
})();
