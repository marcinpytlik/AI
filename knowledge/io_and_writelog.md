
# IO i WRITELOG – wskazówki SQLManiaka

Jeśli dominują PAGEIOLATCH_* lub WRITELOG, zwykle oznacza to:
- zbyt wolny subsystem dyskowy,
- nadmierne obciążenie logu transakcyjnego,
- brak odpowiedniej konfiguracji plików danych/logów.

Dobre praktyki:
- Sprawdź `sys.dm_io_virtual_file_stats` dla plików danych i logu.
- Upewnij się, że log jest na szybkim storage i ma odpowiedni rozmiar (unikaj częstych autogrowth).
- Analizuj operacje generujące dużo logu (masowe INSERT/UPDATE/DELETE, indeksy klastrowe/niesklastrowe).
