# Propozycje poprawek README

Ocena `README.md` i `README_EN.md` na gałęzi `glos-serca`, stan na 2026-11-08. Oba pliki są poprawne w tym, co mówią, ale nie pozwalają ani zainstalować moda, ani do niego dołączyć.

## Co jest dobrze i zostaje

- Jasna nazwa i jedno zdanie o tym, czym jest mod.
- Wskazanie bazy (VoiceOverFrameworkMod jessevanga) i licencji NVorbis. Kod jest na MIT, więc atrybucja musi zostać.
- Wersja angielska, przydatna dla autora upstreamu i innych zespołów dubbingowych.

## Czego brakuje dla gracza

1. **Wymagania.** Stardew Valley 1.6.15, SMAPI 4.x, polskie tłumaczenie DNKSH (które samo wymaga Content Patchera). Rozdzielić wprost: Content Patcher jest zależnością tłumaczenia, nie Głosu Serca. Bez tłumaczenia mod milczy.
2. **Instalacja krok po kroku** i ustawienie języka gry: przy wariancie „Podmiana" gra po portugalsku (pt-BR), przy wariancie „Mod" język `pl` z menu języków. Oba działają bez zmian w configu.
3. **Stan nagrań per postać.** Dziś 14 postaci prawie kompletnych, 8 częściowo, reszta bez nagrań. Gracz powinien to wiedzieć przed instalacją. Tabela do odświeżania przy każdym wydaniu.
4. **Konfiguracja.** Trzy opcje w GMCM i `config.json`: wyciszenie dźwięku pisania, głośność, opóźnienie startu głosu; plus tryb deweloperski i `LanguageAliases` dla nietypowych tłumaczeń.
5. **Znane ograniczenia.** Brak dubbingu listów i telewizji, część wydarzeń i dymków bez nagrań, kolejność prac.
6. **Zgłaszanie błędów.** Gdzie (GitHub issues, Discord?) i co załączyć: log SMAPI z włączonym trybem deweloperskim, nazwa postaci i tekst kwestii.

## Czego brakuje dla zespołu i współtwórców

7. **Credits.** Aktorzy i ich postacie, reżyseria, tłumaczenie, kod. Dla projektu społecznościowego to najważniejsza sekcja, a nie ma jej wcale. Ustalić, czy aktorzy chcą być wymienieni i pod jakimi nickami.
8. **Jak dodać nagrania.** Konwencja nazw (`N.ogg`, `N_male.ogg`, `N_female.ogg`, bez spacji i nawiasów), gdzie jest lista kwestii (`VoiceSources/<postać>/<postać>.txt`), jak nie zgubić numeracji (pominięta linia przesuwa wszystkie kolejne; patrz Abigail), jak oznaczać duble. Odsyłacz do `docs/propozycje-usprawnien.md`.
9. **Jak zbudować z kodu.** dotnet SDK, Pathoschild.Stardew.ModBuildConfig, `GamePath` w `stardewvalley.targets`, uwaga o czyszczeniu `bin/` po zmianie nazw plików audio (inaczej przyrostowa kopia zostawia starą zawartość).
10. **Relacja do upstreamu.** Co zmieniliśmy względem frameworka (dopasowanie po kluczu tłumaczenia, obsługa języków z modów, poprawki indeksu wydarzeń, loader) i że zmiany kodu nadają się do odesłania jako PR.

## Drobiazgi do poprawy

- Oba pliki zaczynają się od BOM (niewidoczny znak przed `#`). Usunąć.
- Link do frameworka podany dwa razy (w zdaniu i jako lista z jednym elementem).
- „Tworzony przez mniejsze i większe osobistości polskiej sceny Voice Actingowej" brzmi potocznie; neutralniej: „Tworzony przez społeczność polskich aktorów głosowych".
- README_EN: „polish" z małej litery, „Made on base of" to kalka; „Built on top of".
- Brak licencji projektu. Kod: MIT, z zachowaną notą jessevanga, w pliku `LICENSE`. Nagrania: osobna, nazwana wprost licencja (np. „wszelkie prawa zastrzeżone, do użytku wyłącznie w tym modzie"), bo MIT na audio raczej nie jest zamierzone.
- `Manifest.json` ma `UpdateKeys: Nexus:33061`, czyli stronę frameworka. Jeśli README poda link do pobierania, musi to być własna strona, a klucz aktualizacji trzeba podmienić; to samo dotyczy `UniqueID`.

## Proponowany układ README.md

```
# Głos Serca – polski dubbing dla Stardew Valley
(jedno zdanie + zrzut/odnośnik do próbki audio)

## Wymagania
## Instalacja
## Ustawienie języka gry (Podmiana vs Mod)
## Co jest już nagrane (tabela postaci)
## Ustawienia
## Znane ograniczenia
## Zgłaszanie błędów
## Twórcy (credits)
## Dla współtwórców: nagrania, budowanie z kodu
## Licencje (kod MIT + NVorbis MIT + nagrania)
## Podziękowania (jessevang, DNKSH)
```

README_EN.md: ten sam układ w skrócie, bez tabeli postaci (odsyłacz), z naciskiem na sekcje „Built on top of" i licencje.
