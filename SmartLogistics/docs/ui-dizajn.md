# UI dizajn – dizajn sustav

Jedan vizualni jezik za cijelu aplikaciju. Svaka nova stranica slaže se od komponenti
opisanih ovdje; ako komponenta ne postoji, dodaje se u `site.css` i u ovaj dokument.

## Načela

- **Pregledno prije svega:** najvažnija brojka ili upozorenje vidljivi su bez skrolanja.
- **Mirna površina, jedan naglasak:** neutralna (slate) pozadina, indigo kao jedina
  naglasna boja; boje kamiona i stanja koriste se samo kad nose značenje.
- **Upozorenja se ne skrivaju:** neizvedivi nalozi i prekršaji uvijek su `sl-callout`
  na vrhu stranice, nikad samo u tablici.
- **Brojke su poravnate:** tablične brojke desno, `tabular-nums`.
- **Hrvatski u sučelju, engleski u kodu.**

## Tehnička pravila

- Sav CSS je u `wwwroot/css/site.css`; bez `style="..."`, bez CSS izolacije (`*.cshtml.css`).
- Boje, razmaci, radijusi, sjene i fontovi su **tokeni** u `:root`. Komponente koriste
  samo tokene. Nova boja ili razmak = novi token, nikad vrijednost izravno u komponenti.
- Ikone samo preko `Web/Ui/Icons.cs` (`@Icons.Get("naziv")`): inline SVG s klasom `sl-icon`, boja prati tekst.
  Bez icon fontova i CDN-ova; nova ikona = novi unos u rječnik.
- Bootstrap 5.3 služi za grid (`row`, `col-*`), offcanvas i pomoćne klase; izgled
  komponenti daju naše klase.
- Fontovi su sistemski (Segoe UI Variable / system-ui), bez preuzimanja.
- JS samo ako je nužno (trenutno samo Bootstrap bundle za mobilni izbornik); stranica dodaje
  svoje skripte u sekciju `Scripts` layouta. Forme (Nalozi, Plan prijevoza) rade bez JS-a: validacija je na
  serveru (DataAnnotations + `ModelState`), odabrana opcija radio-gumba ističe se CSS-om (`:has`).
- Tamna tema zasad ne postoji; dodaje se redefiniranjem tokena na jednom mjestu.

## Nazivi klasa

Sve vlastite klase imaju prefiks `sl-` (T41 b); Bootstrap klase (`row`, `col-*`, `table`, `offcanvas`,
`text-end`, `fw-semibold` …) ostaju kakve jesu. Shema (T45):

| Dio | Oblik | Primjer |
|-----|-------|---------|
| Blok (komponenta) | `sl-<blok>` | `sl-card`, `sl-badge`, `sl-callout`, `sl-map-route` |
| Element | `sl-<blok>-<element>` | `sl-card-header`, `sl-callout-icon`, `sl-stat-value` |
| Modifikator | uz blok, `sl-<blok>-<modifikator>` | `sl-badge sl-badge-warning`, `sl-callout sl-callout-ok`, `sl-nav-link sl-nav-link-active` |

Nema općih `is-*` klasa; stanje je uvijek modifikator svog bloka. Modifikator uspjeha zove se `-ok`
(kao token `--color-ok`). Boje kamiona su sufiksi `truck-a`, `truck-b` i `any` (ostali kamioni), npr.
`sl-map-route-truck-a`, `sl-stat-tile-truck-b`. ID-ovi SVG markera (`arrow-truck-a` …) nisu klase i nemaju
prefiks (`arrow-*` za karte ruta, `order-arrow-*` za kartu naloga). Neke klase su samo semantičke kuke bez
vlastitog pravila (`sl-nav`, `sl-truck-route`, `sl-form`, `sl-plan-form`, `sl-map-grid`, `sl-map-axes`,
`sl-timeline-body`, `sl-md-list`, `sl-map-arrowhead`).

## Tokeni

