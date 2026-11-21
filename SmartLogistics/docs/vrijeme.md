# Utrošeno vrijeme

**Ukupno: oko 2 h 35 min** zajedničkog rada (tehnički vođa + Claude Code),
20.11.2026. od 22:07 do 21.11.2026. 00:42.

Vrijeme je izmjereno iz vremenskih oznaka (git commitovi i datoteke u repozitoriju), ne procijenjeno
napamet. Ne uključuje vrijeme izvan sesije – čitanje koda radi razumijevanja, pripremu prezentacije
i demo. To dopiši u posljednji redak tablice.

## Po fazama

| Faza | Od – do | Trajanje | Što je napravljeno | Izvor oznake |
|------|---------|---------:|--------------------|--------------|
| Priprema | 22:07 – 22:16 | 9 min | Visual Studio rješenje (kasnije zamijenjeno) | datoteke VS predloška |
| 0 – Kostur | 22:16 – 22:24 | 8 min | .NET rješenje, CLAUDE.md, zadatak, 6 podagenata, prelazak na .NET 10 | `docs/zadatak.md`, `.claude/agents/` |
| 1 – Specifikacija | 22:24 – 22:50 | 26 min | domain-analyst: `spec.md`; potvrda pretpostavki; zapis odluka; stranica Dokumentacija | commit `e9e1313` |
| UI – Dizajn sustav | 22:50 – 23:09 | 19 min | bočni izbornik, kartice, ikone, tokeni, `ui-dizajn.md` | commit `3568821` |
| 2 – Domena i udaljenosti | 23:09 – 23:17 | 8 min | `Core/Domain`, `Distance`, `scenario.json`, loader, testovi udaljenosti, stranica Ulazni podaci | commit `11233d7` |
| 3 – Validator i testovi | 23:17 – 23:33 | 16 min | validator-engineer i test-engineer paralelno; stranica Pravila i provjere | commit `f04f0da` |
| 4 – Egzaktni planer | 23:33 – 23:51 | 18 min | solver-engineer: `ExactDpPlanner`; stranice Plan ruta i Karta | commit `c858034` |
| D – Dokumentacija predaje | 23:51 – 00:15 | ~24 min | README, rezultati, AI log, utrošeno vrijeme, pregled otvorenih odluka | ova datoteka |
| 5 – Neizvedivi nalozi | 00:15 – 00:42 | ~27 min | `Feasibility.cs`, planer uvijek vraća plan (R37, R40); validator prilagođen; 50 novih testova s neovisnim oracleom; primjeri scenarija u aplikaciji (kasnije, u fazi 6b, zamijenjeni uređivanjem naloga); objašnjenje planera (`planer.md`) | ova datoteka |
| **Ukupno u sesiji** | | **~2 h 35 min** | | |
| Izvan sesije (dopisati) | | … | čitanje i razumijevanje koda, priprema prezentacije, demo | – |

## Napomene

- Faze s podagentima (1, 3, 4) trajale su 5–8 minuta strojnog rada po agentu. Faza 3 je dva agenta
  pokrenula paralelno pa je trajala koliko i jedan.
- Najviše vremena otišlo je na odluke i provjere (specifikacija, ugovor validatora, provjera
  rezultata), a ne na pisanje koda: kod su pisali Claude Code i podagenti, a odluke i provjere tehnički vođa.
