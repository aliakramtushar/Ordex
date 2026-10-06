/* ==========================================================================
   Ordex – shared behaviour. Everything is opt-in through data-* attributes,
   so any page can reuse it without writing new JavaScript.
   ========================================================================== */
(function () {
  "use strict";

  const $ = (sel, root = document) => (sel ? root.querySelector(sel) : null);
  const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

  /** Antiforgery token for fetch() calls. */
  function csrfToken() {
    const input = $('input[name="__RequestVerificationToken"]');
    return input ? input.value : "";
  }

  /** GET JSON from our own endpoints. */
  async function getJson(url) {
    const res = await fetch(url, {
      headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest", "RequestVerificationToken": csrfToken() },
      credentials: "same-origin"
    });
    if (!res.ok) throw new Error("Request failed: " + res.status);
    return res.json();
  }

  // ───────────────────────── Toasts ─────────────────────────
  function toast(message, type) {
    let stack = $(".toast-stack");
    if (!stack) {
      stack = document.createElement("div");
      stack.className = "toast-stack";
      stack.setAttribute("aria-live", "polite");
      document.body.appendChild(stack);
    }
    const el = document.createElement("div");
    el.className = "toast-x" + (type === "error" ? " error" : "");
    el.setAttribute("role", type === "error" ? "alert" : "status");
    const icon = document.createElement("i");
    icon.className = "bi " + (type === "error" ? "bi-exclamation-circle" : "bi-check-circle");
    const text = document.createElement("div");
    text.textContent = message;
    const close = document.createElement("button");
    close.className = "close";
    close.type = "button";
    close.setAttribute("aria-label", "Close");
    close.innerHTML = '<i class="bi bi-x-lg"></i>';
    close.addEventListener("click", () => el.remove());
    el.append(icon, text, close);
    stack.appendChild(el);
    setTimeout(() => el.remove(), type === "error" ? 7000 : 4000);
  }
  window.ordexToast = toast;

  function initServerToasts() {
    $$("[data-toast]").forEach(el => toast(el.dataset.toast, el.dataset.toastType));
  }

  // ───────────────────────── Theme toggle ─────────────────────────
  function initThemeToggle() {
    const root = document.documentElement;
    $$("[data-theme-toggle]").forEach(btn => {
      btn.addEventListener("click", () => {
        const next = root.getAttribute("data-theme") === "dark" ? "light" : "dark";
        root.setAttribute("data-theme", next);
        root.setAttribute("data-bs-theme", next);
        document.dispatchEvent(new CustomEvent("ordex:theme", { detail: next }));

        // Signed in: save it on the user (works on every device). Otherwise remember it here.
        const url = root.getAttribute("data-mode-url");
        if (url && root.hasAttribute("data-mode")) {
          root.setAttribute("data-mode", next);
          const body = new URLSearchParams({ colorMode: next });
          fetch(url, {
            method: "POST",
            body,
            credentials: "same-origin",
            headers: { "RequestVerificationToken": csrfToken(), "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" }
          }).catch(() => { /* offline: the page still switched */ });
        } else {
          try { localStorage.setItem("ordex-theme", next); } catch (e) { /* ignore */ }
        }
      });
    });
  }

  // ───────────────────────── Forms ─────────────────────────
  /** Stops double taps on slow mobile networks from saving twice. */
  function initSubmitOnce() {
    document.addEventListener("submit", e => {
      const form = e.target;
      if (!(form instanceof HTMLFormElement) || e.defaultPrevented) return;
      if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
      if (form.dataset.submitting === "1") { e.preventDefault(); return; }
      form.dataset.submitting = "1";
      $$('button[type="submit"]', form).forEach(b => {
        b.dataset.originalHtml = b.innerHTML;
        setTimeout(() => {
          b.disabled = true;
          b.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> ' + (b.dataset.busyText || "Saving…");
        }, 0);
      });
    });
    // Back/forward cache: re-enable buttons when the page is shown again.
    window.addEventListener("pageshow", () => {
      $$("form[data-submitting]").forEach(f => delete f.dataset.submitting);
      $$("button[data-original-html]").forEach(b => { b.disabled = false; b.innerHTML = b.dataset.originalHtml; });
    });
  }

  /** <select data-autosubmit> / <input data-autosubmit> submit their form when changed. */
  function initAutoSubmit() {
    $$("[data-autosubmit]").forEach(el => el.addEventListener("change", () => el.form && el.form.requestSubmit()));
  }

  /**
   * Top-bar company / business unit switcher: applies as soon as a value changes.
   * Changing the company resets the unit to "all" (units belong to one company).
   */
  function initScopeSwitch() {
    $$("form[data-scope-switch]").forEach(form => {
      const company = $("[data-scope-company]", form);
      const unit = $("[data-scope-unit]", form);
      if (company) company.addEventListener("change", () => {
        if (unit) unit.value = "0";
        form.classList.add("is-loading");
        form.requestSubmit();
      });
      if (unit) unit.addEventListener("change", () => { form.classList.add("is-loading"); form.requestSubmit(); });
    });
  }

  /** Buttons/forms with data-confirm="..." ask before continuing. */
  function initConfirm() {
    const modalEl = $("#confirmModal");
    if (!modalEl || !window.bootstrap) return;
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    let pendingForm = null;

    document.addEventListener("submit", e => {
      const form = e.target;
      if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === "1") return;
      e.preventDefault();
      e.stopImmediatePropagation();
      pendingForm = form;
      $("#confirmMessage").textContent = form.dataset.confirm;
      const okBtn = $("#confirmOk");
      okBtn.className = "btn " + (form.dataset.confirmStyle || "btn-primary");
      okBtn.textContent = form.dataset.confirmText || "Yes, continue";
      modal.show();
    }, true);

    $("#confirmOk").addEventListener("click", () => {
      if (!pendingForm) return;
      pendingForm.dataset.confirmed = "1";
      modal.hide();
      pendingForm.requestSubmit();
    });
  }

  /**
   * Dependent dropdowns:
   * <select data-source="/lookup/batches" data-depends-on="#BusinessUnitId" data-param="businessUnitId"
   *         data-extra-depends="#CompanyId" data-extra-param="companyId" data-placeholder="— None —">
   */
  function initDependentSelects() {
    $$("select[data-source][data-depends-on]").forEach(select => {
      const parent = $(select.dataset.dependsOn);
      if (!parent) return;

      parent.addEventListener("change", async () => {
        const params = new URLSearchParams();
        params.set(select.dataset.param, parent.value || "");
        if (select.dataset.extraDepends) {
          const extra = $(select.dataset.extraDepends);
          if (extra) params.set(select.dataset.extraParam, extra.value || "");
        }

        select.disabled = true;
        try {
          const items = parent.value ? await getJson(select.dataset.source + "?" + params) : [];
          const keepFirst = select.dataset.placeholder !== undefined;
          select.innerHTML = "";
          if (keepFirst) select.add(new Option(select.dataset.placeholder, ""));
          items.forEach(i => {
            const opt = new Option(i.text, i.value);
            if (i.rate !== undefined) opt.dataset.rate = i.rate;
            select.add(opt);
          });
          select.dispatchEvent(new Event("change", { bubbles: true }));
        } catch (err) {
          toast("Could not load options. Please check your connection.", "error");
        } finally {
          select.disabled = false;
        }
      });
    });
  }

  /**
   * Image picker with preview + on-device compression (saves mobile data):
   * <input type="file" data-image-input data-preview="#imgPreview">
   */
  function initImagePickers() {
    // Delegated, so pickers added later (e.g. extra product cards) work too.
    document.addEventListener("change", async e => {
      const input = e.target;
      if (!(input instanceof HTMLInputElement) || input.type !== "file" || !input.hasAttribute("data-image-input")) return;

      const file = input.files && input.files[0];
      if (!file) return;
      if (!/^image\/(jpeg|png|webp|heic|heif)$/i.test(file.type) && !/\.(jpe?g|png|webp)$/i.test(file.name)) {
        toast("Please choose a JPG, PNG or WEBP image.", "error");
        input.value = "";
        return;
      }

      const compressed = await compressImage(file, 1280, 0.82).catch(() => null);
      if (compressed && compressed.size < file.size && window.DataTransfer) {
        const dt = new DataTransfer();
        dt.items.add(new File([compressed], file.name.replace(/\.\w+$/, "") + ".jpg", { type: "image/jpeg" }));
        input.files = dt.files;
      }

      // Preview: data-preview="#id", else the .preview box of the surrounding picker.
      const holder = input.closest(".image-picker, .item-photo");
      const preview = input.dataset.preview ? $(input.dataset.preview) : (holder && holder.querySelector(".preview"));
      if (preview) {
        const url = URL.createObjectURL(input.files[0]);
        preview.innerHTML = "";
        const img = document.createElement("img");
        img.src = url;
        img.alt = "Selected image";
        preview.appendChild(img);
        if (holder) holder.classList.add("has-image");
      }
      const remove = input.form && input.form.querySelector('input[name="RemoveImage"]');
      if (remove) remove.checked = false;
    });
  }

  function compressImage(file, maxSide, quality) {
    return new Promise((resolve, reject) => {
      const img = new Image();
      const url = URL.createObjectURL(file);
      img.onload = () => {
        const scale = Math.min(1, maxSide / Math.max(img.width, img.height));
        const canvas = document.createElement("canvas");
        canvas.width = Math.round(img.width * scale);
        canvas.height = Math.round(img.height * scale);
        const ctx = canvas.getContext("2d");
        ctx.fillStyle = "#fff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        URL.revokeObjectURL(url);
        canvas.toBlob(b => (b ? resolve(b) : reject()), "image/jpeg", quality);
      };
      img.onerror = () => { URL.revokeObjectURL(url); reject(); };
      img.src = url;
    });
  }

  /**
   * Fills modal forms from the button that opened them:
   * <button data-bs-toggle="modal" data-bs-target="#deliverModal" data-fill='{"OrderId":5,"CollectedAmount":1200}' data-label="ORD-2610-0005">
   */
  function initModalFill() {
    document.addEventListener("show.bs.modal", e => {
      const trigger = e.relatedTarget;
      const modal = e.target;
      if (!trigger || !trigger.dataset.fill) return;
      let data = {};
      try { data = JSON.parse(trigger.dataset.fill); } catch (err) { return; }
      Object.keys(data).forEach(name => {
        const field = modal.querySelector('[name="' + name + '"]');
        if (field) field.value = data[name];
      });
      $$("[data-fill-text]", modal).forEach(el => {
        const key = el.dataset.fillText;
        if (data[key] !== undefined) el.textContent = data[key];
      });
      const firstInput = modal.querySelector("textarea, input:not([type=hidden])");
      if (firstInput) setTimeout(() => firstInput.focus(), 300);
    });
  }

  /** Password show/hide buttons: <button data-toggle-password="#Password"> */
  function initPasswordToggle() {
    $$("[data-toggle-password]").forEach(btn => {
      btn.addEventListener("click", () => {
        const input = $(btn.dataset.togglePassword);
        if (!input) return;
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        btn.innerHTML = show ? '<i class="bi bi-eye-slash"></i>' : '<i class="bi bi-eye"></i>';
      });
    });
  }

  /** Landing page nav border on scroll. */
  function initScrollNav() {
    const nav = $(".pub-nav");
    if (!nav) return;
    const update = () => nav.classList.toggle("scrolled", window.scrollY > 8);
    window.addEventListener("scroll", update, { passive: true });
    update();
  }

  /** Quick date range chips: <a data-range="month|last-month|today|year" data-from="#from" data-to="#to"> */
  function initRangeChips() {
    $$("[data-range]").forEach(chip => {
      chip.addEventListener("click", e => {
        e.preventDefault();
        const now = new Date();
        let from, to;
        switch (chip.dataset.range) {
          case "today": from = to = now; break;
          case "last-month": from = new Date(now.getFullYear(), now.getMonth() - 1, 1); to = new Date(now.getFullYear(), now.getMonth(), 0); break;
          case "year": from = new Date(now.getFullYear(), 0, 1); to = now; break;
          default: from = new Date(now.getFullYear(), now.getMonth(), 1); to = now;
        }
        const fmt = d => d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0");
        const fromEl = $(chip.dataset.from), toEl = $(chip.dataset.to);
        if (fromEl) fromEl.value = fmt(from);
        if (toEl) toEl.value = fmt(to);
        (fromEl || toEl).form.requestSubmit();
      });
    });
  }

  /** <select data-toggle-target="#box" data-hide-value="X"> hides #box while X is selected. */
  function initToggleByValue() {
    $$("select[data-toggle-target]").forEach(select => {
      const target = $(select.dataset.toggleTarget);
      if (!target) return;
      const update = () => { target.hidden = select.value === select.dataset.hideValue; };
      select.addEventListener("change", update);
      update();
    });
  }

  /** Elements with data-print print the page. */
  function initPrint() {
    $$("[data-print]").forEach(b => b.addEventListener("click", () => window.print()));
  }

  /** Tapping a number box selects its value, so "0" is replaced instead of becoming "05". */
  function initNumberSelect() {
    document.addEventListener("focusin", e => {
      const el = e.target;
      if (el instanceof HTMLInputElement && el.type === "number") setTimeout(() => el.select(), 0);
    });
  }

  /**
   * Copy / share buttons:
   * <button data-copy="text">            copies the text
   * <button data-share-text="…" hidden>  native share sheet (shown only where the phone supports it)
   * <input data-select-on-focus>         selects all on tap
   */
  function initCopyShare() {
    $$("[data-copy]").forEach(btn => btn.addEventListener("click", async () => {
      const text = btn.dataset.copy;
      try {
        await navigator.clipboard.writeText(text);
      } catch (e) {
        const tmp = document.createElement("textarea");
        tmp.value = text;
        tmp.setAttribute("readonly", "");
        tmp.style.position = "fixed"; tmp.style.opacity = "0";
        document.body.appendChild(tmp);
        tmp.select();
        try { document.execCommand("copy"); } catch (err) { /* ignore */ }
        tmp.remove();
      }
      toast("Link copied.");
    }));

    $$("[data-share-text]").forEach(btn => {
      if (!navigator.share) return;
      btn.hidden = false;
      btn.addEventListener("click", () => {
        navigator.share({ title: btn.dataset.shareTitle || document.title, text: btn.dataset.shareText }).catch(() => { /* cancelled */ });
      });
    });

    $$("input[data-select-on-focus]").forEach(el => el.addEventListener("focus", () => setTimeout(() => el.select(), 0)));
  }

  /** Rows-per-page selector under lists: reload page 1 with the new size, keeping every filter. */
  function initPageSize() {
    $$("select[data-page-size]").forEach(sel => sel.addEventListener("change", () => {
      const url = new URL(window.location.href);
      url.searchParams.set("pageSize", sel.value);
      url.searchParams.set("page", "1");
      window.location.href = url.toString();
    }));
  }

  window.ordex = { getJson, toast, csrfToken };

  document.addEventListener("DOMContentLoaded", () => {
    initServerToasts();
    initThemeToggle();
    initConfirm();
    initSubmitOnce();
    initAutoSubmit();
    initScopeSwitch();
    initDependentSelects();
    initImagePickers();
    initModalFill();
    initPasswordToggle();
    initScrollNav();
    initRangeChips();
    initPrint();
    initToggleByValue();
    initNumberSelect();
    initCopyShare();
    initPageSize();
  });
})();
