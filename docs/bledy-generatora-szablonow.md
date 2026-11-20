# Błędy generatora szablonów (`create_template`)

Znalezione 20 listopada 2026 podczas testów kwestii ze znacznikami (`%kid1`, `%farm`…). Oba błędy siedzą w kodzie generatora z oryginalnego frameworka jessevanga (`Class/DialogueParse/ModEntry.DialogueUtil.cs`), a nie w tłumaczeniu DNKSH. Nie są jeszcze naprawione.

## 1. Znacznik na początku kwestii traci `%`

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

## 2. Reguła usuwania znaczników wycina zwykłe słowa

`RxStripTokens` miał usuwać znaczniki (`%farm`, `%pet`…) ze wzorca dopasowania. Od commita jessevanga `f5b730e` (6 września 2025) `%` jest w nim opcjonalny i nie ma granicy słowa, więc reguła wycina te ciągi liter z każdego słowa: „farma” → „a”, „tapet” → „ta”, „skarpet” → „skar”. Nie zna też `%year`.

- Dotyczy tylko wzorców dopasowania; teksty dla aktorów i nagrania są poprawne.
- 333 wpisy w paczkach (294 tylko ze zwykłymi słowami, 39 mieszanych), w tym 123 nagrane.
- Zwykłe dialogi zwykle grają dzięki dopasowaniu przybliżonemu (sprawdzone: Shane 80.ogg, 115.ogg, Leah 291.ogg), ale krótkie kwestie mogą nie zagrać, a dymki nie mają dopasowania przybliżonego.
- Okienko sklepu i pytania Robin próbują obu wersji tekstu (`Class/ModEntry.MenuLines.cs`), więc działają mimo błędu.

Naprawa: `%(?:adj|noun|…|time|year)(?![A-Za-z])` (obowiązkowy `%`, zamiast `\b` warunek „bez łacińskiej litery dalej”, co działa też dla chińskiego i japońskiego) i przeliczenie samych wzorców w paczkach. Przy okazji ujednolicić spacje w miejscu usuniętego znacznika: przy odtwarzaniu po usunięciu nazwy farmy zostaje „'' ''”, a we wzorcu „''''”.
