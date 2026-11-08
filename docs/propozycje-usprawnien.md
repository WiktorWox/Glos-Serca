# Propozycje usprawnień

Stan na 2026-11-08, gałąź `glos-serca`. Zebrane podczas porządkowania paczki i kodu; nic z poniższego nie jest jeszcze zrobione.

## 1. Duble i warianty nagrań

### Co leży w paczce

W folderach audio są nagrania, do których żaden wpis JSON się nie odwołuje, bo nazwa pliku ma dopisek. Są to prawdziwe, różne nagrania (inne długości, inne sumy kontrolne), nie kopie.

| Postać | Kwestie bez wybranego dubla (dziś nieme) | Kwestie z plikiem i dodatkowymi dublami | Wzorce nazw |
|---|---|---|---|
| Shane | 76 | 29 | `N(1)`, `N(2)`, `N(3)`, `N(poprawa1)`, `N_pijany`, `N_trzeźwy`, `N_podchmielony` |
| Sebastian | 10 | 0 | `Na`, `Nb`, `Nc` |
| Haley | 7 | 0 | `N_male_a`, `N_male_b`, `N_male_c` |
| Marnie | 1 | 0 | `53-001`, `53-002` |
| Vincent | 0 | 2 | `3.1`, `3.2` |
| Penny | 0 | 3 | `1 (2)`, folder `Stara wersja` |

Razem 94 nagrane kwestie, które w grze milczą wyłącznie z powodu nazwy pliku. U Shane'a to 76 ze 106 brakujących.

### Trzy rodzaje przypadków

**A. Zwykłe duble** (`(1)/(2)`, `a/b/c`, `-001/-002`, `poprawa1/2`). Różnią się tylko wykonaniem.
Propozycja: reguła automatyczna „ostatni dubel wygrywa" (`poprawa2` > `poprawa1` > oryginał, `(3)` > `(2)` > `(1)`, `c` > `b` > `a`) tworząca kopię pod nazwą oczekiwaną przez JSON. Oryginały zostają, zespół może podmienić ręcznie. Alternatywa: losowe odtwarzanie dubli (patrz sekcja 2).

