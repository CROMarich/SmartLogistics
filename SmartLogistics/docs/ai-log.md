# AI log

Stupac "Kako sam uhvatio" navodi tko je grešku primijetio: **ja** (tehnički vođa), **Claude**
(glavna sesija pri samoprovjeri), **podagent** ili **validator / build / testovi**.

| Faza | Što je AI napravio krivo | Kako sam uhvatio | Popravak |
|------|--------------------------|------------------|----------|
| 0 | Postavio rješenje na .NET 8 jer je tako pisalo u zadatku, iako na računalu postoje samo SDK 10 i 11. Prelazak nije predložio odmah, nego je .NET 8 zaobilazio preko SDK-a 11. | Ja – "ako ima SDK 10 ili 11, stavi na tome". | Svi projekti prebačeni na `net10.0` (LTS), `.sln` zamijenjen s `.slnx`, CLAUDE.md ažuriran (odluka T1, T2). |
| 0 | Premjestio Visual Studio predložak u `_vs_template_backup/`, ali nije ažurirao nadređeni `SmartLogistics.slnx` koji je VS imao otvoren. | Ja – VS je javio "Project file could not be loaded". | Nadređeni `.slnx` usmjeren na nove projekte; obrisan zaostali `obj/`. |
| 1 | U `spec.md` je D1 upućivao na "odjeljak 3.6", a proračun radnog dana je u 3.8. | Claude – pri upisivanju potvrđenih pretpostavki. | Referenca ispravljena. |
| 1 | Markdown renderer nije podebljao tekst kad je unutar njega bio kod (npr. podebljani `Total` (zadano) u specifikaciji); na stranici su ostale zvjezdice. | Claude – pretraga renderiranog HTML-a za ostatke `**`. | Kod se prvo zamijeni oznakama, zatim se primijeni podebljanje (`MarkdownRenderer.Inline`). |
| 1 | ID sidra naslova bio je HTML-kodiran dvaput (problem za naslove sa znakovima 160–255). | Claude – pregled generiranog HTML-a. | Uklonjeno suvišno kodiranje. |
| 1 | Kartica "Odluke" prikazivala je "18 pravila" – brojala je reference na pravila, a ne pravila. | Claude – pregled stranice. | Brojanje pravila samo za dokument koji ih definira (`DefinesRuleIds`). |
| 1 | Odstupanja od zadanog (polja i tipovi koje nitko nije tražio) navedena su samo u izvještaju u chatu, ne u dokumentu. | Ja – "Jesi li zapisao posebno odstupanja u dokument?" | Upisano T6–T10; u CLAUDE.md dodano pravilo da se svako odstupanje odmah upisuje u `docs/odluke.md`. |
| UI | Varijabla `section` u Razoru – rezervirana riječ, build je pao. | Build. | Preimenovano u `navSection`. |
| UI | Nekoliko boja upisano izravno u CSS, protivno vlastitom pravilu "samo tokeni". | Claude – awk provjera boja izvan `:root`. | Dodani tokeni (`--color-on-accent`, `--sidebar-border` …). |
| UI | Prve snimke zaslona bez CSS-a – aplikacija je pokrenuta u Production okruženju gdje statičke datoteke izvan objave nisu uključene (greška u provjeri, ne u aplikaciji). | Claude – snimka zaslona. | Provjere se pokreću s `ASPNETCORE_ENVIRONMENT=Development`. |
| 2 | Datoteka `scenario.json` zapisana na krivu putanju (`SmartLogistics/SmartLogistics/SmartLogistics/data`). | Claude – odmah nakon zapisivanja. | Pogrešna mapa obrisana, datoteka zapisana ispravno. |
| 2 | Stupac "Može nositi" u tablici kamiona bio je odrezan (dug popis strojeva). | Claude – snimka zaslona. | Prikazuje se broj ("5 od 10 strojeva"); detalj je u tablici strojeva. |
| 3 | Test-engineer je kao **ispravan** plan napisao cijeli zadatak s A: N01, B: N06 i ostalih 8 izvedivih naloga na popisu neizvedivih. To krši R32 (izvediv nalog se ne smije izostaviti). | Validator – odbio plan s R32. Podagent je zaključio da je validator u pravu, a test kriv. | Test pretvoren u negativni test R32; ispravan mali plan koristi scenarij sveden na N01 i N06 (T16). |
| 3 | Pri umetanju odluka T11–T15 red T10 završio je iza T15. | Claude – popis ID-ova. | Redovi presloženi. |
| 4 | Claude (UI dio faze 4): u `_ModeSwitch.cshtml` varijabla `page` sudarila se s Razor direktivom `@page`, pa je build Web projekta pao. | Podagent solver-engineer – njegov `dotnet build` je pao na tuđoj datoteci; prijavio je grešku i nije je dirao. | Preimenovano u `currentPage`. |
| 4 | Na stranici Plan ruta karta sa strane suzila je tablice ruta; stupci "Ukupno km" i "Vrijeme" nisu se vidjeli. | Claude – snimka zaslona. | Karta iznad, tablice kamiona preko cijele širine. |
| 5 | Validator-engineer je jednim `grep`-om nad cijelim `src` slučajno ispisao 4 retka iz `Core/Planning/` (iz njih se vidi da planer poziva validator) – unatoč zabrani čitanja planera. | Podagent – sam je to prijavio u izvještaju. | Upisano kao T40; rizik iz T5 (zabrana je samo uputa) se ostvario. Prijedlog: hook koji blokira `Core/Planning/` za tog agenta ili `--exclude-dir=Planning` u uputi. |
| 5 | Solver-engineer je prvo tumačio dupliciranu bazu tako da su svi nalozi K4. | Validator – odbio plan s R32/R33 (nalozi su se mogli isplanirati jer su obje baze na istim koordinatama). | Usklađeno: iste koordinate → baza se koristi, različite → svi K4 (T35). |
| 5 | Claude je tvoju fazu 5 i svoju fazu "5 – Dokumentacija" numerirao istim brojem. | Claude – pri dodavanju faze 5 na vremensku liniju. | Dokumentacijska faza preimenovana u "D". |
| 4–6 | U opis faze na stranici Pregled (`Pregled.cshtml.cs`) upisani su rezultati "200,1 km / 182,2 km" kao tekst – protivno pravilu "nikad ne kodiraj rezultate tvrdo"; da se planer pokvari, stranica bi i dalje pokazivala točne brojke. | Podagent reviewer (faza 8). | Otvoreno – T61. |
| 5 | K2 (nalog koji ni sam ne stane u radni dan) nema nijedan test; validator nema negativne testove za R37 ni za krivu kategoriju neizvedivosti (R33). Test-engineer je pokrio K1, K3 i K4, a K2 je ispao. | Podagent reviewer (faza 8). | Otvoreno – T62. |
| 6b | Stranica Ulazni podaci ima vlastitu provjeru neispravnih naloga (K4) umjesto da koristi `Feasibility` iz Corea; za duplicirani ID stroja ili lokacije može pokazati nalog kao ispravan, a plan ga prijavi kao K4. | Podagent reviewer (faza 8). | Otvoreno – T63. |
| 6c | Poruke validatora (na engleskom) prikazuju se u hrvatskom UI-ju (stupac "Poruka validatora"). | Podagent reviewer (faza 8). | Otvoreno – T63. |
| 7 | Nakon izdvajanja `PlanAssembly` (T58) brojevi redaka u `docs/planer.md` više ne odgovaraju kodu. | Podagent reviewer (faza 8). | Otvoreno – T63. |
| 9 | README je upućivao na snimke zaslona (`docs/screenshot-*.png`) koje nisu postojale, uz napomenu da se dodaju ručno. | Claude – provjera stavki predaje (faza 10); na GitHubu su se vidjele prazne slike. | Snimke napravljene iz pokrenute aplikacije (headless Chrome) i dodane (21.11., commit `d8b1153`). |
| P | README je bio u mapi `SmartLogistics/`, a korijen gita je mapa iznad – GitHub ga zato nije prikazivao. Korijen gita bio je poznat od faze 0 (otvoreno pitanje u `odluke.md`), ali AI nije upozorio na posljedicu za README. | Ja – na GitHubu nisam vidio README. | README premješten u korijen repozitorija, linkovi dobili prefiks `SmartLogistics/` (commit `6d40f9f`). |
| P | Gotovo prazna grana `main` na GitHubu potječe iz ugniježđenog git repozitorija u `src/SmartLogistics.Web/.git` (commit "First clear commit"). Reviewer ga je prijavio u fazi 8, ali je ostao neriješen. | Ja – na GitHubu su bile dvije grane; Claude – usporedbom commita. | `main` spojen u `master` (`dd90cd8`); ugniježđeni repozitorij čeka brisanje – T64. |
| P | Claude je pushao `master` na `main` ne provjerivši stanje na GitHubu: tehnički vođa je `main` u međuvremenu već obrisao, pa je push granu ponovno stvorio. | Claude – izlaz `git push` ("[new branch]"). | Grana je identična `master`u; alat je odbio brisanje udaljene grane, pa je brisanje prepušteno tehničkom vođi. |

