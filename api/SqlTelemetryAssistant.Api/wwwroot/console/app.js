// Wszystko uruchamiamy dopiero po załadowaniu DOM
window.addEventListener("DOMContentLoaded", () => {

    // Helper – bezpieczne podpinanie eventów
    function bind(id, handler) {
        const el = document.getElementById(id);
        if (!el) {
            console.warn("Brak elementu:", id);
            return;
        }
        el.addEventListener("click", handler);
    }

    // UI helpers
    function setStatus(text) {
        const label = document.getElementById("statusLabel");
        if (label) {
            label.innerText = "Status: " + text;
        }
    }

    function clearTable(bodyId) {
        const body = document.getElementById(bodyId);
        if (!body) return;
        body.innerHTML = `<tr><td colspan="10" class="muted">Brak danych</td></tr>`;
    }

    // 1) PULSE – /telemetry/pulse-kb
    async function loadPulse() {
        try {
            setStatus("pobieranie Pulse…");

            const res = await fetch("/telemetry/pulse-kb");
            if (!res.ok) throw new Error("HTTP " + res.status);
            const data = await res.json();

            renderPulse(data);
            await loadPulseRecommendation();

            setStatus("gotowe");
        } catch (err) {
            console.error("loadPulse error", err);
            setStatus("błąd Pulse");
        }
    }

    function renderPulse(data) {
        const body = document.getElementById("waitTableBody");
        if (!body) return;

        body.innerHTML = "";

        if (!data || !Array.isArray(data.telemetry) || data.telemetry.length === 0) {
            body.innerHTML =
                `<tr><td colspan="3" class="muted">Brak danych – kliknij „Odśwież Pulse”.</td></tr>`;
            return;
        }

        data.telemetry.forEach(w => {
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${w.waitType}</td>
                <td>${w.totalWaitMs.toFixed(0)}</td>
                <td>${w.percentage.toFixed(1)}%</td>
            `;
            body.appendChild(tr);
        });

        const ts = document.getElementById("pulseTimestamp");
        if (ts && data.generatedAtUtc) {
            ts.innerText = "Wygenerowano: " +
                new Date(data.generatedAtUtc).toLocaleString("pl-PL");
        }
    }

    // 2) Pulse + LLM rekomendacja – tymczasowo też /telemetry/pulse-kb
    async function loadPulseRecommendation() {
        try {
            const res = await fetch("/telemetry/pulse-kb");
            if (!res.ok) throw new Error("HTTP " + res.status);
            const data = await res.json();

            document.getElementById("recommendation").innerText = data.recommendation ?? "Brak rekomendacji.";

            const kb = document.getElementById("knowledge");
            kb.innerHTML = (data.knowledgeFragments && data.knowledgeFragments.length)
                ? data.knowledgeFragments.map(x => `<pre>${x}</pre>`).join("\n")
                : `<p class="muted">Brak fragmentów dokumentacji.</p>`;
        }
        catch (err) {
            console.error(err);
            document.getElementById("recommendation").innerText = "Błąd w rekomendacji.";
        }
    }

    // 3) Snapshot SQL + AI – /api/console/sql
    async function loadSqlAi() {
        try {
            setStatus("pobieranie SQL snapshot…");

            const res = await fetch("/api/console/sql");
            if (!res.ok) throw new Error("HTTP " + res.status);
            const data = await res.json();

            const snap = document.getElementById("sqlAiSnapshot");
            const rec = document.getElementById("sqlAiRecommendation");

            if (snap) snap.innerText = data.snapshot ?? "(brak snapshotu)";
            if (rec) rec.innerText = data.recommendation ?? "(brak rekomendacji)";

            setStatus("gotowe");
        } catch (err) {
            console.error("loadSqlAi error", err);
            setStatus("błąd snapshotu SQL");
        }
    }

    // 4) AISQL – naturalne zapytania
    async function runAiSql() {
        const questionEl = document.getElementById("aisqlQuestion");
        if (!questionEl) return;

        const question = questionEl.value;
        if (!question.trim()) return;

        setStatus("generowanie zapytania…");

        try {
            const res = await fetch(
                "/api/console/aisql?question=" + encodeURIComponent(question)
            );
            if (!res.ok) throw new Error("HTTP " + res.status);
            const data = await res.json();

            const sqlEl = document.getElementById("aisqlSql");
            const explEl = document.getElementById("aisqlExplanation");

            if (sqlEl) sqlEl.innerText = data.sql ?? "(brak T-SQL)";
            if (explEl) explEl.innerText = data.explanation ?? "(brak wyjaśnienia)";

            renderAiSqlResults(data.results);
            setStatus("gotowe");
        } catch (err) {
            console.error("runAiSql error", err);
            setStatus("błąd AISQL");
        }
    }

    function renderAiSqlResults(rows) {
        const head = document.getElementById("aisqlResultHead");
        const body = document.getElementById("aisqlResultBody");
        if (!head || !body) return;

        if (!rows || rows.length === 0) {
            clearTable("aisqlResultBody");
            return;
        }

        const columns = Object.keys(rows[0]);

        head.innerHTML =
            `<tr>${columns.map(c => `<th>${c}</th>`).join("")}</tr>`;

        body.innerHTML = rows
            .map(r =>
                `<tr>${columns.map(c => `<td>${r[c]}</td>`).join("")}</tr>`
            )
            .join("");
    }

    // System zakładek
    document.querySelectorAll(".tab-button").forEach(btn => {
        btn.addEventListener("click", () => {
            document.querySelectorAll(".tab-button")
                .forEach(b => b.classList.remove("active"));
            btn.classList.add("active");

            document.querySelectorAll(".tab-panel")
                .forEach(p => p.classList.remove("active"));

            const panel = document.getElementById("tab-" + btn.dataset.tab);
            if (panel) panel.classList.add("active");
        });
    });

    // Podpinamy eventy
    bind("refreshPulseBtn", loadPulse);
    bind("refreshSqlAiBtn", loadSqlAi);
    bind("runAiSqlBtn", runAiSql);

    // Na starcie nic nie ładujemy – użytkownik klika przyciski
});
