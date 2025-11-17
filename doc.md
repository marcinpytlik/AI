jasne, wyświetlam wszystko tutaj, bez żadnych linków, w czystym markdown, żebyś mógł skopiować do VS Code / Hugo / GitHuba.



---

title: "SQLManiak – Telemetry \& AI Assistant"

subtitle: "Jak połączyć monitoring SQL Server z modelami językowymi, by stworzyć inteligentnego asystenta DBA"

date: 2025-11-17

tags: \[SQLServer, Telemetry, GenerativeAI, InfluxDB, Grafana, ASPNETCore, Ollama, OpenAI, RAG]

draft: false

---



> \_„Telemetry bez kontekstu to tylko dane. AI bez danych to tylko słowa. Połączone – stają się wiedzą.”\_  

> — SQLManiak



---



\## 🎯 Cel rozwiązania



Środowisko SQL Server jest pełne sygnałów: wait statystyki, zapisy logu, obciążenia buffer pool, praca TempDB, I/O… Ale codzienna analiza tego wszystkiego wymaga czasu, skupienia i doświadczenia.



Ten projekt pokazuje, jak zbudować \*\*lekkiego asystenta dla administratora SQL Server (DBA)\*\*, który łączy:



\- telemetrię z serwera,

\- własną bazę wiedzy (Markdown),

\- oraz modele językowe (LLM),



żeby generować \*\*konkretne rekomendacje diagnostyczne\*\*, zrozumiałe i spójne z praktykami zespołu.



To nie jest kolejny dashboard.  

To coś więcej: \*\*aktywny, kontekstowy partner do rozmowy o stanie serwera\*\*.



---



\## 🧱 Technologia i składniki



Cały system da się uruchomić z jednego pliku `docker-compose.yml`. Architektura jest modularna, a jednocześnie minimalistyczna:



\### 📡 Telemetria

\- \*\*Telegraf\*\* (plugin SQL Server) – zbiera:

&nbsp; - `sqlserver\_waitstats`

&nbsp; - `sqlserver\_schedulers`

&nbsp; - `sqlserver\_database\_io`

\- \*\*InfluxDB 2.x\*\* – przechowuje dane jako time-series (Flux query language)



\### 🔍 Wizualizacja

\- \*\*Grafana 11\*\* – dashboardy z logiką opartą o Flux (bez dodatkowych agentów)



\### 🧠 Inteligencja

\- \*\*Minimalne API (ASP.NET Core 9, .NET 9.0)\*\* – serce rozwiązania:

&nbsp; - odpytuje InfluxDB przez REST API,

&nbsp; - generuje prompt (w tym powiązane pliki Markdown),

&nbsp; - wysyła go do wybranego LLM,

&nbsp; - zwraca JSON z rekomendacją.



\- \*\*Model LLM, do wyboru:\*\*

&nbsp; - OpenAI GPT (np. `gpt-4.1-mini`) – opcja w chmurze,

&nbsp; - Ollama + LLaMA 3.1 – opcja lokalna, w kontenerze.



\### 🖥️ Interfejs użytkownika

\- \*\*DBA Console\*\* – statyczny frontend (HTML + CSS + Chart.js)

&nbsp; - tabela najlepszych waitów,

&nbsp; - wykres słupkowy,

&nbsp; - panel z rekomendacją LLM,

&nbsp; - zakładka z użytymi fragmentami wiedzy (`knowledge/\*.md`).



Wszystko serwowane \*\*bez dodatkowego serwera\*\*, bez odwiedzania Swaggera czy pisania requestów — po prostu otwierasz przeglądarkę.



---



\## 🧠 Jak to działa (krok po kroku)



1\. \*\*Telegraf\*\* zbiera dane z SQL Servera co 10 sekund i wrzuca je do bucketu `sql\_telemetry` w InfluxDB.

2\. API wywołuje zapytanie Flux (`sum(column: "wait\_time\_ms")`) w zakresie ostatnich kilku minut.

3\. Wyniki są:

&nbsp;  - grupowane,

&nbsp;  - sortowane,

&nbsp;  - reprezentowane jako `WaitStatPulse`.

