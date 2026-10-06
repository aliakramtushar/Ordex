// Runs in <head> before the page paints, so there is no light/dark flash.
// Light is the default; "dark" only when the user picked it with the toggle.
(function () {
  var stored = null;
  try { stored = localStorage.getItem("ordex-theme"); } catch (e) { /* private mode */ }
  var theme = stored === "dark" ? "dark" : "light";
  document.documentElement.setAttribute("data-theme", theme);
  document.documentElement.setAttribute("data-bs-theme", theme);
})();
