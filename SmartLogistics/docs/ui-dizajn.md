# UI dizajn – dizajn sustav

Jedan vizualni jezik za cijelu aplikaciju. Svaka nova stranica slaže se od komponenti
opisanih ovdje; ako komponenta ne postoji, dodaje se u `site.css` i u ovaj dokument.

## Načela

- **Pregledno prije svega:** najvažnija brojka ili upozorenje vidljivi su bez skrolanja.
- **Identitet UpRenta:** papirnata pozadina, tinta (crna) i signalna žuta kao jedini naglasak
  (vidi "Tema UpRent"); boje kamiona i stanja koriste se samo kad nose značenje.
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
- Fontovi su **Barlow** (tekst; 400/500/600) i **Barlow Condensed** (naslovi, oznake, brojke, gumbi; 600/700),
  self-hosted kao woff2 u `wwwroot/fonts/` (`@font-face` na vrhu `site.css`, podskupovi latin i latin-ext zbog
  č/ć/đ/š/ž, licenca SIL OFL 1.1 u `wwwroot/fonts/OFL.txt`); layout preloada dva najčešća reza. Bez CDN-a; uvijek
  postoji sistemski fallback (`system-ui`, `Arial Narrow`).
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
| Brend (UpRent) | `--brand-yellow`, `--brand-yellow-deep`, `--brand-ink`, `--brand-ink-2`, `--brand-ink-3`, `--brand-paper`, `--brand-white`, `--brand-hazard` (+ `--hazard-band`), `--brand-logo-height`, `--brand-logo-height-sm` | Sirova paleta UpRenta; komponente uglavnom koriste semantičke tokene ispod |
| Neutralne | `--color-bg`, `--color-surface`, `--color-surface-muted`, `--color-border`, `--color-border-strong`, `--color-text`, `--color-text-muted`, `--color-text-subtle`, `--color-surface-sunken` | Pozadina, kartice, rubovi, tekst (i `--color-text-subtle` ima kontrast AA) |
| Naglasak | `--color-accent`, `--color-accent-strong`, `--color-accent-soft`, `--color-accent-ink`, `--color-on-accent`, `--color-link-underline`, `--color-focus`, `--color-focus-on-dark`, `--color-focus-ring` | Žuta je **ispuna** (gumb, aktivna stavka, marker), nikad tekst na svijetloj podlozi; fokus je tinta na svijetlom, žuta na tamnom |
| Tamne površine | `--surface-dark`, `--surface-dark-raised`, `--text-on-dark`, `--text-on-dark-muted`, `--text-on-dark-subtle`, `--border-on-dark` | Bočni izbornik, gornja traka, zaglavlje stranice, podnožje |
| Bočni izbornik | `--sidebar-bg*`, `--sidebar-text*` (`-muted`, `-hover`, `-active`, `-strong`), `--sidebar-border*`, `--sidebar-edge`, `--sidebar-width`, `--code-block-text` | Tamni izbornik i blokovi koda |
| Kamioni | `--truck-a`, `--truck-a-soft`, `--truck-b`, `--truck-b-soft` | Rute, legende, karta, oznake kamiona |
| Stanja | `--color-ok`, `--color-warning`, `--color-danger`, `--color-info` (+ `-soft`, `-text`; uspjeh, upozorenje i greška i `-border`) | Statusi, upozorenja, prekršaji |
| Karta | `--map-bg`, `--map-grid`, `--map-axis`, `--map-tick`, `--map-base`, `--map-base-border`, `--map-location`, `--map-location-border`, `--map-label`, `--map-label-halo`, `--map-any`, `--map-invalid` | SVG karta; većinom upućuju na tokene iznad, pa se karta mijenja na jednom mjestu |
| Radni dan | `--meter-track`, `--meter-fill` | `meter.sl-workday-meter` (tinta na toplom tragu umjesto zelene boje preglednika) |
| Razmaci | `--space-1` … `--space-7` | Svi paddingi i margine |
| Oblik | `--radius-sm`, `--radius`, `--radius-lg`, `--radius-xl`, `--radius-pill`, `--rule-thick`, `--callout-bar`, `--shadow-sm`, `--shadow`, `--shadow-lg`, `--shadow-lift` | Kartice, gumbi, oznake; `--shadow-lift` je žuta "polica" ispod kartice na hover |
| Tipografija | `--font-sans` (Barlow), `--font-display` (Barlow Condensed), `--font-mono`, `--text-xs` … `--text-3xl`, `--tracking-display`, `--tracking-caps`, `--tracking-wide` | Svi tekstovi |
| Raspored | `--content-max`, `--page-header-decor`, `--transition` | Širina sadržaja, ukras zaglavlja, prijelazi |

