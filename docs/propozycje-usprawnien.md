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

## 4. Stan na 9 listopada 2026

### Źródło prawdy dla definicji paczki

Pliki `<Postać>_pt-BR.json` zespołu leżą na Dysku Google (folder projektu, podfoldery „ROM/NRO – <Postać>”, obok list kwestii dla aktorów i wgranych nagrań). 11 definicji w repozytorium (Alex, Clint, Demetrius, Elliott, Emily, Evelyn, Gus, Leo, Lewis, Robin, Sandy) było angielskimi szablonami z częściowym tłumaczeniem; zostały zastąpione wersjami z Dysku, pozostałe 21 było identycznych. Numeracja slotów wydarzeń w plikach z Dysku odpowiada polskiemu skryptowi (warianty płci `^` to osobne sloty), więc narzędzie do przenumerowania nie jest potrzebne.

Od tego czasu repozytorium ma już wpisy, których na Dysku nie ma (patrz niżej). Zasada do ustalenia z zespołem: albo Dysk jest nadrzędny i Wiktor przenosi tam zmiany z repozytorium, albo odwrotnie; dwa źródła prawdy rozjadą się szybko.

### Kwestie do nagrania dodane w repozytorium

| Postać | Pliki | Skąd | Lista dla aktora |
|---|---|---|---|
| Leo | 129–148 | pierwsze spotkanie w chacie (IslandHut 1039573); Leo gra tam jako aktor `ParrotBoy` | `VoiceSources/Leo/Leo_IslandHut_pierwsze_spotkanie.txt` |
| Parrot (papuga) | 1–2 | dwa mówione dymki w tej samej scenie (aktor `Toddler`); skrzeki `*Kra*` mają własny dźwięk gry | `VoiceSources/Parrot/Parrot_IslandHut.txt` |
| Caroline | 158–160 | napisy cutscenki z herbatą (Sunroom 719926) | `VoiceSources/Caroline/Caroline_cutscenki.txt` |
| Maru | 492–496 | napisy cutscenki z kometą (Mountain 5183338) | `VoiceSources/Maru/Maru_cutscenki.txt` |
| Abigail | 573–577 | okrzyki w Prairie King we dwoje (SeedShop 1) | `VoiceSources/Abigail/Abigail_cutscenki.txt` |

Mod obsługuje je przez aliasy aktorów (`ParrotBoy → Leo`, `Toddler → Parrot`, wbudowane; `ActorAliases` w `config.json` tylko dopisuje) oraz moduł napisów cutscenek (`Class/ModEntry.Cutscenes.cs`), który czyta wyświetlany tekst z obiektu gry i gra wpis o kluczu tekstowym gry (`Strings/Locations:Caroline_Tea_Event1` itd.). Wszystkie pozostałe komendy `cutscene` (samolot, robot, krowy, balon, gra planszowa) nie mają tekstu.

### Czego mod nie obejmuje (świadomie, na dziś)

- **Postacie mówiące w wydarzeniach bez paczki**, choć jessevang ma dla nich szablony: Krobus (185 kwestii), Morris (76), Dwarf (69), Marlon (61), Gunther (35), Grandpa (30), Governor (20), MrQi (16), Bear (9), Birdie (1). Na Dysku nie ma dla nich folderów. Henchman i Bouncer nie mają nawet szablonu.
- **Dymki w terapii ubraniowej** (ManorHouse 2123243): aktor `ClothesTherapyCharacters` to arkusz kilku mieszkańców (Shane, Robin, Abigail/Lewis), pięć krótkich dymków. Gdyby zespół chciał, wystarczy paczka o nazwie postaci `ClothesTherapyCharacters` z pięcioma wpisami nagranymi przez właściwych aktorów, bez zmian w kodzie.
- **Podpowiedzi o prezentach** („{0} nie cierpi '{1}'”, `Strings/StringsFromCSFiles:NPC.cs.41xx`): szablony z podstawianym imieniem i przedmiotem, wspólne dla wszystkich NPC; nienagrywalne bez składania z fragmentów.
- **Listy** (`Data/mail`) i **telewizja**: mod nic z nimi nie robi; turecki PR do upstreamu ma do tego kod (sekcja 2).

### Kwestie bez mówcy w wydarzeniach (narrator, Kel)

Komenda `message "..."` otwiera okno dialogowe bez postaci (`Game1.currentSpeaker == null`): tak mówi Kel w Forest 54 i jego gałęzi `choseInternet` („KEL: …”, „GŁOS Z ODDALI: …”), tak też pojawiają się didaskalia i listy czytane w scenkach. Generator szablonów `create_template` z frameworka przypisuje te linie ostatniej mówiącej postaci, więc w paczce Leah są to sloty s7, s10–s12, s15 (pliki 356, 359–361, 364) i `choseInternet` s1, s2, s5 (375, 376, 379), nagrane. Ścieżka wydarzeń w modzie pomijała jednak każde okno bez mówcy („narrator can be added later”), więc żadna z nich nie grała. Łącznie dotyczyło to 77 linii w 26 paczkach (Sebastian 9, Maru 9, Leah 8, Emily 6, Gus 4, Harvey 4, …).

Od 9 listopada mod odtwarza licznik generatora także dla takich okien (`GeneratorSpeakerForCurrent` w `Class/ModEntry.EventIndex.cs`): ustala, kto mówił ostatnio, i gra wpis `Events/<mapa>:<id>:s<n>` z jego paczki. Celowo bez dopasowania tekstowego, żeby linia narratora nie trafiła w cudzą paczkę. Sprawdzone w grze na Forest 54 (20 linii, w tym 5 Kela, wszystkie zagrały). Kwestia, kto nagrywa Kela, pozostaje decyzją zespołu: dziś gra to, co leży pod tymi numerami w paczce Leah.

### Znana niedoróbka

Odpowiedzi w pytaniach `$q` w środku kwestii (np. Abigail na cmentarzu, `Event_Grave1..4`) trafiają do dopasowania tekstowego. Gdy to samo zdanie występuje w kilku odpowiedziach („Całe moje życie tu mieszkałam…” w Event_Grave2/3/4), gra pierwszy zarejestrowany wpis, czyli dobre słowa, ale inny dubel. Rozwiązanie: po odpowiedzi rozpoznać klucz po pierwszej stronie dialogu (jest unikalna) i grać dalsze strony po kluczu.

### Próby TTS do testów (nie do publikacji)

- Jedyny lokalny model z polskim: Chatterbox w wersji wielojęzycznej (`chatterbox-tts` ≥ 0.1.7, klasa `ChatterboxMultilingualTTS`, `language_id="pl"`, MIT, wagi 3,2 GB). Kokoro i Orpheus nie mają polskiego.
- Generowanie na M4 Max (MPS): około 2 s obliczeń na sekundę mowy; 21 kwestii Leo w 137 s. Barwa z `audio_prompt_path` (23 s zlepionych nagrań aktora) – klonowanie zero-shot, bez trenowania. Portret z paczki steruje pokrętłami `exaggeration`/`cfg_weight`.
- Wygenerowane pliki służą wyłącznie do testów działania moda i są wykluczone z gita (`.git/info/exclude`). Każde użycie głosu aktora, także testowe, wymaga jego zgody.
- Fine-tuning (dotrenowanie modelu na nagraniach aktora) to osobny, większy temat: potrzebne godziny audio z transkrypcją i GPU; kandydaci to Chatterbox, F5-TTS (MIT) lub Orpheus. Nagrania Głosu Serca są do tego gotowym materiałem, ale decyzja i zgody aktorów są warunkiem wstępnym.