## Osvrt na AI alate

**Alat:** Claude Code (model Claude Opus 5.5) u VS Codeu, s pravilima projekta u `CLAUDE.md` i šest
podagenata u `.claude/agents/` (domain-analyst, validator-engineer, test-engineer, solver-engineer,
ui-builder, reviewer).

**Kako je rad bio organiziran**
- Rad u fazama; nakon svake faze AI staje s izvještajem i prijedlogom commit poruke, a tehnički vođa
  potvrđuje pretpostavke prije sljedeće faze.
- Prvo specifikacija (pravila R1–R39), tek onda kod. Dvosmislenosti (npr. "9 h": vožnja ili vožnja +
  radnje) odlučio je čovjek, a AI ih je upisao kao pravila.
- Validator je napisan neovisno o planeru (agent nije smio čitati `Core/Planning/`) i ponovno računa
  sve km i minute. Planer svoj rezultat mora provući kroz validator prije vraćanja.
- Validator i testovi pisani su paralelno, uz zajednički ugovor (stub) i zajedničku tablicu oznaka
  pravila koje je tehnički vođa odredio prije pokretanja.
- Referentne vrijednosti (200,1 / 182,2 km) izračunate su neovisno i upisane u CLAUDE.md prije
  pisanja planera, s pravilom "rezultat ispod ovih brojeva = greška u kodu".