Preimenovano u fazi 6a: `--success*` → `--color-ok*`, `--warning*` → `--color-warning*`,
`--danger*` → `--color-danger*`, `--info*` → `--color-info*`.
Faza 6c: novi `--map-axis` (osi) i `--map-tick` (oznake km); `--map-grid-size` je uklonjen jer mrežu više ne crta
CSS pozadina nego SVG linije u km (`MapGeometry`).

## Tema UpRent

Cijelo sučelje nosi vizualni identitet UpRenta (najam građevinskih strojeva; statična kopija weba u `web/`,
izvor `web/assets/site.css`). Tema je samo re-skin: struktura stranica i komponente su iste, promijenjeni su
tokeni i izgled komponenti.

- **Boje:** papir `#f4f3ee` (pozadina), bijele kartice s toplim rubom `#dddbd2`, tinta `#111` za tamne površine i
  tekst, signalna žuta `#ffec00` kao ispuna. Žuti tekst samo na tamnoj podlozi (15,5 : 1); na svijetloj je žuta
  ispuna s tintom (gumb, aktivna stavka, ikona pokazatelja). Linkovi u sadržaju su tinta s okernom podcrtom
  (`--color-link-underline`), na hover žuta ispuna.
- **Tipografija:** naslovi, oznake polja, zaglavlja tablica, oznake (badge), gumbi i velike brojke u Barlow
  Condensed, velikim slovima i s razmakom slova; tekst u Barlowu.
- **Okvir:** tamni bočni izbornik sa žutim desnim rubom (`--sidebar-edge`), logo UpRenta (`wwwroot/img/uprent-logo.png`,
  žut na prozirnom – samo na tamnoj podlozi) i naziv aplikacije "Smart Logistics / Planer prijevoza" (`_AppBrand`).
  Naslovi skupina u stilu UpRent "eyebrow" (kratka žuta crta + kondenzirana velika slova). Aktivna stavka je žuta
  ispuna s tintom. Mobilna gornja traka je tamna sa žutim donjim rubom; offcanvas ima isti izbornik.
- **Zaglavlje stranice:** tamni panel (`--radius-xl`) sa žutim eyebrowom, naslovom velikim kondenziranim slovima i
  zarotiranim žutim kvadratom (kao blok kataloga na uprent.hr).
- **Podnožje:** tamna traka s UpRent "hazard" prugom (`--brand-hazard`) na vrhu.
- **Obavijesti:** traka lijevo u boji stanja; upozorenje ima hazard prugu (žuto-crne kose pruge) da se ne može
  previdjeti. Ikona obavijesti je u kvadratu (žuta/tinta za upozorenje, tinta/žuta za info).
- **Pokazatelji:** gornja crta u tonu pločice (inset sjena prati zaobljenje), ikona u kvadratu.
- **Gumbi:** "pill" s kondenziranim velikim slovima (kao `.btn` na uprent.hr); primarni žut, na hover tinta sa žutim tekstom.
- **Fokus:** tinta (3 px) na svijetlim površinama, žuta na tamnim; polja forme dobivaju još i žuti halo (`--color-focus-ring`).
- **Kamioni:** A čelično plava `#1f5f99`, B sigurnosno narančasta `#b5400c` – par razlučiv i za daltoniste, oba AA na
  bijelom i na svojoj mekoj podlozi (oznake). Žuta ostaje rezervirana za brend i bazu L0 na karti (žuti krug s
  rubom od tinte). Ostali kamioni na karti su grafit (`--map-any`).
- **Stanja:** uspjeh zelena, upozorenje oker/žuta (tekst `#4d3d00` na `#fff6bf`), greška crvena, info grafit.

Promjena tokena u odnosu na prethodnu (slate/indigo) temu:

