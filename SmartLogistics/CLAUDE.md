# Smart Logistics Planner – pravila projekta

## Domena (ne mijenjati bez mog odobrenja)
- Pickup & Delivery VRP s kapacitetom i radnim danom.
- A: 3500 kg / 2 stroja; B: 10000 kg / 3 stroja. Vrijedi NAKON SVAKE radnje.
- Isti kamion preuzima i isporučuje; preuzimanje prije isporuke.
- Ruta počinje i završava u L0. Kamion smije ostati neiskorišten.
- Dionica = euklidska udaljenost, `Math.Round(x, 1, MidpointRounding.AwayFromZero)`,
  tek zatim zbrajanje.
- 60 km/h (1 km = 1 min); svaka radnja 20 min, i u bazi.
- Radni dan: zadano `WorkdayMode.Total` (vožnja + radnje <= 540 min);
  alternativa `WorkdayMode.DrivingOnly` (samo vožnja <= 540 min).

## Referentne vrijednosti (izračunate neovisno)
- Total: optimum 200.1 km (A 37.2, B 162.9)
- DrivingOnly: optimum 182.2 km
- Rezultat ispod ovih brojeva = greška u kodu.

## Tehnologija
.NET 10 (LTS), C#, record tipovi, xUnit, ASP.NET Core Razor Pages, Bootstrap 5
(dolazi s predloškom), bez vanjskih API-ja. Core ne ovisi o Web.

## UI pravila (zbog kasnije zamjene teme)
- Bez inline stilova (`style="..."`); sav CSS u `wwwroot/css/site.css`.
- Boje i razmaci kao CSS varijable u `:root` (npr. `--truck-a`, `--truck-b`,
  `--warning`); SVG karta koristi te varijable.
- Stranice prikazuju ViewModel tipove iz `Web/ViewModels`, ne domenske
  objekte izravno; formatiranje (km, kg, hh:mm) u ViewModelu.
- Ponovljivi dijelovi su partial viewovi u `Pages/Shared`.
- Bez JavaScript frameworka; JS samo ako je nužno, u `wwwroot/js`.
- Dizajn sustav je u `docs/ui-dizajn.md` i obavezan je za sve stranice: postojeće
  komponente (app shell, `_PageHeader`, panel, stat-tile, badge, callout, timeline),
  samo tokeni iz `:root`, ikone samo preko `Web/Ui/Icons.cs`. Nova komponenta ili
  token upisuje se i u `docs/ui-dizajn.md`. Nova stranica dobiva stavku u `NavigationMenu`.

## Pravila rada
- Radi samo fazu koju zadam i STANI na kraju s kratkim izvještajem.
- Nikad ne mijenjaj test da bi prošao: popravi kod ili me pitaj.
- Nikad ne kodiraj rezultate tvrdo.
- Prije "gotovo": `dotnet build` i `dotnet test`.
- Kod, komentari i README na engleskom; UI na hrvatskom.
- `double` usporedbe u testovima s tolerancijom.
- Na kraju svake faze ažuriraj UI (Web) tako da pokrenuta aplikacija prikazuje
  što više korisnih informacija o dosad napravljenom (npr. ulazni podaci,
  izvedene činjenice, pravila, rezultati, prekršaji, upozorenja). Podaci dolaze
  iz Core preko ViewModela, nikad tvrdo upisani u Web; vrijede UI pravila.
- Na kraju svake faze predloži commit poruku (conventional commits).