| Skupina | Tokeni | Namjena |
|---------|--------|---------|
| Neutralne | `--color-bg`, `--color-surface`, `--color-surface-muted`, `--color-border`, `--color-border-strong`, `--color-text`, `--color-text-muted`, `--color-text-subtle` | Pozadina, kartice, rubovi, tekst |
| Naglasak | `--color-accent`, `--color-accent-strong`, `--color-accent-soft`, `--color-focus`, `--color-on-accent` | Aktivna navigacija, linkovi, fokus, tekst na naglasnoj boji |
| Bočni izbornik | `--sidebar-bg*`, `--sidebar-text*`, `--sidebar-border*`, `--sidebar-width`, `--code-block-text` | Tamni izbornik i blokovi koda |
| Kamioni | `--truck-a`, `--truck-a-soft`, `--truck-b`, `--truck-b-soft` | Rute, legende, karta, oznake kamiona |
| Stanja | `--color-ok`, `--color-warning`, `--color-danger`, `--color-info` (+ `-soft`, `-text`; upozorenje i greška i `-border`) | Statusi, upozorenja, prekršaji |
| Karta | `--map-bg`, `--map-grid`, `--map-axis`, `--map-tick`, `--map-base`, `--map-base-border`, `--map-location`, `--map-location-border`, `--map-label`, `--map-label-halo`, `--map-any`, `--map-invalid` | SVG karta; većinom upućuju na tokene iznad, pa se karta mijenja na jednom mjestu |
| Razmaci | `--space-1` … `--space-7` | Svi paddingi i margine |
| Oblik | `--radius-sm`, `--radius`, `--radius-lg`, `--radius-pill`, `--shadow-sm`, `--shadow`, `--shadow-lg` | Kartice, gumbi, oznake |
| Tipografija | `--font-sans`, `--font-mono`, `--text-xs` … `--text-2xl` | Svi tekstovi |
| Raspored | `--content-max`, `--transition` | Širina sadržaja, prijelazi |

Preimenovano u fazi 6a: `--success*` → `--color-ok*`, `--warning*` → `--color-warning*`,
`--danger*` → `--color-danger*`, `--info*` → `--color-info*`.
Faza 6c: novi `--map-axis` (osi) i `--map-tick` (oznake km); `--map-grid-size` je uklonjen jer mrežu više ne crta
CSS pozadina nego SVG linije u km (`MapGeometry`).

## Komponente

