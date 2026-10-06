/* Theme & settings page: preview a theme or mode instantly (every palette is already in /theme.css);
   "Save my look" stores it on the user. Leaving without saving keeps the saved look. */
(function () {
  "use strict";
  const form = document.querySelector("[data-appearance-form]");
  if (!form) return;

  const root = document.documentElement;
  const media = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;

  function applyMode(mode) {
    const dark = mode === "dark" || (mode === "system" && media && media.matches);
    root.setAttribute("data-mode", mode);
    root.setAttribute("data-theme", dark ? "dark" : "light");
    root.setAttribute("data-bs-theme", dark ? "dark" : "light");
    document.dispatchEvent(new CustomEvent("ordex:theme", { detail: dark ? "dark" : "light" }));
  }

  form.addEventListener("change", e => {
    const el = e.target;
    if (el.name === "theme") {
      root.setAttribute("data-palette", el.value);
      document.dispatchEvent(new CustomEvent("ordex:theme", { detail: root.getAttribute("data-theme") }));
    }
    if (el.name === "colorMode") applyMode(el.value);
  });
})();
