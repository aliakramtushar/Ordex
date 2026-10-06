// Runs in <head> before the page paints, so there is no light/dark flash.
// Signed-in pages: the server puts the user's choice on <html data-mode="light|dark|system">.
// "system" follows the phone/computer setting and updates live when it changes.
// Public pages (no data-mode): the last choice made on this device, else light.
(function () {
  var root = document.documentElement;
  var mode = root.getAttribute("data-mode");
  var media = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;

  function apply(theme) {
    root.setAttribute("data-theme", theme);
    root.setAttribute("data-bs-theme", theme);
  }

  if (mode === "system") {
    apply(media && media.matches ? "dark" : "light");
    if (media && media.addEventListener) {
      media.addEventListener("change", function (e) {
        if (root.getAttribute("data-mode") !== "system") return;
        apply(e.matches ? "dark" : "light");
        document.dispatchEvent(new CustomEvent("ordex:theme", { detail: e.matches ? "dark" : "light" }));
      });
    }
  } else if (mode === "dark" || mode === "light") {
    apply(mode);
  } else {
    var stored = null;
    try { stored = localStorage.getItem("ordex-theme"); } catch (e) { /* private mode */ }
    apply(stored === "dark" ? "dark" : "light");
  }
})();