| Token | Prije | Sada |
|-------|-------|------|
| `--color-bg` | `#f4f6fb` | `#f4f3ee` (`--brand-paper`) |
| `--color-border` / `-strong` | `#e3e8f0` / `#cbd5e1` | `#dddbd2` / `#c4c1b5` |
| `--color-text` / `-muted` / `-subtle` | `#0f172a` / `#64748b` / `#94a3b8` | `#1a1a1a` / `#5d5c57` / `#6f6d66` |
| `--color-accent` / `-strong` / `-soft` | `#4f46e5` / `#4338ca` / `#eef2ff` | `#ffec00` / `#e6d200` / `#fff8c4` |
| `--color-on-accent` | `#ffffff` | `#111111` |
| `--color-focus` | indigo 35 % | `#111111` (+ `--color-focus-on-dark` žuta) |
| `--sidebar-bg` | `#0f172a` | `#111111` |
| `--sidebar-bg-active` / `--sidebar-text-active` | bijela 10 % / `#ffffff` | `#ffec00` / `#111111` |
| `--sidebar-width` | 256px | 272px |
| `--truck-a` / `-soft` | `#0d9488` / `#ccfbf1` | `#1f5f99` / `#e2ecf6` |
| `--truck-b` / `-soft` | `#ea580c` / `#ffedd5` | `#b5400c` / `#fdebe0` |
| `--color-ok` / `-soft` / `-text` | `#16a34a` / `#dcfce7` / `#166534` | `#2e7d32` / `#e7f3e4` / `#1d5420` |
| `--color-warning` / `-soft` / `-text` / `-border` | `#d97706` / `#fef3c7` / `#92400e` / amber 25 % | `#8a6d00` / `#fff6bf` / `#4d3d00` / `#e6d200` |
| `--color-danger` / `-soft` / `-text` | `#dc2626` / `#fee2e2` / `#991b1b` | `#c62828` / `#fdecea` / `#8e1b1b` |
| `--color-info` / `-soft` / `-text` | `#2563eb` / `#dbeafe` / `#1e40af` | `#2a2a2a` / `#ecebe4` / `#2a2a2a` |
| `--radius-sm` / `--radius` | 6px / 10px | 8px / 12px |
| `--font-sans` | Segoe UI Variable / system-ui | Barlow (+ sistemski fallback) |
| `--map-base` / `--map-base-border` | indigo | `#ffec00` / `#111111` |
| `--map-any` | indigo | `#55534d` |

## Komponente

