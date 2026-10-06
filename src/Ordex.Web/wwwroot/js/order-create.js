/* ==========================================================================
   New order screen
   1. Customer  – search by mobile or name → pick a returning customer, or add a new one.
   2. Order     – date + purchase batch (a new batch can be created in a sheet).
   3. Products  – one card per product; add / copy / remove cards. Each card:
                  cost = purchase price × batch rate (until the cost is typed by hand),
                  live due & profit, and a running total in the sticky bar.
   ========================================================================== */
(function () {
  "use strict";

  const form = document.querySelector("[data-order-create]");
  if (!form || !window.ordex) return;

  const $ = (sel, root = form) => root.querySelector(sel);
  const $$ = (sel, root = form) => Array.from(root.querySelectorAll(sel));
  const field = name => form.querySelector('[name="' + name + '"]');

  const maxItems = parseInt(form.dataset.maxItems || "15", 10);
  let defaultRate = parseFloat(form.dataset.defaultRate || "0") || 0;
  const saleSymbol = form.dataset.saleSymbol || "";
  const saleCode = form.dataset.saleCode || "BDT";
  const buyCode = form.dataset.buyCode || "SGD";
  const saleDecimals = parseInt(form.dataset.saleDecimals || "0", 10) || 0;

  const num = el => {
    const v = parseFloat(((el && el.value) || "").replace(/,/g, ""));
    return isNaN(v) ? 0 : v;
  };
  const money = v => {
    const text = Math.abs(v).toLocaleString(saleCode === "BDT" ? "en-IN" : "en-US",
      { minimumFractionDigits: saleDecimals, maximumFractionDigits: saleDecimals });
    return (v < 0 ? "-" : "") + (saleSymbol ? saleSymbol + " " : "") + text;
  };
  const initials = name => {
    const parts = (name || "").trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return "?";
    return (parts.length === 1 ? parts[0][0] : parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  };

  const companySel = document.getElementById("CompanyId");
  const unitSel = document.getElementById("BusinessUnitId");
  const batchSel = field("BatchId");

  // ═════════════════════════ 1. Customer ═════════════════════════
  const searchBox = $("[data-cust-search]");
  const pickedBox = $("[data-cust-picked]");
  const term = $("[data-cust-term]");
  const results = $("[data-cust-results]");
  const busy = $("[data-cust-busy]");
  const newBtn = $("[data-cust-new]");
  const newLabel = $("[data-cust-new-label]");
  const idField = $("[data-customer-id]");
  const mobile = field("Mobile");
  const name = field("CustomerName");
  const address = field("DeliveryAddress");
  const social = field("SocialLink");

  const moreBox = $("[data-cust-more]");
  const moreToggle = $("[data-cust-more-toggle]");

  /** Returning customer: only the delivery address is shown; name / social link open on demand. */
  function setDetailsOpen(open, isReturning) {
    moreBox.hidden = !open;
    moreToggle.hidden = open || !isReturning;
  }

  let timer = null;
  let lastQuery = "";
  let found = [];
  let active = -1;

  function showSearch() {
    pickedBox.hidden = true;
    searchBox.hidden = false;
    idField.value = "";
    mobile.readOnly = false;
    term.value = "";
    results.innerHTML = "";
    newLabel.textContent = "New customer";
    setTimeout(() => term.focus(), 50);
  }

  function showPicked(isReturning, orders) {
    searchBox.hidden = true;
    pickedBox.hidden = false;
    const tag = $("[data-cust-tag]");
    tag.className = "cust-tag " + (isReturning ? "returning" : "new");
    tag.textContent = isReturning
      ? "Returning customer" + (orders ? " · " + orders + (orders === 1 ? " order" : " orders") : "")
      : "New customer";
    refreshCard();
  }

  function refreshCard() {
    $("[data-cust-show-name]").textContent = name.value.trim() || "New customer";
    $("[data-cust-show-mobile]").textContent = mobile.value.trim() || "Add the mobile number below";
    $("[data-cust-initials]").textContent = initials(name.value);
  }

  async function pick(c) {
    idField.value = c.id;
    mobile.value = c.mobile || "";
    mobile.readOnly = true; // the mobile number is the customer's key
    name.value = c.name || "";
    social.value = c.socialLink || "";
    address.value = c.address || "";
    showPicked(true, c.orders);
    setDetailsOpen(false, true);
    $$(".field-validation-error", pickedBox).forEach(e => { e.textContent = ""; });
    await useScopeOf(c);
    const firstProduct = $("[data-item] [data-f=name]");
    if (firstProduct && !firstProduct.value) firstProduct.focus({ preventScroll: true });
  }

  function addNew() {
    const t = term.value.trim();
    idField.value = "";
    mobile.readOnly = false;
    name.value = ""; mobile.value = ""; social.value = ""; address.value = "";
    if (/^[+\d][\d\s-]*$/.test(t)) mobile.value = t; else name.value = t;
    showPicked(false);
    setDetailsOpen(true, false);
    setTimeout(() => (mobile.value ? name : mobile).focus(), 50);
  }

  function render() {
    results.innerHTML = "";
    active = -1;

    if (lastQuery.length >= 2 && !found.length) {
      const empty = document.createElement("div");
      empty.className = "cust-empty";
      empty.textContent = "No customer found for “" + lastQuery + "”.";
      results.appendChild(empty);
    }

    found.forEach((c, i) => {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "cust-item";
      b.setAttribute("role", "option");
      b.dataset.index = i;

      const av = document.createElement("span");
      av.className = "avatar";
      av.textContent = initials(c.name);

      const body = document.createElement("span");
      body.className = "min-w-0 flex-grow-1";
      const n = document.createElement("span");
      n.className = "ci-name";
      n.textContent = c.name;
      const m = document.createElement("span");
      m.className = "ci-meta";
      m.textContent = c.mobile + (c.address ? " · " + c.address : "");
      body.append(n, m);

      const side = document.createElement("span");
      side.className = "ci-side";
      if (c.orders > 0) {
        const cnt = document.createElement("b");
        cnt.textContent = c.orders + (c.orders === 1 ? " order" : " orders");
        side.appendChild(cnt);
        if (c.lastOrder) {
          const last = document.createElement("small");
          last.textContent = "last " + c.lastOrder;
          side.appendChild(last);
        }
      } else {
        const none = document.createElement("small");
        none.textContent = "no orders yet";
        side.appendChild(none);
      }
      if (c.unit && (companySel || unitSel)) {
        const u = document.createElement("small");
        u.textContent = c.unit;
        side.appendChild(u);
      }

      b.append(av, body, side);
      b.addEventListener("click", () => pick(c));
      results.appendChild(b);
    });

    const t = term.value.trim();
    newLabel.textContent = t ? "New customer “" + t + "”" : "New customer";
  }

  async function search() {
    const q = term.value.trim();
    if (q.length < 2) { found = []; lastQuery = ""; render(); return; }

    const params = new URLSearchParams({ term: q });
    if (companySel && +companySel.value > 0) params.set("companyId", companySel.value);
    if (unitSel && +unitSel.value > 0) params.set("businessUnitId", unitSel.value);

    busy.hidden = false;
    try {
      const data = await window.ordex.getJson("/lookup/customers?" + params);
      if (term.value.trim() !== q) return; // a newer search is on its way
      found = data;
      lastQuery = q;
      render();
    } catch (e) {
      window.ordex.toast("Could not search customers. Check your connection.", "error");
    } finally {
      busy.hidden = true;
    }
  }

  term.addEventListener("input", () => {
    clearTimeout(timer);
    timer = setTimeout(search, 250);
    const t = term.value.trim();
    newLabel.textContent = t ? "New customer “" + t + "”" : "New customer";
  });

  // Keyboard: ↑/↓ to move, Enter to pick (or add new when nothing matches).
  term.addEventListener("keydown", e => {
    const items = $$(".cust-item", results);
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      if (!items.length) return;
      e.preventDefault();
      active = (active + (e.key === "ArrowDown" ? 1 : -1) + items.length) % items.length;
      items.forEach((el, i) => el.classList.toggle("active", i === active));
      items[active].scrollIntoView({ block: "nearest" });
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (active >= 0 && found[active]) pick(found[active]);
      else if (found.length === 1) pick(found[0]);
      else if (term.value.trim().length >= 2 && !found.length) addNew();
    }
  });

  newBtn.addEventListener("click", addNew);
  moreToggle.addEventListener("click", () => { setDetailsOpen(true, true); name.focus(); });
  $("[data-cust-change]").addEventListener("click", showSearch);
  [name, mobile].forEach(el => el.addEventListener("input", refreshCard));

  /** A SuperAdmin / company admin picking a customer from another unit: switch the form to that unit. */
  async function useScopeOf(c) {
    if (companySel && c.companyId && companySel.value !== String(c.companyId)) {
      const loaded = unitSel ? onceChanged(unitSel) : Promise.resolve();
      companySel.value = String(c.companyId);
      companySel.dispatchEvent(new Event("change", { bubbles: true }));
      await loaded;
    }
    if (unitSel && c.businessUnitId && unitSel.value !== String(c.businessUnitId)) {
      unitSel.value = String(c.businessUnitId);
      unitSel.dispatchEvent(new Event("change", { bubbles: true }));
    }
  }

  function onceChanged(el) {
    return new Promise(resolve => {
      const done = () => { el.removeEventListener("change", done); resolve(); };
      el.addEventListener("change", done);
      setTimeout(done, 4000);
    });
  }

  // ═════════════════════════ 2. Rate from the batch ═════════════════════════
  const rateLabel = $("[data-rate-label]");

  function currentRate() {
    const opt = batchSel && batchSel.selectedOptions && batchSel.selectedOptions[0];
    const r = opt && parseFloat(opt.dataset.rate || "");
    return r > 0 ? r : defaultRate;
  }

  // Another company picked (SuperAdmin) → use that company's default rate.
  if (companySel) companySel.addEventListener("change", async () => {
    try {
      const r = await window.ordex.getJson("/lookup/rate?companyId=" + encodeURIComponent(companySel.value || ""));
      defaultRate = parseFloat(r.rate) || 0;
      recalcAll();
    } catch (e) { /* keep the old rate */ }
  });

  // New rate → recalculate costs that were worked out automatically (costs typed by hand stay).
  if (batchSel) batchSel.addEventListener("change", recalcAll);

  // ═════════════════════════ 3. Products ═════════════════════════
  const list = $("[data-items]");
  const addBtn = $("[data-item-add]");
  const totalSell = $("[data-total-sell]");
  const totalDue = $("[data-total-due]");
  const totalProfit = $("[data-total-profit]");
  const countBadge = $("[data-item-count]");
  const saveLabel = $("[data-save-label]");

  // Remember a clean copy of the first card as the template for new ones.
  const template = list.querySelector("[data-item]").cloneNode(true);
  cleanCard(template);

  function cleanCard(card) {
    card.querySelectorAll("input:not([type=file]), textarea").forEach(el => {
      el.value = el.type === "number" ? "0" : "";
      el.classList.remove("input-validation-error", "valid");
    });
    card.querySelectorAll("input[type=file]").forEach(el => { el.value = ""; });
    card.querySelectorAll("[data-valmsg-for]").forEach(el => {
      el.textContent = "";
      el.className = "field-validation-valid";
    });
    const photo = card.querySelector(".item-photo");
    if (photo) {
      photo.classList.remove("has-image");
      photo.querySelector(".preview").innerHTML = '<i class="bi bi-camera"></i>';
    }
    card.dataset.costManual = "";
  }

  // Existing cost on a re-shown form counts as typed by hand.
  $$("[data-item]").forEach(card => {
    card.dataset.costManual = num(card.querySelector("[data-f=cost]")) > 0 ? "1" : "";
  });

  /** Items[3].X → Items[i].X in names, ids, labels and validation messages, so the server sees 0..n-1. */
  function renumber() {
    $$("[data-item]").forEach((card, i) => {
      card.querySelector("[data-item-no]").textContent = i + 1;
      card.querySelectorAll("[name], [id], [for], [data-valmsg-for]").forEach(el => {
        ["name", "id", "for", "data-valmsg-for"].forEach(attr => {
          const v = el.getAttribute(attr);
          if (!v) return;
          el.setAttribute(attr, v.replace(/Items\[\d+\]/g, "Items[" + i + "]").replace(/Items_\d+__/g, "Items_" + i + "__"));
        });
      });
    });

    const count = $$("[data-item]").length;
    countBadge.textContent = count;
    saveLabel.textContent = count > 1 ? "Save " + count + " orders" : "Save order";
    $$("[data-item-remove]").forEach(b => { b.hidden = count === 1; });
    addBtn.disabled = count >= maxItems;
    addBtn.title = count >= maxItems ? "Up to " + maxItems + " products at a time" : "";
    reparseValidation();
  }

  function reparseValidation() {
    if (!window.jQuery || !jQuery.validator || !jQuery.validator.unobtrusive) return;
    const $form = jQuery(form);
    $form.removeData("validator").removeData("unobtrusiveValidation");
    jQuery.validator.unobtrusive.parse(form);
  }

  function addCard(copyFrom) {
    if ($$("[data-item]").length >= maxItems) {
      window.ordex.toast("You can add up to " + maxItems + " products at a time.", "error");
      return;
    }
    const card = template.cloneNode(true);
    if (copyFrom) {
      // Copy text and prices (not the picture: a file can't be copied safely across inputs).
      const src = copyFrom.querySelectorAll("input:not([type=file]), textarea");
      const dst = card.querySelectorAll("input:not([type=file]), textarea");
      src.forEach((el, i) => { if (dst[i]) dst[i].value = el.value; });
      card.dataset.costManual = copyFrom.dataset.costManual || "";
      copyFrom.after(card);
    } else {
      list.appendChild(card);
    }
    renumber();
    recalcAll();
    card.classList.add("just-added");
    setTimeout(() => card.classList.remove("just-added"), 900);
    const focusEl = copyFrom ? card.querySelector("[name$='.ProductSize']") : card.querySelector("[data-f=name]");
    card.scrollIntoView({ behavior: "smooth", block: "center" });
    if (focusEl) setTimeout(() => focusEl.focus({ preventScroll: true }), 250);
  }

  function removeCard(card) {
    if ($$("[data-item]").length === 1) return;
    const hasData = card.querySelector("[data-f=name]").value.trim() || num(card.querySelector("[data-f=sell]")) > 0;
    if (hasData && !window.confirm("Remove product " + card.querySelector("[data-item-no]").textContent + "?")) return;
    card.remove();
    renumber();
    recalcAll();
  }

  addBtn.addEventListener("click", () => addCard(null));

  list.addEventListener("click", e => {
    const copy = e.target.closest("[data-item-copy]");
    const remove = e.target.closest("[data-item-remove]");
    if (copy) addCard(copy.closest("[data-item]"));
    if (remove) removeCard(remove.closest("[data-item]"));
  });

  list.addEventListener("input", e => {
    const card = e.target.closest("[data-item]");
    if (!card) return;
    if (e.target.matches("[data-f=cost]")) card.dataset.costManual = e.target.value.trim() !== "" ? "1" : "";
    recalcCard(card, currentRate());
    recalcTotals();
  });

  function recalcCard(card, rate) {
    const buy = card.querySelector("[data-f=buy]");
    const cost = card.querySelector("[data-f=cost]");
    const sell = card.querySelector("[data-f=sell]");
    const adv = card.querySelector("[data-f=adv]");

    if (!card.dataset.costManual && rate > 0 && num(buy) > 0) cost.value = (num(buy) * rate).toFixed(saleDecimals);

    const due = num(sell) - num(adv);
    const profit = num(sell) - num(cost);
    const out = card.querySelector("[data-item-summary]");
    if (num(sell) > 0) {
      out.innerHTML = "";
      const d = document.createElement("span");
      d.textContent = "Due " + money(due);
      if (due < 0) d.className = "neg";
      const p = document.createElement("span");
      p.textContent = "Profit " + money(profit);
      p.className = profit < 0 ? "neg" : "pos";
      out.append(d, p);
    } else {
      out.textContent = "";
    }
    adv.classList.toggle("input-validation-error", num(adv) > num(sell) && num(sell) > 0);
  }

  function recalcTotals() {
    let sell = 0, due = 0, profit = 0;
    $$("[data-item]").forEach(card => {
      const s = num(card.querySelector("[data-f=sell]"));
      sell += s;
      due += s - num(card.querySelector("[data-f=adv]"));
      profit += s - num(card.querySelector("[data-f=cost]"));
    });
    totalSell.textContent = money(sell);
    totalDue.textContent = money(due);
    totalProfit.textContent = money(profit);
    totalProfit.classList.toggle("neg", profit < 0);
  }

  function recalcAll() {
    const rate = currentRate();
    if (rateLabel) rateLabel.textContent = rate > 0 ? "1 " + buyCode + " = " + rate + " " + saleCode : "No rate set – type the cost yourself";
    $$("[data-item]").forEach(card => recalcCard(card, rate));
    recalcTotals();
  }

  // ═════════════════════════ Submit ═════════════════════════
  // Before validation runs (click comes before submit): drop extra product cards left completely empty.
  form.addEventListener("click", e => {
    if (!e.target.closest('button[type="submit"]')) return;
    const cards = $$("[data-item]");
    const blank = cards.filter(card =>
      !card.querySelector("[data-f=name]").value.trim() &&
      ["buy", "cost", "sell", "adv"].every(f => num(card.querySelector("[data-f=" + f + "]")) === 0) &&
      !(card.querySelector("input[type=file]").files || []).length);
    if (blank.length && blank.length < cards.length) {
      blank.forEach(card => card.remove());
      renumber();
      recalcAll();
    }
  }, true);

  // Registered on the form, so it runs before the site-wide "submit once" handler.
  form.addEventListener("submit", e => {
    if (!pickedBox.hidden) return;
    e.preventDefault();
    window.ordex.toast("Search and pick the customer, or tap “New customer”.", "error");
    term.focus();
  });

  // ═════════════════════════ Quick new batch ═════════════════════════
  const qbForm = document.querySelector("[data-quick-batch]");
  const qbModalEl = document.getElementById("quickBatchModal");
  if (qbForm && qbModalEl && batchSel) {
    const qbError = qbForm.querySelector("[data-quick-batch-error]");

    qbModalEl.addEventListener("show.bs.modal", () => {
      qbError.hidden = true;
      const rate = currentRate();
      const rateInput = qbForm.querySelector("[name=ExchangeRate]");
      if (!rateInput.value && rate > 0) rateInput.value = rate;
      const orderDate = field("OrderDate");
      if (orderDate && orderDate.value) qbForm.querySelector("[name=BatchDate]").value = orderDate.value;
    });

    qbForm.addEventListener("submit", async e => {
      e.preventDefault();
      qbError.hidden = true;
      if (!(parseFloat(qbForm.ExchangeRate.value) > 0)) {
        qbError.textContent = "Enter the exchange rate.";
        qbError.hidden = false;
        return;
      }

      const data = new FormData(qbForm);
      if (companySel) data.set("CompanyId", companySel.value || "0");
      if (unitSel) data.set("BusinessUnitId", unitSel.value || "0");

      const btn = qbForm.querySelector("button[type=submit]");
      btn.disabled = true;
      try {
        const res = await fetch("/lookup/batches", {
          method: "POST",
          body: data,
          credentials: "same-origin",
          headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest", "RequestVerificationToken": window.ordex.csrfToken() }
        });
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json.error || "Could not create the batch.");

        const opt = new Option(json.text, json.value, true, true);
        opt.dataset.rate = json.rate;
        batchSel.add(opt, batchSel.options[1] || null);
        batchSel.value = String(json.value);
        batchSel.dispatchEvent(new Event("change", { bubbles: true }));
        bootstrap.Modal.getOrCreateInstance(qbModalEl).hide();
        qbForm.reset();
        window.ordex.toast("Batch " + json.text.split(" · ")[0] + " created.");
      } catch (err) {
        qbError.textContent = err.message;
        qbError.hidden = false;
      } finally {
        btn.disabled = false;
      }
    });
  }

  // ═════════════════════════ Start ═════════════════════════
  renumber();
  recalcAll();
  if (!pickedBox.hidden) refreshCard();
  else if (window.matchMedia("(min-width: 992px)").matches) term.focus();
})();
