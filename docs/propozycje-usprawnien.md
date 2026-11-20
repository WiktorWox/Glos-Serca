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

### Plany dnia, zakończenia zadań i dymki przy wejściu (19 listopada 2026)

Zgłoszone przez Wiktora 18 listopada: Caroline w sklepie i przy oddawaniu zadań nie miała głosu, Gus przy wejściu do sklepu też nie. Przyczyny i zmiany:

- **Plany dnia** (`Strings/schedules/<postać>`): kwestia, którą postać mówi na końcu trasy (np. Caroline do Jodi u Pierre'a, `Tue.001`). Generator nie czytał tego pliku. Teraz czyta (`BuildFromScheduleStrings`).
- **Zakończenia zadań fabularnych** (pole 9 zadań `ItemDelivery`, `LostItem`, `SecretLostItem` w `Data/Quests`): np. „O, dynia!...” Caroline po zadaniu 108. Generator nie czytał tego pliku. Teraz czyta (`BuildFromQuestCompletions`).
- **Dymki `<Lokacja>_Entry`** (Gus `SeedShop_Entry`, Pam `Saloon_Entry` i `Trailer_Entry`, Lewis `Saloon_Entry`): gra pokazuje nad głową jeden losowy wariant z listy rozdzielonej `/`, a generator zapisywał całą listę jako jeden wpis. Teraz pierwszy wariant zostaje w dotychczasowym slocie, a każdy kolejny dostaje własny wpis (`BuildFromLocationEntryVariants`, ten sam klucz z kolejnym numerem strony).

Nowe źródła generator dopisuje na samym końcu, więc numery istniejących wpisów się nie zmieniają (sprawdzone porównaniem z szablonami z 9 listopada). W odtwarzaniu nic nie trzeba było zmieniać: plany dnia i zadania trafiają do zwykłego dopasowania po tekście, dymki do tego samego mechanizmu co inne dymki. Sprawdzone w grze 19 listopada: Caroline `Tue.001`, zadania 108 i 129 (dwie strony), 3 warianty Gusa i 3 Pam zagrały właściwe pliki.

Do paczek doszło 266 kwestii do nagrania; listy w `VoiceSources/<postać>/<postać>_plany_dnia_zadania.txt`. Dwie kwestie z planu dnia mają już identyczny nagrany tekst i nie zostały dodane osobno (Jas `spring.000` = 125.ogg, Willy `winter.000` = 278.ogg).

| Postać | Nowe kwestie | Pliki |
|---|---|---|
| Abigail | 12 | 578–589 |
| Alex | 6 | 548–553 |
| Caroline | 12 | 162–173 |
| Clint | 6 | 182–187 |
| Demetrius | 5 | 199–203 |
| Elliott | 4 | 484–487 |
| Emily | 28 | 511–538 |
| Evelyn | 8 | 142–149 |
| George | 7 | 156–162 |
| Gus | 15 | 182–196 |
| Haley | 7 | 562–568 |
| Harvey | 8 | 480–487 |
| Jas | 5 | 121–125 |
| Jodi | 9 | 181–189 |
| Kent | 2 | 114–115 |
| Leah | 4 | 546–549 |
| Leo | 7 | 149–155 |
| Lewis | 13 | 360–372 |
| Linus | 3 | 187–189 |
| Marnie | 7 | 177–183 |
| Maru | 8 | 497–504 |
| Pam | 14 | 263–276 |
| Penny | 8 | 569–576 |
| Pierre | 3 | 231–233 |
| Robin | 7 | 252–258 |
| Sam | 10 | 596–605 |
| Sandy | 12 | 106–117 |
| Sebastian | 9 | 527–535 |
| Shane | 9 | 460–468 |
| Vincent | 4 | 102–105 |
| Willy | 12 | 273–284 |
| Wizard | 2 | 141–142 |

Do decyzji zespołu:
- Pam ma nagrane 45.ogg i 46.ogg ze wszystkimi wariantami w jednym pliku; teraz grają przy pierwszym wariancie w całości. Trzeba je przyciąć do pierwszego wariantu albo nagrać ponownie.
- Wypłata za zlecenia z tablicy (`ItemDeliveryQuest.cs.135xx`, sklejana z losowych kawałków) nadal bez głosu, czeka na konsultację.

Przy okazji wyszło, że ścieżka po kluczu tłumaczenia w dialogach (`[V2-TK]`) nigdy nie trafia: gra podaje klucz z ukośnikami wstecznymi (`Strings\schedules\Caroline:Tue.001`), a paczka ma zwykłe (`Strings/schedules/...`). Wszystkie dialogi poza wydarzeniami grają więc dziś wyłącznie po tekście. Nie zmieniałem tego, bo dotyczy całego moda; to osobna, mała poprawka do przetestowania.

### Teksty ze wspólnych plików gry i okienka sklepów (20 listopada 2026)

Przegląd całego kodu gry (każde miejsce, w którym postać coś mówi) pokazał, że część kwestii gra bierze nie z pliku postaci (`Characters/Dialogue/<postać>`), tylko ze wspólnych plików tekstów. Generator ich nie czytał. Funkcja `BuildFromStringsCharacters` od jessevanga istniała, ale po jego przebudowie z 30 sierpnia 2025 nikt jej nie wywoływał, a i tak łapała tylko klucze `<Imię>_…`.

Nowe źródła generatora (`Class/DialogueParse/ModEntry.SharedGameStrings.cs`), dopisane na końcu, więc numery istniejących nagrań się nie zmieniają:
- **Kwestie konkretnych postaci:** Abigail w kopalni, Krobus i mroczny talizman, Henchman, Leo (wspomnienie), lista zapasów Pierre'a, Robin przy budowie domu, Elliott i książka, Demetrius/Maru/Lewis na festiwalach, Bramkarz, Pan Qi w klubie, Profesor Ślimak, telefony (automatyczne sekretarki i telefony do gracza), oświadczyny z własnym tekstem.
- **Kwestie ogólne** mówione przez wiele postaci, dodawane tylko tym, które mogą je powiedzieć, i tylko gdy postać nie ma własnej wersji, której gra użyłaby zamiast nich (reguły z kodu gry): uderzenie przez gracza, grzebanie w śmietniku (wg wieku), prezent urodzinowy (wg manier i płci postaci), kino (wg usposobienia, osobno dla małżonka), bukiet, wisior syreny, oświadczyny, rozwód, małżeństwo (bałagan, potwory w domu, okrzyki przy ataku, dobranoc), taniec na Festiwalu Kwiatów.
- **Okienko właściciela w sklepie** (`Data/Shops`, `BuildFromShopOwnerDialogues`), w tym stragany na Festiwalu Pustyni.
- **Oferty rozbudowy u Robin** (5 pytań tak/nie). Druga oferta ma w tekście wstawki, ale gra zawsze podaje te same wartości (65,000 monet, 100 twardego drewna), więc jest nagrywalna.

Pominięte: teksty ze wstawkami zmieniającymi się w grze (przedmiot, ilość, cena, imię innej postaci), komentarze o zakupach u Pierre'a i Willy'ego, formularz ankiety w stacji badawczej, okrzyki kibiców w saloonie i powitania między postaciami (dymki bez stałego mówcy), zlecenia z tablicy (czekają na konsultację).

Nowe w odtwarzaniu (`Class/ModEntry.MenuLines.cs`): okienko sklepu (ramka obok portretu, nie okno dialogowe) i okna pytań bez mówcy w warsztacie Robin. Oba dopasowują tekst tylko w paczce właściciela/Robin. Reszta trafia do istniejącego dopasowania po tekście i do obsługi dymków.

Sprawdzone w grze 20 listopada (pliki testowe TTS): Abigail w kopalni, telefon do Robin, zaproszenie Alexa do kina, odrzucenie bukietu, uderzenie (Lewis), 5 stron Krobusa, dymek „Pomocy!”, okienko sklepu Pierre'a i Robin, pytanie o rozbudowę domu.

Do paczek doszło 1012 kwestii (listy w `VoiceSources/<postać>/<postać>_wspolne_teksty.txt`), w tym cztery nowe paczki: Bramkarz, Henchman, Profesor Ślimak i Fizz. 63 kwestie pominięto, bo identyczny tekst już był w paczce tej postaci.

| Postać | Nowe kwestie | Pliki |
|---|---|---|
| Abigail | 58 | 590–647 |
| Alex | 50 | 554–603 |
| Bear | 6 | 10–15 |
| Bouncer (nowa paczka) | 4 | 1–4 |
| Caroline | 9 | 174–182 |
| Clint | 20 | 188–207 |
| Demetrius | 10 | 204–213 |
| Dwarf | 10 | 74–83 |
| Elliott | 46 | 488–533 |
| Emily | 47 | 539–585 |
| Evelyn | 13 | 150–162 |
| Fizz (nowa paczka) | 19 | 1–19 |
| George | 12 | 163–174 |
| Gus | 19 | 197–215 |
| Haley | 47 | 569–615 |
| Harvey | 47 | 488–534 |
| Henchman (nowa paczka) | 9 | 1–9 |
| Jas | 9 | 126–134 |
| Jodi | 14 | 190–203 |
| Kent | 13 | 116–128 |
| Krobus | 28 | 192–219 |
| Leah | 48 | 550–597 |
| Leo | 15 | 156–170 |
| Lewis | 13 | 373–385 |
| Linus | 13 | 190–202 |
| Marlon | 11 | 62–72 |
| Marnie | 20 | 184–203 |
| Maru | 48 | 505–552 |
| Mister Qi | 7 | 30–36 |
| MrQi | 7 | 20–26 |
| Pam | 12 | 277–288 |
| Penny | 45 | 577–621 |
| Pierre | 33 | 234–266 |
| Professor Snail (nowa paczka) | 22 | 1–22 |
| Robin | 31 | 259–289 |
| Sam | 46 | 606–651 |
| Sandy | 16 | 118–133 |
| Sebastian | 46 | 536–581 |
| Shane | 49 | 469–517 |
| Vincent | 13 | 106–118 |
| Willy | 15 | 285–299 |
| Wizard | 12 | 143–154 |

### Znana niedoróbka

Odpowiedzi w pytaniach `$q` w środku kwestii (np. Abigail na cmentarzu, `Event_Grave1..4`) trafiają do dopasowania tekstowego. Gdy to samo zdanie występuje w kilku odpowiedziach („Całe moje życie tu mieszkałam…” w Event_Grave2/3/4), gra pierwszy zarejestrowany wpis, czyli dobre słowa, ale inny dubel. Rozwiązanie: po odpowiedzi rozpoznać klucz po pierwszej stronie dialogu (jest unikalna) i grać dalsze strony po kluczu.

### Próby TTS do testów (nie do publikacji)

- Jedyny lokalny model z polskim: Chatterbox w wersji wielojęzycznej (`chatterbox-tts` ≥ 0.1.7, klasa `ChatterboxMultilingualTTS`, `language_id="pl"`, MIT, wagi 3,2 GB). Kokoro i Orpheus nie mają polskiego.
- Generowanie na M4 Max (MPS): około 2 s obliczeń na sekundę mowy; 21 kwestii Leo w 137 s. Barwa z `audio_prompt_path` (23 s zlepionych nagrań aktora) – klonowanie zero-shot, bez trenowania. Portret z paczki steruje pokrętłami `exaggeration`/`cfg_weight`.
- Wygenerowane pliki służą wyłącznie do testów działania moda i są wykluczone z gita (`.git/info/exclude`). Każde użycie głosu aktora, także testowe, wymaga jego zgody.
- Fine-tuning (dotrenowanie modelu na nagraniach aktora) to osobny, większy temat: potrzebne godziny audio z transkrypcją i GPU; kandydaci to Chatterbox, F5-TTS (MIT) lub Orpheus. Nagrania Głosu Serca są do tego gotowym materiałem, ale decyzja i zgody aktorów są warunkiem wstępnym.

## 5. Plan uzgodniony 20 listopada 2026

### Jedno źródło dopasowania: `DialogueText` (zrobione 20 listopada)

- `DialogueText` w paczce = tekst gry ze znacznikami (`{Farmer_Name}`, `%farm`, `%time`, `%book`, `{0}`, `{Portrait:…}`); zespół go nie zmienia (sprawdzone: 11 996 wpisów identycznych z generatorem).
- Mod buduje z niego dopasowanie przy wczytaniu: bez znaczników dosłownie, ze znacznikami jako regex z `.+?` w miejscu zmiennej; portrety pomijane; białe znaki ujednolicone po obu stronach do jednej spacji (spacja we wzorcu dosłownie, nie `\s*`).
- `DisplayPattern` przestaje być potrzebny (generator go nie zapisuje, mod ignoruje); znika wycinanie imion z tekstu na ekranie.
- Kolejność: klucz tłumaczenia z gry → dopasowanie po `DialogueText`; przybliżenie tylko w logu trybu deweloperskiego.

### Uwagi dla aktorów: pole `ActorNote` (zrobione 20 listopada)

- Osobne pole w JSON, mod je ignoruje; trafia do list `.txt` jako linia „Uwaga: …”.
- Generator wypełnia je automatycznie tylko objaśnieniem znaczników, bez własnych sformułowań: `{Farmer_Name}` imię gracza (wstawia gra), `%farm` nazwa farmy gracza, `%time` aktualna godzina w grze, `%book` losowy tytuł książki, `{0}` wartość wstawiana przez grę, `*kaszel*` opis odgłosu, nie słowo do przeczytania itd.
- Ręcznie dopisane uwagi (np. „czytamy »Czy to jest?«”) generator zachowuje.

Jak to wyszło w kodzie (20 listopada):
- Bardzo krótkie kwestie (mniej niż 5 liter stałego tekstu, np. „{Farmer_Name}...”, „Hej, {Farmer_Name}.”) dopasowują się tylko wtedy, gdy w miejscu imienia stoi faktyczne imię gracza; bez tego pasowałyby do niemal każdego tekstu. Krótkie kwestie bez imienia („{0}!?!” Gila, „Hej, {0}.” Willy'ego) grają po kluczu tłumaczenia.
- Kontrola offline wszystkich 12 044 wpisów (tekst z ekranu odtworzony z `DialogueText` z przykładowymi wartościami): 11 145 dosłownie, 895 regexem, 4 tylko po kluczu (wyżej), 0 błędnych plików. Jedyny wpis bez `DialogueText` (Evelyn `spring13`, pusty tekst) zachował stary `DisplayPattern`.
- Paczki: usunięte `DisplayPattern`, dodane `ActorNote` w 1 325 wpisach; teksty, numery i pliki bez zmian poza dwoma resztkami `%revealtaste…` (Emily 163, Lewis 97), których gra nie pokazuje.
- Listy `.txt`: 835 linii „Uwaga: …” w 67 plikach. 490 wpisów z uwagą nie ma listy w repozytorium (np. główne listy Alexa), więc ich uwagi są tylko w JSON.
- W grze sprawdzone: Shane „farma” i `%time`, Leah `%farm`, Elliott `%book` (3 strony), Abigail `%kid1 i %kid2`, Marnie `%pet`, Krobus 5 stron, Abigail z imieniem gracza, krótkie „Hej, Tester.” (Alex), „Tester!” (Clint), „Tester...” (Abigail), „Tester?” (Sebastian), Abigail `Sun2` ze zdaniem przed pytaniem i odpowiedzią, Emily bez `%revealtaste`, Willy `{0}` po kluczu, dymek Gusa, dymek Marnie z imieniem, okienko Pierre'a, oferta Robin.

### Zlecenia z tablicy (zrobione 20 listopada)

- Kawałki `ItemDeliveryQuest.cs.13526–13544` jako zwykłe wpisy w paczkach postaci wystawiających zlecenia (do ustalenia w kodzie gry, kto wystawia); stałe kwestie Czarnoksiężnika, Haley, Sama, Sebastiana, Maru, Abigail i Elliotta jako zwykłe dialogi.
- Pierwsze okienko rozpoznawane jednym regexem w kolejności powitanie → reakcja → podziękowanie (`^…$`), zbudowanym z wpisów paczki; drugie okienko (zapłata) zwykłym dopasowaniem.
- Nowe w modzie: kolejka odtwarzania kilku plików pod rząd.
- „Czy to jest {0}?” zespół nagrywa jako „Czy to jest?”.

Jak to wyszło (20 listopada):
- Kto wystawia zlecenia (sprawdzone w `ItemDeliveryQuest.GetValidTargetList` i `Data/Characters` w grze): 27 dorosłych mieszkańców miasta (`HomeRegion` „Town”) i Sandy (po naprawie autobusu); dzieci (Jas, Vincent) nie. Czarnoksiężnik w 1.6 nie wystawia zleceń (`HomeRegion` „Other”, brak `ItemDeliveryQuests`), więc jego podziękowanie (13555) pominięte.
- Nowe źródło generatora `BuildFromQuestDeliveries`: 22 postacie dostają kawałki 13526–13544 (Evelyn tylko powitanie „Och!”, bo gra innego jej nie daje), Haley, Sam, Maru, Abigail, Sebastian i Elliott swoje stałe kwestie; wszyscy 28 „To za mało... potrzebuję {0}.” (13615, gra po kluczu).
- Paczki: 410 nowych wpisów w 28 postaciach, numery dalej od ostatniego; listy `VoiceSources/<postać>/<postać>_zlecenia_z_tablicy.txt`.
- Mod (`Class/ModEntry.QuestReply.cs`): pierwsze okienko rozpoznaje jeden regex `^powitanie reakcja podziękowanie$` zbudowany z wpisów paczki i odtwarza trzy pliki pod rząd (kolejka w `ModEntry.Playback.cs`; każdy inny głos albo zamknięcie okienka ją czyści). Brakujące nagrania kolejka pomija.
- Kontrola offline: wszystkie 1 536 kombinacji kawałków we wszystkich paczkach dają właściwe trzy pliki. W grze (testowe nagrania TTS, niecommitowane): Pierre „Cześć Tester! Oo, czyli jednak widziałeś moją notkę? Doceniam to.” i „Hej. Czy to jest Pasternak? Wygląda wspaniale.” – trzy pliki po sobie, potem zapłata; Haley, Maru (2 strony), Pierre „To za mało...”.

### Komentarze o kupionych przedmiotach i okrzyki w saloonie (zrobione 20 listopada)

- Przyczyna błędu: `GetExtraDialogueForCharacter` (`ModEntry.cs`) przypisywał postaci każdy klucz `Data/ExtraDialogue` z jej imieniem w nazwie, a w `PurchasedItem_…` imię oznacza sklep (`_Willy`) albo mówiącego. Klucze `PurchasedItem_` są teraz pomijane w tej regule.
- Nowe źródło `BuildFromPurchasedItemComments` według reguł gry (`ShopLocation` i `FishShop.getPurchasedItemDialogueForNPC`, `Game1.UpdateShopPlayerItemInventory`): mówią mieszkańcy z `CanCommentOnPurchasedShopItems` albo z miasta, bez dzieci.
  - Sklep Pierre'a: Abigail, Caroline, Pierre, Haley, Elliott, Alex i Leah zawsze swoją kwestię; niegrzeczni tylko `_3_…_Rude`; nastolatki także `_Teen`; Jodi swoją `_2_QualityHigh_Jodi`. `PurchasedItem_3_NonRude` gra nigdy nie pokazuje (w kodzie wynik nie jest przypisywany), więc pominięte.
  - Sklep Willy'ego: kwestie `…_Willy` dla wszystkich poza Willym; Willy tylko `PurchasedItem_Pierre_…_Willy`.
- Nowe źródło `BuildFromSaloonCheers`: 15 okrzyków (`Strings/Characters:Saloon_good/bad/neutralEvent_0–4`) dla postaci, których plan dnia stawia je w sali z telewizorem (pole X > 26, Y < 12); w grze: Alex, George, Gus, Kent, Shane.
- Paczki: 673 komentarze w 27 postaciach i 75 okrzyków w 5 postaciach. Usunięte 27 błędnych, nienagranych wpisów: Willy 223–245 i Pierre 211–214 (także z list `Willy.txt` i `Pierre.txt`). Nowe listy: `<postać>_komentarze_zakupow.txt`, `<postać>_okrzyki_saloon.txt`.
- W grze (testowe TTS): Emily `PurchasedItem_1_QualityHigh_Willy` – strony 1–4, okienko się zamyka na `#$e#`, strona 5 przy następnej rozmowie; dymki Gusa „Punkt!!”, „Arrgh!”, „No weź...”; Willy `PurchasedItem_Pierre_QualityHigh_Willy` (nagranie aktora).

Odłożone na później (20 listopada): odmiana wstawianych słów (`Strings/Lexicon`, „świeży”), lista kwestii ze wstawkami do przeformułowania przez aktorów, literówki do zgłoszenia DNKSH („cały dam”, „..”).
