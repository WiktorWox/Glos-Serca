# Błędy generatora szablonów (`create_template`)

Znalezione 20 listopada 2026 podczas testów kwestii ze znacznikami (`%kid1`, `%farm`…). Błędy siedzą w kodzie generatora z oryginalnego frameworka jessevanga (`Class/DialogueParse/ModEntry.DialogueUtil.cs`), a nie w tłumaczeniu DNKSH. Błędy 1 i 2 są naprawione 20 listopada (niżej opis zmian), błąd 3 jeszcze nie.

## 1. Znacznik na początku kwestii traci `%` (naprawione)

Generator usuwa `%` z początku kwestii (`RxLeadingNarr`, wzorzec `^\s*%+`), bo w grze `%` na początku oznacza narrację bez portretu. Gra robi jednak wyjątek dla znaczników z listy `Dialogue.percentTokens` (`%kid1`, `%kid2`, `%farm`, `%year`…) i takich kwestii nie traktuje jako narracji. Generator tego wyjątku nie zna.

Skutek:
- tekst dla aktora (`DialogueText`) zaczyna się od gołego słowa, np. „kid1 i %kid2…”, więc aktor może przeczytać „kid1” dosłownie;
- wzorzec dopasowania (`DisplayPattern`) traci całe początkowe słowo, a przez błąd nr 2 także dalsze znaczniki, więc mod nie znajduje nagrania.

Sprawdzone w grze: Abigail `MarriageDialogueAbigail:TwoKids_0`. Gra pokazuje poprawnie „dziecko i drugie dziecko zjadły swoje czekoladowe płatki. Ale się darły!” (testowy zapis nie ma dzieci, więc gra wstawia „dziecko”), w paczce jest:

| Pole | Wartość |
|---|---|
| Tekst w tłumaczeniu | `%kid1 i %kid2 zjadły swoje czekoladowe płatki. Ale się darły!` (poprawny) |
| Tekst dla aktora w paczce | `kid1 i %kid2 zjadły swoje czekoladowe płatki. Ale się darły!` |
| Wzorzec w paczce | `i zjadły swoje czekoladowe płatki. Ale się darły!` |
| Wynik | nagranie (377.ogg) nie zagrało |

Kwestie w tłumaczeniu, które zaczynają się od znacznika (cała kwestia albo jej strona):

| Plik | Klucz | Tekst |
|---|---|---|
| `Characters/Dialogue/MarriageDialogue` | `TwoKids_0` | %kid1 i %kid2 już jedli śniadanie. Straszne z nich głodomory! |
| `Characters/Dialogue/MarriageDialogueAbigail` | `TwoKids_0` | %kid1 i %kid2 zjadły swoje czekoladowe płatki. Ale się darły! |
| `Characters/Dialogue/MarriageDialogueShane` | `OneKid_1` | %kid1 musi jeść dużo jajek... trzeba se jakoś wyrobić te nogi! |
| `Characters/Dialogue/MarriageDialoguePenny` | `OneKid_1` | %kid1 będzie tu miał wspaniałe dzieciństwo. |
| `Characters/Dialogue/MarriageDialoguePenny` | `TwoKids_0` | %kid1 i %kid2 już zjadły obiad. Straszne z nich głodomory! |
| `Characters/Dialogue/MarriageDialogueAlex` | `OneKid_1` | %kid1 będzie siłaczem, tak jak tatuś. |
| `Data/ExtraDialogue` | `SummitEvent_Outro_Lewis` (strona 2) | %year lat temu martwiłem się przyszłością doliny. |

Naprawa: w `RxLeadingNarr` nie usuwać `%`, gdy za nim stoi znacznik z listy gry, i przeliczyć te wpisy w paczkach (tekst dla aktora i wzorzec; numery i nagrania bez zmian). Dla zespołu nagraniowego: w tych kwestiach na początku jest imię dziecka albo liczba lat, którą aktor pomija lub omija przeformułowaniem.

## 2. Reguła usuwania znaczników wycina zwykłe słowa (naprawione)

`RxStripTokens` miał usuwać znaczniki (`%farm`, `%pet`…) ze wzorca dopasowania. Od commita jessevanga `f5b730e` (6 września 2025) `%` jest w nim opcjonalny i nie ma granicy słowa, więc reguła wycina te ciągi liter z każdego słowa: „farma” → „a”, „tapet” → „ta”, „skarpet” → „skar”. Nie zna też `%year`.