4\. Na podstawie waitów dobierane są odpowiednie pliki `.md` z katalogu `knowledge/` (np. `io\_and\_writelog.md` dla `WRITELOG`).

5\. Budowany jest \*\*prompt\*\* z:

&nbsp;  - listą wait statystyk,

&nbsp;  - fragmentami dokumentacji,

&nbsp;  - instrukcją dla modelu („Jesteś doświadczonym DBA…”).

6\. Prompt trafia do wybranego modelu:

&nbsp;  - w chmurze (OpenAI),

&nbsp;  - lokalnie (Ollama),

7\. Model zwraca rekomendację: diagnozę + kroki działania.

8\. Konsola DBA pokazuje wszystko w prostym UI.



---



\## 📋 Przykład scenariusza



> Dominujące waity: `WRITELOG (55%)`, `PAGEIOLATCH\_SH (22%)`, reszta marginalna.



System to zinterpretuje jako:



\- „log transakcyjny jest wąskim gardłem, dysk nie nadąża”

\- „czy log jest na odpowiednim storage?”

\- „czy autogrowth logu nie leci co chwilę?”

\- „czy duże operacje nie zalewają logu?”



A w konsoli zobaczysz np.:



```text

💡 Rekomendacja



1\. Dominuje WRITELOG (55%) – wskazuje na obciążenie logu transakcyjnego.

2\. Możliwe przyczyny:

&nbsp;  - zbyt wolny dysk, brak dedykowanego storage,

&nbsp;  - częste autogrowth logu,

&nbsp;  - masowe operacje INSERT/UPDATE bez optymalizacji.

3\. Proponowane działania:

&nbsp;  - sprawdź średnie I/O logu przez sys.dm\_io\_virtual\_file\_stats,

&nbsp;  - ustaw większy rozmiar początkowy logu oraz rozmiar przyrostu,

&nbsp;  - rozważ przeniesienie pliku .ldf na szybszy storage (NVMe).

---

📚 Bazowa dokumentacja użyta:

\- io\_and\_writelog.md

\- waitstats\_basics.md



🧩 Architektura logiczna

&nbsp;  SQL Server

&nbsp;      ┃

&nbsp;      ┃  (T-SQL / DMVs)

&nbsp;      ▼

&nbsp;  Telegraf

&nbsp;      ┃

&nbsp;      ┃  (HTTP / line protocol)

&nbsp;      ▼

&nbsp;  InfluxDB (Flux)

&nbsp;      ┃

&nbsp;      ┃  (REST / query API)

&nbsp;      ▼

ASP.NET Core 9 (API) 

&nbsp;      ┃

&nbsp;      ┃   ┌───────────┐

&nbsp;      ┣━━▶│ LLM: GPT  │

&nbsp;      ┃   └───────────┘

&nbsp;      ┃        ▲

&nbsp;      ┃        │

&nbsp;      ┃   ┌──────────┐

&nbsp;      ┗━━▶│ Markdown │

&nbsp;          └──────────┘

&nbsp;      │

&nbsp;      ▼

&nbsp; DBA Console (Web UI)



🚀 Dlaczego warto?



Myślenie zintegrowane

Nie tylko wykresy – wykres + komentarz eksperta.



Świadomość kontekstu

RAG (retrieval augmented generation) poprzez fragmenty .md konkretne dla danego typu problemu.



Bez halucynacji

Model nie „wymyśla” przypadkowych rzeczy — działa na faktycznym statusie serwera i Twojej dokumentacji.



Offline lub online

Możesz korzystać z AI nawet w zamkniętych środowiskach — Ollama działa lokalnie.



Idealne demo na DevAI / meetup / szkolenie

Pokazujesz:



🔹 telemetria

🔹 LLM

🔹 własna baza wiedzy

🔹 integracja z Grafaną

🔹 UI w przeglądarce



To nie jest przyszłość pracy DBA.

To już się dzieje — a Ty to masz u siebie jako prototyp gotowy do rozbudowy.



SQLManiak • 2025

„Co potrafi system, zależy od tego, jak rozmawia z danymi.”

