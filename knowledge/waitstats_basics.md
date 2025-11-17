
# Wait Stats – podstawy interpretacji

W SQL Server wait stats pokazują, **na co czekają wątki** silnika bazy danych.
To nie jest lista błędów, tylko sygnał, gdzie silnik spędza czas bezczynnie.

Podstawowe zasady:
- Interpretuj waity w kontekście obciążenia i okna czasu.
- Patrz na waity dominujące procentowo w danym przedziale (np. TOP 3).
- Nie panikuj na widok pojedynczego wait_type – ważny jest **wzorzec**.