- Dotyczy tylko wzorców dopasowania; teksty dla aktorów i nagrania są poprawne.
- 333 wpisy w paczkach (294 tylko ze zwykłymi słowami, 39 mieszanych), w tym 123 nagrane.
- Zwykłe dialogi zwykle grają dzięki dopasowaniu przybliżonemu (sprawdzone: Shane 80.ogg, 115.ogg, Leah 291.ogg), ale krótkie kwestie mogą nie zagrać, a dymki nie mają dopasowania przybliżonego.
- Okienko sklepu i pytania Robin próbują obu wersji tekstu (`Class/ModEntry.MenuLines.cs`), więc działają mimo błędu.

Naprawa: `%(?:adj|noun|…|time|year)(?![A-Za-z])` (obowiązkowy `%`, zamiast `\b` warunek „bez łacińskiej litery dalej”, co działa też dla chińskiego i japońskiego) i przeliczenie samych wzorców w paczkach. Przy okazji ujednolicić spacje w miejscu usuniętego znacznika: przy odtwarzaniu po usunięciu nazwy farmy zostaje „'' ''”, a we wzorcu „''''”.

## 3. Zdanie przed pytaniem `$q` znika

Kwestia może zaczynać się zdaniem, po którym dopiero pada pytanie z odpowiedziami, np. Abigail `Sun2`: „Zastanawiam się, co by się stało gdybym spędziła całą noc na cmentarzu?#$q 17/18 Sun_old#@, Jak myślisz, co się stanie gdy umrzemy?#$r …”. Generator zaczyna od pytania i gubi pierwsze zdanie, więc w paczce go nie ma i w grze nie gra (sprawdzone 20 listopada). Przy okazji generator zapisuje odpowiedzi gracza („Nie mam pojęcia.”, „Idziemy do Nieba.”…) jako kwestie postaci.

Dotyczy 17 kwestii: Penny `Mon4`, Alex `Wed`, `summer_Wed`, `fall_Wed`, Haley `summer_Sat`, `fall_Sat`, Sam `Wed`, Maru `Sun`, `summer_Thu`, Leah `Mon`, Abigail `Sun2`, `summer_Sun`, `fall_Sun`, `winter_Sun`, Clint `Mon`, Sebastian `Fri2`, `fall_Fri`.

Naprawa: dopisać brakujące zdania jako nowe wpisy na końcu paczek (żeby nie przesuwać numeracji nagranych stron) i dodać je do list dla aktorów. Wpisy z odpowiedziami gracza zostawić albo oznaczyć, bo część może już być nagrana.

## Co zmieniono 20 listopada

- Generator: `RxStripTokens` wymaga `%` i zna `%year`; `RxLeadingNarr` nie zdejmuje `%` sprzed znacznika gry. Przeliczone 331 wzorce i 11 tekstów dla aktorów (z nich nagrane: Abigail 377, Leah 286, Shane 171 – aktor mógł przeczytać „kid1” dosłownie).
- Odtwarzanie: kwestie ze zmiennymi (imię gracza, `%farm`, `%book`, `{0}`…) dopasowuje wzorzec-regex zbudowany z tekstu dla aktora (`Class/ModEntry.TemplateMatch.cs`), w dialogach, wydarzeniach, dymkach, okienku sklepu i pytaniach.
- Dopasowanie przybliżone wyłączone domyślnie (`FuzzyMatching` w config.json); w trybie deweloperskim log `[V2-NOMATCH]` z najbliższym wpisem.
- Klucz tłumaczenia z gry (`Characters\Dialogue\…`) jest ujednolicany do zapisu paczek, więc ścieżka po kluczu działa; numer strony z gry jest brany tylko, gdy gra i paczka mają tyle samo stron.

Sprawdzone w grze: Shane „farma” i `%time`, Leah `%farm`, Elliott wszystkie 3 strony z `%book`, Abigail `%kid1 i %kid2`, Marnie `%pet`, Krobus 5 stron z wariantem płci, Abigail z imieniem gracza, odpowiedź na pytanie, dymek Gusa, okienko sklepu Pierre'a (wtorek), oferta Robin.
