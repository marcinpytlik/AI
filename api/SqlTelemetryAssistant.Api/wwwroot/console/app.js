
let chartInstance = null;

async function fetchPulse() {
  setStatus("Pobieranie danych...", false);
  try {
    const res = await fetch("/telemetry/pulse-kb");
    if (!res.ok) {
      throw new Error(`HTTP ${res.status}`);
    }
    const data = await res.json();
    renderPulse(data);
    setStatus("Dane odświeżone.", false);
  } catch (err) {
    console.error(err);
    setStatus("Błąd podczas pobierania danych.", true);
  }
}

function renderPulse(data) {
  const telemetry = data.telemetry || [];
  const tbody = document.getElementById("waitTableBody");
  tbody.innerHTML = "";

  if (telemetry.length === 0) {
    const tr = document.createElement("tr");
    const td = document.createElement("td");
    td.colSpan = 3;
    td.className = "muted";
    td.textContent = "Brak danych telemetrycznych w ostatnim oknie czasowym.";
    tr.appendChild(td);
    tbody.appendChild(tr);
    return;
  }

  const sorted = [...telemetry].sort((a, b) => b.percentage - a.percentage).slice(0, 10);

  for (const row of sorted) {
    const tr = document.createElement("tr");

    const tdType = document.createElement("td");
    tdType.textContent = row.waitType;

    const tdTotal = document.createElement("td");
    tdTotal.textContent = row.totalWaitMs.toFixed(0);

    const tdPct = document.createElement("td");
    tdPct.textContent = row.percentage.toFixed(1);

    tr.appendChild(tdType);
    tr.appendChild(tdTotal);
    tr.appendChild(tdPct);
    tbody.appendChild(tr);
  }

  const labels = sorted.map(r => r.waitType);
  const values = sorted.map(r => r.percentage);

  const ctx = document.getElementById("waitChart").getContext("2d");
  if (chartInstance) {
    chartInstance.destroy();
  }

  chartInstance = new Chart(ctx, {
    type: "bar",
    data: {
      labels,
      datasets: [
        {
          label: "Udział waitów [%]",
          data: values
        }
      ]
    },
    options: {
      responsive: true,
      plugins: {
        legend: {
          labels: { color: "#e5e7eb" }
        }
      },
      scales: {
        x: {
          ticks: { color: "#e5e7eb" },
          grid: { display: false }
        },
        y: {
          ticks: { color: "#e5e7eb" },
          grid: { color: "rgba(148,163,184,0.25)" }
        }
      }
    }
  });

  const ts = data.generatedAtUtc
    ? new Date(data.generatedAtUtc).toLocaleString()
    : "(brak timestampu)";
  document.getElementById("pulseTimestamp").textContent = `Ostatnia aktualizacja: ${ts}`;

  const recEl = document.getElementById("recommendation");
  recEl.textContent = (data.recommendation || "").trim() || "Brak rekomendacji od modelu.";

  const kbEl = document.getElementById("knowledge");
  const kb = data.knowledgeFragments || [];
  if (kb.length === 0) {
    kbEl.innerHTML = "<p class=\"muted\">Brak załadowanych fragmentów z bazy wiedzy.</p>";
  } else {
    kbEl.innerHTML = "";
    kb.forEach((frag, idx) => {
      const block = document.createElement("section");
      block.style.marginBottom = "0.75rem";
      const title = document.createElement("h4");
      title.textContent = `Fragment #${idx + 1}`;
      title.style.margin = "0 0 0.25rem 0";
      title.style.fontSize = "0.85rem";
      title.style.color = "#22c55e";

      const pre = document.createElement("pre");
      pre.textContent = frag.trim();
      pre.style.whiteSpace = "pre-wrap";
      pre.style.margin = "0";
      pre.style.fontFamily = "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace";
      pre.style.fontSize = "0.8rem";

      block.appendChild(title);
      block.appendChild(pre);
      kbEl.appendChild(block);
    });
  }
}

function setStatus(text, isError) {
  const el = document.getElementById("statusLabel");
  el.textContent = `Status: ${text}`;
  if (isError) {
    el.style.borderColor = "#f97373";
    el.style.color = "#fecaca";
  } else {
    el.style.borderColor = "rgba(148, 163, 184, 0.4)";
    el.style.color = "#9ca3af";
  }
}

function setupTabs() {
  const buttons = document.querySelectorAll(".tab-button");
  const panels = {
    recommendation: document.getElementById("tab-recommendation"),
    knowledge: document.getElementById("tab-knowledge")
  };

  buttons.forEach(btn => {
    btn.addEventListener("click", () => {
      const tab = btn.getAttribute("data-tab");
      buttons.forEach(b => b.classList.toggle("active", b === btn));
      Object.keys(panels).forEach(key => {
        panels[key].classList.toggle("active", key === tab);
      });
    });
  });
}

document.addEventListener("DOMContentLoaded", () => {
  setupTabs();
  document.getElementById("refreshBtn").addEventListener("click", () => fetchPulse());
  // pierwsze auto-pobranie
  fetchPulse();
});
