/* ==========================================================================
   Order form:
   • Cost (sales currency) = purchase price × rate (rate from the selected batch, else company default).
     Once the user edits BDT by hand we stop overwriting it.
   • Live Due (selling − advance) and Profit (selling − cost).
   • Type 3+ digits of a mobile number → pick an existing customer → auto-fill.
   ========================================================================== */
(function () {
  "use strict";

  const form = document.querySelector("[data-order-form]");
  if (!form) return;

  const $ = name => form.querySelector('[name="' + name + '"]');
  const sgd = $(form.dataset.sgdField || "PurchasePriceSgd");
  const bdt = $(form.dataset.bdtField || "ProductPriceBdt");
  const selling = $("SellingPrice");
  const advance = $("AdvanceAmount");
  const batch = $("BatchId");
  const rateLabel = form.querySelector("[data-rate-label]");
  const dueOut = form.querySelector("[data-calc-due]");
  const profitOut = form.querySelector("[data-calc-profit]");
  const defaultRate = parseFloat(form.dataset.defaultRate || "0") || 0;

  // On edit, the saved BDT is treated as "manual" so we never silently change it.
  let bdtManual = form.dataset.isNew !== "true" && parseFloat(bdt.value || "0") > 0;

  const num = el => {
    const v = parseFloat((el && el.value || "").replace(/,/g, ""));
    return isNaN(v) ? 0 : v;
  };

  // Currency of the company being viewed (set on the form by the server).
  const saleSymbol = form.dataset.saleSymbol || "";
  const saleCode = form.dataset.saleCode || "BDT";
  const buyCode = form.dataset.buyCode || "SGD";
  const saleDecimals = parseInt(form.dataset.saleDecimals || "0", 10) || 0;
  const money = v => {
    const text = Math.abs(v).toLocaleString(saleCode === "BDT" ? "en-IN" : "en-US",
      { minimumFractionDigits: saleDecimals, maximumFractionDigits: saleDecimals });
    return (v < 0 ? "-" : "") + (saleSymbol ? saleSymbol + " " : "") + text;
  };

  function currentRate() {
    const opt = batch && batch.selectedOptions && batch.selectedOptions[0];
    const r = opt && parseFloat(opt.dataset.rate || "");
    return r > 0 ? r : defaultRate;
  }

  function recalc() {
    const rate = currentRate();
    if (rateLabel) rateLabel.textContent = rate > 0 ? "1 " + buyCode + " = " + rate + " " + saleCode : "No rate set – enter the cost manually";

    if (!bdtManual && rate > 0 && num(sgd) > 0) {
      bdt.value = (num(sgd) * rate).toFixed(2);
    }

    if (!dueOut || !profitOut) return; // stock form has no selling price

    const due = num(selling) - num(advance);
    const profit = num(selling) - num(bdt);

    dueOut.textContent = money(due);
    dueOut.classList.toggle("neg", due < 0);

    profitOut.textContent = money(profit);
    profitOut.classList.toggle("neg", profit < 0);
    profitOut.classList.toggle("pos", profit > 0);
  }

  if (!sgd || !bdt) return;

  bdt.addEventListener("input", () => { bdtManual = bdt.value.trim() !== ""; recalc(); });
  [sgd, selling, advance].forEach(el => el && el.addEventListener("input", recalc));
  if (batch) batch.addEventListener("change", () => { bdtManual = false; recalc(); });

  const resetBtn = form.querySelector("[data-recalc-bdt]");
  if (resetBtn) resetBtn.addEventListener("click", e => { e.preventDefault(); bdtManual = false; recalc(); });

  recalc();

  // ───────── Customer auto-fill by mobile / name ─────────
  const mobile = $("Mobile");
  const list = form.querySelector("[data-customer-list]");
  if (!mobile || !list || !window.ordex) return;

  let timer = null;
  let items = [];

  function fill(c) {
    $("Mobile").value = c.mobile || "";
    $("CustomerName").value = c.name || "";
    if (c.socialLink) $("SocialLink").value = c.socialLink;
    if (c.address && !$("DeliveryAddress").value.trim()) $("DeliveryAddress").value = c.address;
    list.classList.remove("show");
    window.ordex.toast("Customer details filled in.");
  }

  function render() {
    list.innerHTML = "";
    if (!items.length) { list.classList.remove("show"); return; }
    items.forEach(c => {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "ac-item";
      const name = document.createElement("span");
      name.textContent = c.name;
      const meta = document.createElement("small");
      meta.textContent = c.mobile + (c.address ? " · " + c.address : "");
      b.append(name, meta);
      // mousedown + preventDefault keeps focus in the input, so the list
      // doesn't move (blur validation) before the tap is completed.
      b.addEventListener("mousedown", e => e.preventDefault());
      b.addEventListener("click", () => fill(c));
      list.appendChild(b);
    });
    list.classList.add("show");
  }

  mobile.addEventListener("input", () => {
    clearTimeout(timer);
    const term = mobile.value.trim();
    if (term.length < 3) { items = []; render(); return; }
    timer = setTimeout(async () => {
      try {
        items = await window.ordex.getJson("/lookup/customers?term=" + encodeURIComponent(term));
        render();
      } catch (e) { /* offline: just type the details */ }
    }, 250);
  });

  document.addEventListener("click", e => { if (!list.contains(e.target) && e.target !== mobile) list.classList.remove("show"); });
})();
