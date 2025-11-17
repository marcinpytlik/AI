
# CXPACKET / CXCONSUMER – równoległość

Waity CXPACKET / CXCONSUMER zwykle mówią:
- zapytania wykonywane są równolegle,
- część wątków czeka na inne wątki wykonujące fragment planu.

Praktyczne wskazówki:
- Sprawdź aktualne ustawienie MAXDOP oraz Cost Threshold for Parallelism.
- Zidentyfikuj najbardziej obciążające zapytania w Query Store lub sys.dm_exec_query_stats.
- Rozważ:
  - tunning indeksów,
  - zmianę zapytań,
  - dopasowanie MAXDOP do liczby rdzeni oraz charakteru obciążenia.