| Komponenta | Klase / partial | Kada |
|------------|-----------------|------|
| Okvir aplikacije | `_Layout` – regije: bočni izbornik `aside.sl-sidebar` s `header.sl-sidebar-header` i `sl-sidebar-footer` (+ `sl-sidebar-footer-title`), mobilni `offcanvas.sl-offcanvas` (`sl-offcanvas-title`), `header.sl-topbar` (gumb `sl-icon-button` + `_AppBrand`), `main.sl-content`, `footer.sl-footer` (+ `sl-footer-brand`, hazard pruga), sekcija `Scripts`; `_AppBrand` (`sl-brand`, `sl-brand-logo` = logo UpRenta, `sl-brand-text`, `sl-brand-name`, `sl-brand-sub`), `_AppNavigation` (`nav.sl-nav`) | Bočni izbornik na desktopu, offcanvas na mobitelu |
| Navigacija | `NavigationMenu.Build(NavigationContext)` u `ViewModels/LayoutViewModels.cs`; `sl-nav-section`, `sl-nav-section-title`, `sl-nav-link` (+ `-active`, `-disabled`, `-external`), `sl-nav-external-icon`, `sl-nav-badge` | Skupine "Planiranje", "Projekt" i "Tvrtka" (T41 a). Nova stranica dobiva stavku; nedovršene stavke imaju oznaku "uskoro"; `AlsoActiveFor` označava stavku aktivnom i na pripadnoj podstranici. `NavItemViewModel` ima ili `Page` (Razor stranica, `asp-page`) ili `Href` (statična datoteka / vanjska adresa, običan `href`; `OpensInNewTab` daje `target="_blank" rel="noopener"`, ikonu `external` i skriveni tekst "(otvara se u novoj kartici)"); bez oboje je stavka onemogućena |
| Vanjska stavka "UpRent web" | `LinkKind.UpRentWebsite` u `NavigationMenu`; adresa dolazi iz `NavigationContext.UpRentWebsiteHref` (`Services/UpRentWebsite.StartPage` = `/uprent/index.html`) | Statična kopija weba UpRenta (`web/`) servira se na `/uprent/` (`UseStaticFiles` + `PhysicalFileProvider`, samo `*.html` u korijenu i `assets/**`; `/uprent` preusmjerava na početnu). Mapa se traži kao `docs/` i `data/` (`ProjectPaths.FindDirectory`) i kopira se pri publishu. Ako mapa nedostaje, stavka je onemogućena s oznakom "nedostupno" (nema slomljenog linka) |
| Zaglavlje stranice | `_PageHeader` (`PageHeaderViewModel`): `sl-page-header`, `sl-page-eyebrow`, `sl-page-title`, `sl-page-subtitle` | Na vrhu **svake** stranice |
| Kartica (panel) | `sl-card`, `sl-card-header`, `sl-card-title`, `sl-card-action`, `sl-card-body`, `sl-card-lead` | Svaka cjelina sadržaja |
| Pokazatelj | `_StatTile` (`sl-stat-grid`, `sl-stat-tile` + `-accent/-ok/-warning/-neutral/-truck-a/-truck-b`) | Ključne brojke (km ukupno, po kamionu, broj naloga) |
| Oznaka | `sl-badge` + `sl-badge-ok/-warning/-danger/-neutral/-accent/-truck-a/-truck-b`; niz oznaka `sl-badge-row` | Status, kamion |
| Čip | `sl-chip-row`, `sl-chip`; red s meta podacima `sl-meta-row` | Meta podaci (datoteka, datum, brojevi) |
| Obavijest | `sl-callout` + `sl-callout-info/-warning/-danger/-ok`, `sl-callout-icon`, `sl-callout-list` | Upozorenja, neizvedivi nalozi, prekršaji |
| Vremenska linija | `_PhaseTimeline` (`sl-timeline`, `sl-timeline-item` + `-done/-review/-planned/-current`); `-current` = faza u tijeku (`PhaseStatus.InProgress`, oznaka `sl-badge-accent` "U tijeku", ikona `play`) | Napredak faza (`ViewModels/ProjectPhases.cs`; optimum u opisu faze 4 izračunat je u trenutku prikaza – `ComputedOptima`) |
| Testni scenariji | Stranica `/Scenariji` (`ScenarioLabPageViewModel` iz `ScenarioLabViewModelFactory`, podaci iz `Services/ScenarioLab`); GET `slucaj=osnova/voznja/k1/k2/k3/k4/kvar`, `kvar=preopterecenje/strojevi/redoslijed/kamioni/povratak/km/izostavljen/nestao/radni-dan`. Pregled: mreža `sl-lab-grid` s karticama `_LabCaseCard` (`LabCaseCardViewModel`: `a.sl-lab-case` + `sl-lab-case-active`, `sl-lab-case-head`, `-number`, `-title`, `-text`, `-foot`). Presuda `_LabVerdict` (`LabVerdictViewModel`): `sl-badge sl-badge-ok` "Očekivano ✓" ili `sl-badge-danger` "Neočekivano ✗". Naslovi `sl-lab-subtitle`, popis izmjena `sl-lab-list`, očekivanja `sl-lab-checks` > `sl-lab-check` (+ `sl-lab-check-failed`, `sl-lab-check-mark`), ishodi `sl-badge-row sl-lab-outcomes`, izbor kvara `sl-segmented` sa `sl-lab-defect-rules` i `sl-lab-defect-mark`; plan se prikazuje postojećim `_Warnings`, `_PlanSummary`, `_RouteMap`, `_TruckRouteTable` | Rubni slučajevi K1–K4 i namjerno pokvaren plan; nikad ne mijenja trenutni scenarij u spremištu |
| Kartica dokumenta | `_DocumentCard` (`sl-doc-grid`, `sl-doc-card`, `sl-doc-card-missing`) | Popis dokumenata |
| Sadržaj dokumenta | `_DocumentOutline` (`sl-doc-outline`, `sl-doc-outline-sub`) | Desni stupac dokumentacije |
| Segmentirana navigacija | `sl-segmented`, `sl-segmented-item` (+ `-active`) | Prebacivanje između srodnih prikaza (dokumenti) |
| Markdown | `sl-markdown`; `MarkdownRenderer` generira `sl-md-table`, `sl-md-code`, `sl-md-list`, `sl-md-nested`, `sl-md-quote` | Prikaz `docs/*.md` |
| Tablica podataka | `table sl-table` u kartici (bez `sl-card-body`), brojke `sl-num text-end`, sporedni tekst `sl-cell-muted`; široke tablice (ruta kamiona, usporedba algoritama) dodaju `sl-table-dense` (uži razmak između stupaca, vanjske ćelije poravnate s karticom) | Kamioni, strojevi, nalozi, lokacije, stopovi |
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

Testni scenariji (T69): `ScenarioLabPageViewModel`, `LabCaseCardViewModel`, `LabCaseDetailViewModel`,
`LabDefectOptionViewModel`, `LabCheckViewModel`, `LabOutcomeViewModel`, `LabFactViewModel`, `LabVerdictViewModel`
(namespace `SmartLogistics.Web.ViewModels`); Pregled: `ProjectPhases` + `ComputedOptima`.

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