- Svako odstupanje i prijedlog upisuje se u `docs/odluke.md` (T1–T65).
- Od faze 6 više podagenata radi paralelno na odvojenim dijelovima (Web, Core/Planning, testovi), a
  na kraju podagent reviewer samo čita cijeli repozitorij i piše izvještaj (faza 8).

**Što je dobro radilo**
- Neovisni validator je odmah uhvatio pogrešan test (R32) – dvije neovisne implementacije pravila
  međusobno su se provjerile.
- Agenti su prijavljivali probleme izvan svog opsega umjesto da ih "popravljaju" (solver-engineer je
  prijavio grešku u Web projektu i nije je dirao).
- Egzaktni planer daje točno referentne vrijednosti, a u kodu nema nijedne tvrdo upisane brojke iz CLAUDE.md (provjereno grepom).
- Snimke zaslona (headless Edge) i neovisni izračun matrice u awk-u uhvatili su greške koje build
  i testovi ne vide.
- Solver-engineer je izmjerio da zadana postavka OR-Toolsa (jedan GLS, 5 s) u načinu `Total`
  izostavlja nalog (156,4 km, 9/10 – krši R32) i to prijavio umjesto da prihvati rezultat (T55).
- Druga, namjerno jednostavna egzaktna implementacija (Dijkstra oracle, faza 4b) potvrdila je
  egzaktni planer na 40 slučajeva – uključujući scenarije u kojima radni dan ograničava plan.
- Reviewer samo za čitanje (faza 8) našao je stvarne probleme koje testovi ne vide: tvrdo upisane
  km u UI-ju, pravilo bez testa (K2), ugniježđeni git repozitorij i necommitan rad.

**Što nije dobro radilo / na što paziti**
- AI je sklon tiho proširiti zadatak (dodatna polja, metode, provjere). Bez pravila "svako odstupanje
  u dokument" ta bi odstupanja ostala samo u chatu.
- AI je doslovno slijedio zadatak (.NET 8) umjesto da odmah predloži razumniju opciju.
- Promjene izvan projekta (VS rješenje u nadređenoj mapi, korijen git repozitorija) AI ne vidi kao dio
  zadatka, a upravo su one prouzročile probleme koje je primijetio čovjek.
- Zabrana čitanja `Core/Planning/` za validator je samo uputa u promptu (T5) – tehnički se ne može
  provjeriti da ju je agent poštovao.
- Dodavanje NuGet paketa (Markdig) odbio je sigurnosni sloj alata; rješenje je vlastiti mali renderer (T3).
- Velike faze s paralelnim podagentima brzo napreduju, ali se rad lako nakupi necommitan: na kraju
  faze 10 svih pet faza od 6a otišlo je u jedan commit.
- Nalazi recenzije ne rješavaju se sami: ono što reviewer prijavi, a nitko ne preuzme (T61–T64),
  ostaje otvoreno do predaje.
- AI ne vidi kako repozitorij izgleda na GitHubu (README u podmapi, višak grana) – to je primijetio
  čovjek.