| Komponenta | Klase / partial | Kada |
|------------|-----------------|------|
| Okvir aplikacije | `_Layout` – regije: bočni izbornik `aside.sl-sidebar` s `header.sl-sidebar-header`, mobilni `offcanvas.sl-offcanvas`, `header.sl-topbar`, `main.sl-content`, `footer.sl-footer`, sekcija `Scripts`; `_AppBrand` (`sl-brand*`), `_AppNavigation` (`nav.sl-nav`) | Bočni izbornik na desktopu, offcanvas na mobitelu |
| Navigacija | `NavigationMenu` u `ViewModels/LayoutViewModels.cs`; `sl-nav-section`, `sl-nav-link` (+ `-active`, `-disabled`), `sl-nav-badge` | Skupine "Planiranje" i "Projekt" (T41 a). Nova stranica dobiva stavku; nedovršene stavke imaju oznaku "uskoro"; `AlsoActiveFor` označava stavku aktivnom i na pripadnoj podstranici |
| Zaglavlje stranice | `_PageHeader` (`PageHeaderViewModel`): `sl-page-header`, `sl-page-eyebrow`, `sl-page-title`, `sl-page-subtitle` | Na vrhu **svake** stranice |
| Kartica (panel) | `sl-card`, `sl-card-header`, `sl-card-title`, `sl-card-action`, `sl-card-body`, `sl-card-lead` | Svaka cjelina sadržaja |
| Pokazatelj | `_StatTile` (`sl-stat-grid`, `sl-stat-tile` + `-accent/-ok/-warning/-neutral/-truck-a/-truck-b`) | Ključne brojke (km ukupno, po kamionu, broj naloga) |
| Oznaka | `sl-badge` + `sl-badge-ok/-warning/-danger/-neutral/-accent/-truck-a/-truck-b`; niz oznaka `sl-badge-row` | Status, kamion |
| Čip | `sl-chip-row`, `sl-chip`; red s meta podacima `sl-meta-row` | Meta podaci (datoteka, datum, brojevi) |
| Obavijest | `sl-callout` + `sl-callout-info/-warning/-danger/-ok`, `sl-callout-icon`, `sl-callout-list` | Upozorenja, neizvedivi nalozi, prekršaji |
| Vremenska linija | `_PhaseTimeline` (`sl-timeline`, `sl-timeline-item` + `-done/-review/-planned`) | Napredak faza |
| Kartica dokumenta | `_DocumentCard` (`sl-doc-grid`, `sl-doc-card`, `sl-doc-card-missing`) | Popis dokumenata |
| Sadržaj dokumenta | `_DocumentOutline` (`sl-doc-outline`, `sl-doc-outline-sub`) | Desni stupac dokumentacije |
| Segmentirana navigacija | `sl-segmented`, `sl-segmented-item` (+ `-active`) | Prebacivanje između srodnih prikaza (dokumenti) |
| Markdown | `sl-markdown`; `MarkdownRenderer` generira `sl-md-table`, `sl-md-code`, `sl-md-list`, `sl-md-nested`, `sl-md-quote` | Prikaz `docs/*.md` |
| Tablica podataka | `table sl-table` u kartici (bez `sl-card-body`), brojke `sl-num text-end`, sporedni tekst `sl-cell-muted` | Kamioni, strojevi, nalozi, lokacije, stopovi |
| Tablice ulaznih podataka | `_TrucksTable` (`TruckRowViewModel`), `_MachinesTable` (`MachineRowViewModel`), `_LocationsTable` (`LocationRowViewModel`), `_OrdersTable` (`OrdersTableViewModel`; s `ShowActions` gumbi Uredi / Obriši) | Samo tablica (`table-responsive`); naslov kartice daje stranica. Ulazni podaci, Nalozi, potvrda brisanja (jedan redak) |
| Status scenarija | `_ScenarioStatus` (`ScenarioStatusViewModel`): `sl-callout` + `sl-callout-with-action`, `sl-callout-body`, `sl-callout-detail`, gumb iz `_ResetScenarioForm` | Je li scenarij izmijenjen u odnosu na `data/scenario.json` (broj izmjena) + "Vrati izvorne podatke" |
| Vraćanje izvornih podataka | `_ResetScenarioForm` (model = putanja stranice): POST `?handler=Reset` s antiforgery tokenom, `sl-inline-form` | Ulazni podaci i Nalozi; stranica preusmjerava natrag (PRG) |
| Poruka o uspjehu | `_FlashMessage` (TempData ključ `FlashMessage.Key`): `sl-callout sl-callout-ok` | Odmah ispod `_PageHeader` na stranicama na koje vodi Post-Redirect-Get (Ulazni podaci, Nalozi) |
| Greške forme | `_FormErrors` (lista poruka iz `ModelState[""]`, `ModelStateExtensions.FormErrors`): `sl-callout sl-callout-danger` | Neuspjeh spremišta (`ScenarioStoreResult`) i druge greške cijele forme |
| Gumb | `sl-button` + `sl-button-primary/-secondary/-danger`, manji `sl-button-sm`; niz `sl-button-row` | Spremanje, odustajanje, Uredi / Obriši, Izračunaj |
| Forma | `sl-form`, `sl-form-grid` (+ `sl-field-wide`), `sl-form-row`, `sl-field`, `sl-field-label`, `sl-input` (+ `sl-input-number`, `sl-input-invalid`; tag helper dodaje `input-validation-error`), `sl-field-hint`, `sl-field-error`, `sl-form-actions`, `sl-form-footer`, `sl-form-rules` | Obrazac naloga (`_OrderForm`), forma plana |
| Izbor opcije | `sl-choice-group`, `sl-choice` (radio + `sl-choice-input`, `sl-choice-text`, `sl-choice-hint`; odabrana opcija istaknuta CSS-om) | Način radnog dana |
| Obrazac naloga | `_OrderForm` (`OrderFormPageModel` → `OrderInputModel` + `OrderFormViewModel`): select stroja (s opcijom "Novi stroj…"), masa novog stroja, select preuzimanja i isporuke | `/Orders/Create`, `/Orders/Edit/{id}` |
| Forma plana | `_PlanForm` (`Transport.PlanFormViewModel`): GET forma `sl-card sl-form sl-plan-form`, parametri `algoritam` (`exact` zadano, `ortools`), `nacin` i `maxMinuta` | Vrh stranice `/Plan` (alatna traka ispod zaglavlja) |
| Izbor algoritma | `sl-choice-group` s `AlgorithmOptionViewModel` (iz `PlannerCatalog`); algoritam bez registriranog planera ima `sl-choice-disabled` (isprekidani rub, `disabled` radio, napomena "nije dostupan u ovoj verziji") | Faza 7: `/Plan`, prvo polje forme |
| Sažetak plana | `_PlanSummary` (`Transport.PlanSummaryViewModel`): `sl-stat-grid`, oznaka algoritma `sl-algorithm-label`, oznaka jamstva (R38) `sl-badge sl-badge-ok sl-badge-lg` "Egzaktni optimum" ili `sl-badge sl-badge-warning sl-badge-lg` "Optimum nije zajamčen (heuristika)", oznaka validatora `sl-badge sl-badge-ok sl-badge-lg` (ili `-danger`), čipovi s činjenicama (i statistika OR-Toolsa), kod prekršaja `sl-card sl-card-danger` s `_ViolationTable` | `/Plan` |
| Usporedba algoritama | `_PlanComparison` (`Transport.PlanComparisonViewModel`): `sl-card sl-plan-comparison` s `sl-card-lead`, `sl-callout-warning` kad algoritmi nisu isplanirali jednak broj naloga, `table sl-table` (algoritam, jamstvo, km, razlika prema egzaktnom, nalozi, vrijeme izračuna, validator); redak prikazanog plana `sl-plan-comparison-current` (rub u naglasnoj boji); ostali reci imaju gumb `sl-button-sm` "Prikaži" (iz međuspremnika) ili "Izračunaj" | Faza 7: `/Plan`, kad su registrirana barem dva planera; vrijednosti samo iz međuspremnika `PlanService` (stranica ne pokreće drugi planer) |
| Neizvedivi nalozi | `_Warnings` (`Transport.PlanWarningsViewModel`): `sl-callout sl-callout-warning sl-callout-block` s tablicom `sl-callout-table` (K-kod kao oznaka, razlog, poruka planera); upozorenja o ulazu kao `sl-callout-list` | `/Plan`, odmah ispod statusa |
| Ruta kamiona | `_TruckRouteTable` (`Transport.TruckRouteViewModel`): `sl-truck-route`, `sl-route-table` (polazak/povratak `sl-route-table-base-row`), ispod tablice `sl-truck-route-summary` sa `sl-kv-list` (+ `sl-kv-value-danger`) i `meter.sl-workday-meter` | Po kamionu na `/Plan`; neiskorišten kamion: "Kamion nije korišten u optimalnom planu." |
| Karta ruta | `_RouteMap` (`Transport.MapViewModel`): `sl-map-route-canvas`, okvir `_MapFrame`, `sl-map-route sl-map-route-truck-a/-truck-b/-any`, markeri `arrow-*`, `sl-map-step-*`, legenda `sl-map-legend`, `sl-map-swatch` + `-base/-any/-truck-a/-truck-b/-none` | `/Plan` |
| Okvir karte | `_MapFrame` (`MapFrameViewModel` iz `MapGeometry.Frame`): mreža `sl-map-grid-line`, osi kroz ishodište `sl-map-axis`, oznake km `sl-map-tick sl-map-tick-x/-y`, nazivi osi `sl-map-axis-label` (+ `-x/-y`) | Obje karte; korak mreže 1/2/5 × 10ⁿ km (najviše 10 linija) |
| Upozorenja o ulazu | `sl-callout sl-callout-warning` sa `sl-callout-list` | Upozorenja iz `Feasibility` (npr. duplikat kamiona) |
| Status plana | `PlanStatusViewModel` (`PlanViewModel.Status`): `sl-callout` uspjeh/upozorenje/greška | Vrh rezultata na `/Plan` |
| Brojevi koraka na karti | `sl-map-step sl-map-step-truck-a/-truck-b/-any` (SVG `text` s obrubom `--map-label-halo`) | Faza 6c: `Transport.MapViewModel.Steps`, jedna oznaka po (lokacija, kamion), npr. "1, 4" |
| Tablica prekršaja | `_ViolationTable` (`ViolationRowViewModel`): pravilo kao `sl-badge sl-badge-danger`, kamion, korak (1-based), poruka | Rezultat validatora uz svaki plan |
| Oznake kamiona | `_TruckBadges` (`TruckBadgeViewModel`), `sl-badge sl-badge-truck-a/-b` | Gdje god se spominje kamion |
| Par lokacija | `sl-route-pair` (A → B s ikonom) | Preuzimanje → isporuka, dionica |
| Matrica udaljenosti | `_DistanceMatrix` (`sl-matrix`, `sl-matrix-corner`, dijagonala `sl-matrix-diagonal`) | Udaljenosti između lokacija |
| Karta naloga | `_LocationMap` (`LocationMapViewModel`): `sl-map`, `sl-map-canvas`, okvir `_MapFrame`, `sl-map-location` (+ `-base`), `sl-map-order` + `-any/-truck-a/-truck-b/-none`, markeri `order-arrow-*`, `sl-map-arrowhead-*` | Lokacije i nalozi na Ulaznim podacima. Sve koordinate računa `MapGeometry` (y = −Y), boje dolaze iz tokena. Veličine unutar SVG-a (debljina linije, font) su u km jedinicama karte, ne u tokenima razmaka. |
| Pravilo u tablici | `sl-rule-text` | Tekst pravila (širi stupac) |
| Razmak | `sl-section-gap` | Razmak ispod kartice |
| Poveznica-čip | `a.sl-chip.sl-chip-link` | Kratka poveznica u retku čipova (npr. "Uredi naloge" na planu) |

