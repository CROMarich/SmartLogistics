# UI dizajn – dizajn sustav

Jedan vizualni jezik za cijelu aplikaciju. Svaka nova stranica slaže se od komponenti
opisanih ovdje; ako komponenta ne postoji, dodaje se u `site.css` i u ovaj dokument.

## Načela

- **Pregledno prije svega:** najvažnija brojka ili upozorenje vidljivi su bez skrolanja.
- **Mirna površina, jedan naglasak:** neutralna (slate) pozadina, indigo kao jedina
  naglasna boja; boje kamiona i stanja koriste se samo kad nose značenje.
- **Upozorenja se ne skrivaju:** neizvedivi nalozi i prekršaji uvijek su `callout`
  na vrhu stranice, nikad samo u tablici.
- **Brojke su poravnate:** tablične brojke desno, `tabular-nums`.
- **Hrvatski u sučelju, engleski u kodu.**

## Tehnička pravila

- Sav CSS je u `wwwroot/css/site.css`; bez `style="..."`, bez CSS izolacije (`*.cshtml.css`).
- Boje, razmaci, radijusi, sjene i fontovi su **tokeni** u `:root`. Komponente koriste
  samo tokene. Nova boja ili razmak = novi token, nikad vrijednost izravno u komponenti.
- Ikone samo preko `Web/Ui/Icons.cs` (`@Icons.Get("naziv")`): inline SVG, boja prati tekst.
  Bez icon fontova i CDN-ova; nova ikona = novi unos u rječnik.
- Bootstrap 5.3 služi za grid (`row`, `col-*`), offcanvas i pomoćne klase; izgled
  komponenti daju naše klase.
- Fontovi su sistemski (Segoe UI Variable / system-ui), bez preuzimanja.
- JS samo ako je nužno (trenutno samo Bootstrap bundle za mobilni izbornik).
- Tamna tema zasad ne postoji; dodaje se redefiniranjem tokena na jednom mjestu.

## Tokeni

| Skupina | Tokeni | Namjena |
|---------|--------|---------|
| Neutralne | `--color-bg`, `--color-surface`, `--color-surface-muted`, `--color-border`, `--color-text`, `--color-text-muted`, `--color-text-subtle` | Pozadina, kartice, rubovi, tekst |
| Naglasak | `--color-accent`, `--color-accent-strong`, `--color-accent-soft`, `--color-focus`, `--color-on-accent` | Aktivna navigacija, linkovi, fokus, tekst na naglasnoj boji |
| Bočni izbornik | `--sidebar-bg`, `--sidebar-text*`, `--sidebar-border*`, `--sidebar-width`, `--code-block-text` | Tamni izbornik i blokovi koda |
| Kamioni | `--truck-a`, `--truck-a-soft`, `--truck-b`, `--truck-b-soft` | Rute, legende, karta, oznake kamiona |
| Stanja | `--success`, `--warning`, `--danger`, `--info` (+ `-soft`, `-text`, `-border`) | Statusi, upozorenja, prekršaji |
| Razmaci | `--space-1` … `--space-7` | Svi paddingi i margine |
| Oblik | `--radius-sm`, `--radius`, `--radius-lg`, `--radius-pill`, `--shadow-sm`, `--shadow`, `--shadow-lg` | Kartice, gumbi, oznake |
| Tipografija | `--font-sans`, `--font-mono`, `--text-xs` … `--text-2xl` | Svi tekstovi |

## Komponente