**B. Nastrój zakodowany w tekście.** `116_pijany` („Taa... *czkawka*... Osszywiście, Sskarbeńku"), `29_pijany` („Taniec? ... daj mi tylko wypić jeszcze tro..."), `27_podchmielony` („Albo jeszcze jednego..."), `255(pijany)` (wydarzenie za 2 serca, Shane pijany nad jeziorem). Istnieje tylko jedna wersja, a dopisek opisuje grę aktora.
Propozycja: podpiąć od razu pod oczekiwaną nazwę; nie ma czego wybierać.

**C. Cała scena w dwóch nastrojach.** `370–390_podchmielony`: komplet 21 linii wydarzenia z meczem gridballa (BusStop 9581348), nagrany na trzeźwo i na rauszu; dla 377 i 378 istnieje tylko wersja na rauszu.
Propozycja: jedna decyzja reżyserska na całą scenę. Tekst („Może za dużo wypiłem...") sugeruje wersję podchmieloną.

**D. Prawdziwie zależne od sytuacji w grze.** Tylko trzy kwestie: `40` (secondChance_Guys) oraz `69` i `70` (Fri8). Shane mówi je zarówno w ciągu dnia na ranczu, jak i wieczorem w Saloonie.
Propozycja krótkoterminowa: wersja trzeźwa jako domyślna.
Propozycja docelowa: warianty kontekstowe po sufiksie w modzie. Jeśli dla wpisu istnieje plik `N_pijany.ogg`, a NPC stoi w lokacji `Saloon` po 17:00, mod wybiera go zamiast `N_trzeźwy.ogg`/`N.ogg`. Gra nie ma stanu „pijany", więc kontekst trzeba wywnioskować z miejsca i pory. Mała, ogólna funkcja; wdrażać dopiero, gdy takich par będzie więcej niż trzy.

### Co trzeba ustalić z zespołem nagraniowym

- Konwencja nazw dubli na przyszłość (jedna, np. `N.ogg` = wybrany, `N.take2.ogg` = alternatywa), żeby loader mógł je rozpoznać.
- Kto wybiera dubel: aktor przy oddaniu, czy reżyser po odsłuchu.
- Czy warianty pijany/trzeźwy mają być nagrywane dalej (wtedy funkcja z punktu D ma sens), czy aktor ma wybierać sam.

## 2. Funkcje z PR #1 do oryginalnego frameworka

Autor: `fantalatone` (turecki projekt dubbingu), PR otwarty 2026-07-31, sześć commitów do 2026-10-23, 14 plików, stan „mergeable", bez reakcji właściciela repozytorium. Licencja MIT pozwala wziąć kod bezpośrednio.

| Funkcja | Pliki | Przydatność dla Głosu Serca |
|---|---|---|
| Losowe nagrania dla jednej kwestii | `ModEntry.Loading.cs`, `Models.cs` | Wysoka. Rozwiązuje problem dubli z sekcji 1A bez wybierania: wszystkie `(1)/(2)/(3)` grają naprzemiennie. Trzeba sprawdzić, jaką konwencję nazw zakłada. |
| Głośność per postać | `Models.cs`, `Playback.cs` | Wysoka jako bezpiecznik. Nagrania różnią się o 12 dB między aktorami (-16 do -29 LUFS). Normalizacja plików offline jest lepsza, ale mnożnik per postać pozwala poprawić bez ponownego eksportu. |
| Dubbing listów (`Data/mail`) | `ModEntry.Mail.cs`, słownik `Mails_tr-TR.json` | Średnia. Nowe pole do nagrań; zależy od planów zespołu. |
| Dubbing telewizji (kanały, własne menu TV) | `ModEntry.TV.cs`, `Patches/PatchTV.cs`, `Menus/TVMenu.cs` | Niska na dziś. Duży patch Harmony na TV, do oceny osobno. |

Zalecana kolejność: najpierw głośność per postać i losowe nagrania (małe, izolowane zmiany), listy i TV po decyzji o zakresie nagrań. Każdą funkcję przetestować w środowisku testowym tak samo jak kod Wiktora, zanim trafi na gałąź.

Warto też nawiązać kontakt z autorem PR: dwa zespoły utrzymują równoległe forki tego samego, nieaktywnego frameworka.

## 3. Pozostałe pomysły (zebrane wcześniej)

- **Wykrywanie trybu tłumaczenia zamiast aliasu w configu.** Mod widzi listę załadowanych modów; obecność `DNKSH.spolszczenie` przy grze w pt-BR oznacza polski tekst. Dziś robi to domyślny alias `pt-BR → pl` w configu; wykrywanie zdjęłoby go z gracza.
- **Głośne ostrzeżenie o niezgodności języka.** Gdy język gry nie pasuje do żadnej paczki ani aliasu, jednorazowy wpis WARN w logu zamiast ciszy. Najczęstsze zgłoszenie od graczy.
- **Fuzzy matching jako ostatnia deska ratunku.** Po dopasowaniu po kluczu zostawić fuzzy tylko dla dialogów bez klucza i dymków, z progiem 0,90 zamiast 0,80, minimalną długością tekstu i logiem każdego użycia. Przed zmianą progu policzyć w środowisku testowym, jak często fuzzy w ogóle się odzywa.
- **Normalizacja głośności plików.** Dwuprzebiegowy `loudnorm` (ffmpeg) do około -18 LUFS / -1,5 dBTP dla całej paczki zamiast wzmocnienia 400% z obcinaniem. Skan głośności wszystkich plików jest gotowy.
- **Słownik polski** `Dictionary/V2/<postać>_pl.json` generowany z plików Spolszczenia, żeby ścieżka słownikowa działała dla dialogów bez klucza z innych modów. Wymaga odświeżania przy zmianach tłumaczenia.
- **Uproszczenie formatu paczki.** Skoro klucz tłumaczenia jest pierwszy, pole `DisplayPattern` starzeje się razem z tłumaczeniem. Regenerować je automatycznie przed wydaniem albo zrezygnować z niego i trzymać tylko klucz, stronę i plik. Większa decyzja, do omówienia z Wiktorem.
- **Własny `UniqueID` moda.** Dziś to `Darkmushu1.VoiceOverFrameworkMod`, identyfikator oryginału: SMAPI pokazuje fałszywą aktualizację, a instalacja obu modów naraz kończy się konfliktem. Jeśli upstream przyjmie poprawki z gałęzi, Głos Serca może wrócić do bycia zwykłym content packiem bez kodu.
- **Kontrola numeracji pozostałych postaci.** Metoda z Abigail (odchylenia długości tekstu vs czasu nagrania, potem odsłuch kilku plików) dla Leah, Shane, Pam, Willy, Marnie, Caroline, Maru, Wizard, Linus, Leo, Kent, George, Sam.
- **Willy 154**: JSON oczekuje wersji męskiej, istnieje tylko żeńska. Do wyjaśnienia z zespołem.