## ViewModeli plana prijevoza (faze 6a i 6c)

U `ViewModels/Transport/` (namespace `SmartLogistics.Web.ViewModels.Transport`, T42 a):
`PlanViewModel`, `TruckRouteViewModel`, `StopRowViewModel`, `InfeasibleOrderViewModel`, `MapViewModel`
(+ `PlanStatusViewModel`, `MapSegmentViewModel`, `MapStepViewModel`, `StopRowKind`). Gradi ih samo `PlanMapper`
(čiste funkcije, T46); boju kamiona daje njegov položaj u scenariju (prvi `truck-a`, drugi `truck-b`, ostali
`any`) – isto pravilo koriste i Ulazni podaci. Stranica `/Plan` dodaje `PlanPageViewModel`, `PlanFormViewModel`,
`WorkdayModeOptionViewModel`, `PlanSummaryViewModel`, `PlanWarningsViewModel` (`PlanPageViewModelFactory`,
parametri forme u `PlanForm`). Faza 7: `AlgorithmOptionViewModel`, `PlanComparisonViewModel`, `PlanComparisonRowViewModel`
i ulaz `PlanComparisonEntry`; popis algoritama (ključ, hrvatski naziv, opis, egzaktan) je `Services/PlannerCatalog`,
a planeri su keyed servisi `IRoutePlanner` registrirani u `Program.cs`. Jamstvo optimuma dolazi od samog planera
(`IPlannerCapabilities.GuaranteesOptimum`; planer bez sučelja smatra se heuristikom).