| Komponenta | Klase / partial | Kada |
|------------|-----------------|------|
| Okvir aplikacije | `_Layout`, `_AppBrand`, `_AppNavigation` | Bočni izbornik na desktopu, offcanvas na mobitelu |
| Navigacija | `NavigationMenu` u `ViewModels/LayoutViewModels.cs` | Nova stranica dobiva stavku; nedovršene stavke imaju oznaku "uskoro" |
| Zaglavlje stranice | `_PageHeader` (`PageHeaderViewModel`) | Na vrhu **svake** stranice |
| Panel | `.panel`, `.panel-header`, `.panel-title`, `.panel-body` | Svaka cjelina sadržaja |
| Pokazatelj | `_StatTile` (`.stat-grid`, `tone-*`) | Ključne brojke (km ukupno, po kamionu, broj naloga) |
| Oznaka | `.badge-soft` + `is-success/warning/danger/neutral/accent/truck-a/truck-b` | Status, kamion |
| Čip | `.chip-row`, `.chip` | Meta podaci (datoteka, datum, brojevi) |
| Obavijest | `.callout` + `is-info/warning/danger/success` | Upozorenja, neizvedivi nalozi, prekršaji |
| Vremenska linija | `_PhaseTimeline` (`.timeline`) | Napredak faza; kasnije redoslijed radnji kamiona |
| Kartica dokumenta | `_DocumentCard` (`.doc-grid`) | Popis dokumenata |
| Segmentirana navigacija | `.segmented`, `.segmented-item` | Prebacivanje između srodnih prikaza (dokumenti, kamioni, način radnog dana) |
| Markdown | `.markdown-body` | Prikaz `docs/*.md` |
| Tablica podataka | `.table.data-table` u panelu (bez `panel-body`), brojke `.num.text-end`, sporedni tekst `.cell-muted` | Kamioni, strojevi, nalozi, lokacije, stopovi |
| Prekidači scenarija i načina | `_ModeSwitch` (`PlanPageViewModel`): `.switch-group` s `.switch-label` i `.segmented`; parametri `?scenarij=` i `?nacin=voznja`, svaka poveznica zadržava drugi izbor; opis primjera `.scenario-description` | Stranice Plan ruta i Karta |
| Upozorenja o ulazu | `callout is-warning` s `.callout-list` | Upozorenja iz `Feasibility` (npr. duplikat kamiona) |
| Status plana | `_PlanStatus` (prekidač + čipovi s činjenicama + `callout` uspjeh/upozorenje/greška) | Vrh stranica Plan ruta i Karta |
| Plan kamiona | `_TruckPlan` (`TruckPlanViewModel`): zaglavlje s oznakom kamiona, `.kv-list` (vožnja/radnje/ukupno), `meter.workday-meter` (radni dan), `.route-table` s retcima polazak/povratak `.is-base-row` | Ruta po kamionu |
| Rute na karti | `.map-route is-truck-a/is-truck-b` u `_LocationMap`; legenda dolazi iz `MapViewModel.Legend` | Karta ruta; `.map-compact` (u stranici) i `.map-large` (zasebna karta) |
| Tablica prekršaja | `_ViolationTable` (`ViolationRowViewModel`): pravilo kao `badge-soft is-danger`, kamion, korak (1-based), poruka | Rezultat validatora; kasnije uz svaki plan |
| Uvodni tekst panela | `.panel-lead` | Jedna-dvije rečenice objašnjenja na vrhu panela |
| Oznake kamiona | `_TruckBadges` (`TruckBadgeViewModel`), `badge-soft is-truck-a/b` | Gdje god se spominje kamion |
| Par lokacija | `.route-pair` (A → B s ikonom) | Preuzimanje → isporuka, dionica |
| Matrica udaljenosti | `_DistanceMatrix` (`.matrix-table`, dijagonala `.is-diagonal`) | Udaljenosti između lokacija |
| Karta | `_LocationMap` (`MapViewModel`), `.map-canvas`, `.map-location`, `.map-order is-any/is-truck-a/is-truck-b/is-none`, legenda `.map-legend` | Lokacije i nalozi; kasnije rute kamiona. Koordinate računa ViewModel (y je okrenut), boje dolaze iz tokena. Veličine unutar SVG-a (debljina linije, font) su u km jedinicama karte, ne u tokenima razmaka. |

## Predložak stranice

1. `_PageHeader` (eyebrow, naslov, jedna rečenica opisa).
2. `callout` za upozorenja (ako postoje).
3. `stat-grid` s ključnim brojkama.
4. Paneli sa sadržajem u Bootstrap gridu (`row g-4`, `col-xl-*`).

## Formatiranje

Sve vrijednosti formatiraju se u ViewModelu (`ViewModels/Format.cs`), ne u `.cshtml`:
množina na hrvatskom (`Format.Plural`), a u sljedećim fazama km (1 decimala), kg i hh:mm.
