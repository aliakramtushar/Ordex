/* Dashboard trend chart. Data comes from a JSON <script> block (CSP-safe). */
(function () {
  "use strict";

  const canvas = document.getElementById("trendChart");
  const dataEl = document.getElementById("trendData");
  if (!canvas || !dataEl || !window.Chart) return;

  const rows = JSON.parse(dataEl.textContent || "[]");
  let chart = null;

  const symbol = canvas.dataset.symbol ? canvas.dataset.symbol + " " : "";
  const lakh = (canvas.dataset.code || "BDT") === "BDT";

  // BDT: 1,50,000 -> 1.5L · others: 1,500,000 -> 1.5M · 12,000 -> 12k
  const short = v => {
    const a = Math.abs(v), sign = v < 0 ? "-" : "";
    if (!lakh && a >= 1000000) return sign + (a / 1000000).toFixed(a % 1000000 ? 1 : 0) + "M";
    if (lakh && a >= 100000) return sign + (a / 100000).toFixed(a % 100000 ? 1 : 0) + "L";
    if (a >= 1000) return sign + Math.round(a / 1000) + "k";
    return String(v);
  };

  const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();

  function draw() {
    if (chart) chart.destroy();

    const text = css("--muted");
    const grid = css("--border");

    chart = new Chart(canvas, {
      type: "bar",
      data: {
        labels: rows.map(r => r.label),
        datasets: [
          { label: "Collection", data: rows.map(r => r.collection), backgroundColor: css("--chart-1"), borderRadius: 6, maxBarThickness: 28 },
          { label: "Purchase", data: rows.map(r => r.purchase), backgroundColor: css("--chart-2"), borderRadius: 6, maxBarThickness: 28 },
          { label: "Profit", data: rows.map(r => r.profit), type: "line", borderColor: css("--chart-3"), backgroundColor: css("--chart-3"), tension: .35, pointRadius: 3, borderWidth: 2 }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: "index", intersect: false },
        plugins: {
          legend: { position: "bottom", labels: { color: text, boxWidth: 10, boxHeight: 10, usePointStyle: true, font: { size: 11 } } },
          tooltip: { callbacks: { label: c => c.dataset.label + ": " + symbol + Math.round(c.parsed.y).toLocaleString(lakh ? "en-IN" : "en-US") } }
        },
        scales: {
          x: { ticks: { color: text, font: { size: 11 } }, grid: { display: false } },
          y: { ticks: { color: text, font: { size: 11 }, callback: v => symbol + short(v) }, grid: { color: grid } }
        }
      }
    });
  }

  draw();
  document.addEventListener("ordex:theme", draw);
})();