Geometrija obje karte je na jednom mjestu, `ViewModels/MapGeometry.cs` (`MapGeometry.Project` (X, Y) → (X, −Y),
`Frame`, `Locations`, `Shorten`, `GridStep`), sa zajedničkim tipovima `MapFrameViewModel`, `MapLineViewModel`,
`MapTextViewModel`, `MapLocationViewModel`, `MapLegendEntryViewModel` (namespace `SmartLogistics.Web.ViewModels`).
Nakon brisanja starih tipova svako ime ViewModela postoji samo jednom (T50).

## Predložak stranice

1. `_PageHeader` (eyebrow, naslov, jedna rečenica opisa). Ako stranica ima formu koja određuje cijeli sadržaj
   (`/Plan`), ona je odmah ispod zaglavlja, kao alatna traka (T49).
2. `_FlashMessage` / `_FormErrors` i `sl-callout` za upozorenja (ako postoje).
3. `sl-stat-grid` s ključnim brojkama.
4. Kartice sa sadržajem u Bootstrap gridu (`row g-4`, `col-xl-*`).

## Formatiranje

Sve vrijednosti formatiraju se u ViewModelu (`ViewModels/Format.cs`), ne u `.cshtml`. Tekst je hrvatski
(decimalni zarez, točka za tisućice), SVG atributi u invarijantnoj kulturi (`Format.Svg`).

| Vrijednost | Funkcija | Primjer |
|------------|----------|---------|
| km | `Format.Km` (1 decimala) | 162.9 → "162,9 km" |
| razlika km | `Format.SignedKm` (1 decimala, predznak; bez predznaka za 0) | 3.44 → "+3,4 km", −1.2 → "−1,2 km" |
| kg | `Format.Kg` (točka tisućica) | 10000 → "10.000 kg" |
| trajanje | `Format.Clock` (cijele minute, AwayFromZero, sati dvoznamenkasti) | 522.9 → "08:43" |
| udio cijelih brojeva | `Format.Percent(long, long)` (točno, AwayFromZero; "–" ako je nazivnik ≤ 0) | 9900 / 10000 → "99 %" |
| udio mjerenih vrijednosti | `Format.Percent(double, double)` (u `decimal`, AwayFromZero) | 522.9 / 540 → "97 %" |
| množina | `Format.Plural` | 3 → "naloga" |
