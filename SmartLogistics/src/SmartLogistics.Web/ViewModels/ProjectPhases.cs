using SmartLogistics.Core.Planning;

namespace SmartLogistics.Web.ViewModels;

/// <summary>
/// Optimum of the original task data, computed at runtime by the exact planner with the default options (T61).
/// Null values mean "not computed" (e.g. data/scenario.json could not be loaded).
/// </summary>
public sealed record ComputedOptima(double? TotalKm, double? DrivingOnlyKm);

/// <summary>
/// The project timeline of the overview page (/Pregled). The phase list is project metadata, updated at the end of each
/// phase; results in it are never typed in: the optimum comes from <see cref="ComputedOptima"/> and the OR-Tools limits
/// from <see cref="OrToolsPlanner"/>.
/// </summary>
public static class ProjectPhases
{
    public static IReadOnlyList<PhaseViewModel> Create(ComputedOptima optima)
    {
        ArgumentNullException.ThrowIfNull(optima);
        var runs = OrToolsPlanner.GuidedLocalSearchLambdas.Count;

        return
        [
            new("0", "Kostur rješenja",
                ".NET 10 rješenje (Core, Web, Tests), pravila projekta u CLAUDE.md i šest podagenata.",
                PhaseStatus.Done),
            new("1", "Specifikacija domene",
                "Podagent domain-analyst napisao je docs/spec.md; tehnički vođa potvrdio je pretpostavke (pravila R29–R39, zapis u docs/odluke.md).",
                PhaseStatus.Done),
            new("UI", "Dizajn sustav",
                "Moderni izgled aplikacije: bočni izbornik, kartice, ikone i tokeni boja; pravila u docs/ui-dizajn.md.",
                PhaseStatus.Done),
            new("2", "Domenski model i ulazni podaci",
                "Record tipovi u Core/Domain, Distance (dionice i matrica), data/scenario.json, ScenarioLoader, IRoutePlanner i testovi udaljenosti.",
                PhaseStatus.Done),
            new("3", "Validator i testovi",
                "Paralelno: validator-engineer piše PlanValidator iz spec.md, test-engineer pozitivne, negativne i referentne testove.",
                PhaseStatus.AwaitingApproval),
            new("4", "Egzaktni planer",
                "solver-engineer: ExactDpPlanner – sve raspodjele naloga (2^k) i DP po kamionu (lokacija + status naloga u bazi 3). "
                + OptimaText(optima),
                PhaseStatus.AwaitingApproval),
            new("4b", "Referentni oracle",
                "test-engineer: ReferenceOracle (Dijkstra nad stanjima lokacija + status naloga), namjerno jednostavna druga egzaktna implementacija; na nasumičnim scenarijima u oba načina radnog dana daje isti optimum kao ExactDpPlanner (T53).",
                PhaseStatus.AwaitingApproval),
            new("D", "Dokumentacija predaje",
                "README (upute, opis logike, testovi), rezultati, objašnjenje planera s mjerenjem skaliranja, AI log s osvrtom, utrošeno vrijeme i popis odluka koje čekaju potvrdu.",
                PhaseStatus.Done),
            new("5", "Neizvedivi nalozi – planer uvijek vraća plan",
                "Core/Feasibility.cs (K4/K1/K2), izmijenjeni R37 i novi R40, najmanji broj izostavljenih naloga kad radni dan ne dopušta sve; primjeri scenarija (u fazi 6b zamijenjeni uređivanjem naloga).",
                PhaseStatus.AwaitingApproval),
            new("6a", "Temelji sučelja za uređivanje i plan",
                "Spremište trenutnog scenarija u memoriji (izmjene naloga odmah mijenjaju plan), CSS klase s prefiksom sl-, nova navigacija (Planiranje / Projekt) i ViewModeli plana prijevoza s karticom ruta.",
                PhaseStatus.AwaitingApproval),
            new("6b", "Ulazni podaci i uređivanje naloga",
                "Ulazni podaci kao početna stranica (četiri tablice, matrica udaljenosti, karta naloga), stranice Nalozi s dodavanjem, izmjenom i brisanjem (i novi stroj s upisanom masom), \"Vrati izvorne podatke\", Post-Redirect-Get.",
                PhaseStatus.AwaitingApproval),
            new("6c", "Plan prijevoza s formom i kartom",
                "Forma (način radnog dana, max. minuta) dijeli se URL-om; sažetak s oznakom validatora, neizvedivi nalozi s razlogom, tablica ruta po kamionu i SVG karta s mrežom, osima, strelicama i brojevima koraka.",
                PhaseStatus.AwaitingApproval),
            new("7", "Drugi planer: OR-Tools",
                $"solver-engineer: OrToolsPlanner (Google OR-Tools, preuzimanje i isporuka) – {runs} {Format.Plural(runs, "determinističko pokretanje", "deterministička pokretanja", "determinističkih pokretanja")} "
                + $"GLS-a, svako do {Format.Integer(OrToolsPlanner.SolutionLimit)} rješenja, zajednička gornja granica {OrToolsPlanner.TimeLimitSeconds} s (T55); "
                + "na Planu prijevoza izbor algoritma, oznaka \"Optimum nije zajamčen (heuristika)\" (R38) i usporedba s egzaktnim planerom iz međuspremnika.",
                PhaseStatus.AwaitingApproval),
            new("8", "Recenzija",
                "Podagent reviewer pregledao je cijeli repozitorij samo čitanjem i napisao izvještaj; otvoreni nalazi upisani su kao prijedlozi T61–T64.",
                PhaseStatus.Done),
            new("9", "README",
                "Novi README s uputama, opisom logike i brojkama iz pokrenute aplikacije; odjeljci o AI alatima i utrošenom vremenu.",
                PhaseStatus.Done),
            new("10", "Završna provjera",
                "Čisti klon, build i testovi, popis stavki predaje te commit svih faza od 6a.",
                PhaseStatus.Done),
            new("11", "Usklađivanje, testni scenariji i UpRent tema",
                "Nalazi recenzije (T61–T64): rezultati na ovoj stranici računaju se pri pokretanju, Ulazni podaci koriste provjeru naloga iz Corea; nova stranica Testni scenariji (K1–K4 i ručno pokvareni planovi pred validatorom); poruke validatora na hrvatskom; vizualni identitet UpRenta i poveznica na UpRent web.",
                PhaseStatus.AwaitingApproval),
            new("…", "Sljedeće",
                "Odluke o prijedlozima (T3, T5–T75), demo na Azureu (T65) i prezentacija.",
                PhaseStatus.Planned),
        ];
    }

    /// <summary>"Izračunato sada egzaktnim planerom …: Total 200,1 km, DrivingOnly 182,2 km." – values formatted here.</summary>
    private static string OptimaText(ComputedOptima optima) =>
        optima.TotalKm is { } total && optima.DrivingOnlyKm is { } drivingOnly
            ? $"Izračunato sada egzaktnim planerom na podacima zadatka (zadane postavke): Total {Format.Km(total)}, DrivingOnly {Format.Km(drivingOnly)}."
            : "Optimum se računa pri otvaranju stranice, ali podaci zadatka trenutno nisu učitani.";
}
