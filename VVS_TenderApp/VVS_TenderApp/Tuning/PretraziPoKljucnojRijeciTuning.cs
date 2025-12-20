using System;
using System.Collections.Generic;
using System.Diagnostics;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;

namespace VVS_TenderApp.Tuning
{
    public static class PretraziPoKljucnojRijeciTuning
    {
        public static void Run()
        {
            // 1) Priprema velike baze tendera (inače su samo 2 u DbClass)
            var db = new DbClass();
            FillTenders(db, total: 5000, keyword: "abc"); // promijeni broj po potrebi

            // 2) Servisi za poređenje (baseline + tri tuned verzije)
            var baseline = new PretragaService_Baseline(db);
            var t1 = new PretragaService_Tuning1(db);
            var t2 = new PretragaService_Tuning2(db);
            var t3 = new PretragaService_Tuning3(db);

            string keyword = "abc";

            // 3) Brza provjera da svi vraćaju isto ponašanje (broj + poredak)
            //    Ovo nije unit test, ali brzo “uhvati” greške prije benchmarka.
            SanityCheck(keyword, baseline, t1, t2, t3);

            // 4) Benchmark parametri
            int warmup = 20;
            int iterations = 30; // povećaj ako ti je prebrzo / smanji ako traje dugo

            Console.WriteLine($"Tenders in DB: {db.DohvatiSveTendere().Count}");
            Console.WriteLine($"Warmup: {warmup}, Iterations: {iterations}");
            Console.WriteLine();

            // 5) Mjerenje
            PerfBench.Measure("Baseline", () => baseline.PretraziPoKljucnojRijeci(keyword), warmup, iterations);
            PerfBench.Measure("Tuning 1", () => t1.PretraziPoKljucnojRijeci(keyword), warmup, iterations);
            PerfBench.Measure("Tuning 2", () => t2.PretraziPoKljucnojRijeci(keyword), warmup, iterations);
            PerfBench.Measure("Tuning 3", () => t3.PretraziPoKljucnojRijeci(keyword), warmup, iterations);

            Console.WriteLine("Done.");
        }

        private static void SanityCheck(
            string keyword,
            IPretragaLike baseline,
            IPretragaLike t1,
            IPretragaLike t2,
            IPretragaLike t3)
        {
            var r0 = baseline.PretraziPoKljucnojRijeci(keyword);
            var r1 = t1.PretraziPoKljucnojRijeci(keyword);
            var r2 = t2.PretraziPoKljucnojRijeci(keyword);
            var r3 = t3.PretraziPoKljucnojRijeci(keyword);

            EnsureSameResults("Tuning 1", r0, r1);
            EnsureSameResults("Tuning 2", r0, r2);
            EnsureSameResults("Tuning 3", r0, r3);
        }

        private static void EnsureSameResults(string label, List<Tender> expected, List<Tender> actual)
        {
            if (expected.Count != actual.Count)
                throw new Exception($"{label}: Different count. Expected={expected.Count}, Actual={actual.Count}");

            // provjera poretka po Id (pošto vraća listu tendera)
            for (int i = 0; i < expected.Count; i++)
            {
                if (expected[i].Id != actual[i].Id)
                    throw new Exception($"{label}: Different order at index {i}. ExpectedId={expected[i].Id}, ActualId={actual[i].Id}");
            }
        }

        /// <summary>
        /// Popunjava bazu sa puno tendera, sa kontrolisanim procentom poklapanja u nazivu i opisu.
        /// </summary>
        private static void FillTenders(DbClass db, int total, string keyword)
        {
            // očisti postojeće primjere (opcionalno)
            db.Tenderi.Clear();

            var rnd = new Random(123);

            // cilj: dio tendera ima keyword u nazivu, dio u opisu, dio nema
            for (int i = 0; i < total; i++)
            {
                string naziv;
                string opis;

                int mode = rnd.Next(0, 100);

                if (mode < 20)
                {
                    // 20%: keyword u nazivu (startswith / equals / contains)
                    int variant = rnd.Next(0, 3);
                    if (variant == 0) naziv = $"{keyword} - tender {i}";           // startswith
                    else if (variant == 1) naziv = $"{keyword}";                    // equals
                    else naziv = $"xx {keyword} yy tender {i}";                     // contains-only
                    opis = "opis bez pogodaka";
                }
                else if (mode < 60)
                {
                    // 40%: keyword u opisu 1-6 puta (testira while + break)
                    naziv = $"tender {i}";
                    int repeats = rnd.Next(1, 7);
                    opis = MakeDescriptionWithRepeats(keyword, repeats);
                }
                else if (mode < 75)
                {
                    // 15%: i naziv i opis match
                    naziv = $"xx {keyword} yy tender {i}";
                    opis = MakeDescriptionWithRepeats(keyword, rnd.Next(1, 4));
                }
                else
                {
                    // 25%: nema match
                    naziv = $"tender {i}";
                    opis = "opis bez relevantnih rijeci";
                }

                db.DodajTender(new Tender
                {
                    FirmaId = 1,
                    Naziv = naziv,
                    Opis = opis,
                    DatumObjave = DateTime.Now.AddDays(-rnd.Next(1, 60)),
                    RokZaPrijavu = DateTime.Now.AddDays(rnd.Next(1, 60)),
                    ProcijenjenaVrijednost = rnd.Next(1000, 200000),
                    Status = StatusTendera.Otvoren,
                    Kriteriji = new List<Kriterij>()
                });
            }
        }

        private static string MakeDescriptionWithRepeats(string keyword, int repeats)
        {
            // razdvoji razmacima da IndexOf radi normalno
            // npr: "abc xx abc yy abc"
            var parts = new List<string>();
            for (int i = 0; i < repeats; i++)
            {
                parts.Add(keyword);
                parts.Add("xx");
            }
            return string.Join(" ", parts);
        }
    }

    // ------------------------------------------------------------
    // Benchmark helper
    // ------------------------------------------------------------
    public static class PerfBench
    {
        public static void Measure(string label, Func<List<Tender>> action, int warmup, int iterations)
        {
            // Warm-up (JIT)
            for (int i = 0; i < warmup; i++)
                _ = action();

            // GC cleanup prije mjerenja
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long beforeAlloc = GetAllocatedBytesSafe();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                _ = action();
            sw.Stop();

            long afterAlloc = GetAllocatedBytesSafe();

            long elapsedMs = sw.ElapsedMilliseconds;
            long allocBytes = afterAlloc - beforeAlloc;

            Console.WriteLine($"[{label}]");
            Console.WriteLine($"  Time: {elapsedMs} ms (avg {(double)elapsedMs / iterations:0.0000} ms/op)");
            Console.WriteLine($"  Allocated: {allocBytes} bytes (avg {(double)allocBytes / iterations:0.00} bytes/op)");
            Console.WriteLine();
        }

        private static long GetAllocatedBytesSafe()
        {
            try
            {
                return GC.GetAllocatedBytesForCurrentThread(); // .NET 5+
            }
            catch
            {
                return GC.GetTotalMemory(false);
            }
        }
    }

    // ------------------------------------------------------------
    // Minimalni interfejs da SanityCheck radi za sve verzije
    // ------------------------------------------------------------
    public interface IPretragaLike
    {
        List<Tender> PretraziPoKljucnojRijeci(string kljucnaRijec);
    }

    // ------------------------------------------------------------
    // 4 verzije servisa (Baseline + 3 tuning verzije)
    // Svaka je samostalna i ima svoj kod metode.
    // ------------------------------------------------------------

    public class PretragaService_Baseline : IPretragaLike
    {
        private readonly DbClass _db;
        public PretragaService_Baseline(DbClass db) => _db = db;

        public List<Tender> PretraziPoKljucnojRijeci(string kljucnaRijec)
        {
            if (string.IsNullOrWhiteSpace(kljucnaRijec))
                return _db.DohvatiSveTendere();

            var sviTenderi = _db.DohvatiSveTendere();
            var rezultati = new List<Tender>();
            var scorovi = new List<int>(); //po ovome kasnije ravnam

            for (int i = 0; i < sviTenderi.Count; i++)
            {
                var tender = sviTenderi[i];
                int score = 0;
                bool pronadjen = false;

                string nazivLower = tender.Naziv.ToLower();
                string opisLower = tender.Opis.ToLower();
                string kljucnaLower = kljucnaRijec.ToLower();

                if (nazivLower.Contains(kljucnaLower))
                {
                    pronadjen = true;
                    score += 10;
                    if (nazivLower.StartsWith(kljucnaLower))
                        score += 5;

                    if (nazivLower == kljucnaLower)
                        score += 10;
                }

                if (opisLower.Contains(kljucnaLower))
                {
                    pronadjen = true;
                    score += 3;

                    int pojavljivanja = 0;
                    int index = 0;
                    while ((index = opisLower.IndexOf(kljucnaLower, index)) != -1)
                    {
                        pojavljivanja++;
                        index += kljucnaLower.Length;

                        if (pojavljivanja > 3)
                            break;
                    }
                    score += pojavljivanja;
                }

                if (pronadjen)
                {
                    rezultati.Add(tender);
                    scorovi.Add(score);
                }
            }

            for (int i = 0; i < rezultati.Count - 1; i++)
            {
                for (int j = 0; j < rezultati.Count - i - 1; j++)
                {
                    if (scorovi[j] < scorovi[j + 1])
                    {
                        int tempScore = scorovi[j];
                        scorovi[j] = scorovi[j + 1];
                        scorovi[j + 1] = tempScore;

                        var tempTender = rezultati[j];
                        rezultati[j] = rezultati[j + 1];
                        rezultati[j + 1] = tempTender;
                    }
                }
            }

            return rezultati;
        }
    }

    // ---------------- Tuning 1: StringComparison, trim keyword, manje ToLower ----------------
    public class PretragaService_Tuning1 : IPretragaLike
    {
        private readonly DbClass _db;
        public PretragaService_Tuning1(DbClass db) => _db = db;

        public List<Tender> PretraziPoKljucnojRijeci(string kljucnaRijec)
        {
            if (string.IsNullOrWhiteSpace(kljucnaRijec))
                return _db.DohvatiSveTendere();

            var sviTenderi = _db.DohvatiSveTendere();
            var rezultati = new List<Tender>();
            var scorovi = new List<int>();

            string kljucna = kljucnaRijec.Trim();

            for (int i = 0; i < sviTenderi.Count; i++)
            {
                var tender = sviTenderi[i];
                int score = 0;
                bool pronadjen = false;

                if (!string.IsNullOrEmpty(tender.Naziv) &&
                    tender.Naziv.Contains(kljucna, StringComparison.OrdinalIgnoreCase))
                {
                    pronadjen = true;
                    score += 10;

                    if (tender.Naziv.StartsWith(kljucna, StringComparison.OrdinalIgnoreCase))
                        score += 5;

                    if (string.Equals(tender.Naziv, kljucna, StringComparison.OrdinalIgnoreCase))
                        score += 10;
                }

                if (!string.IsNullOrEmpty(tender.Opis) &&
                    tender.Opis.Contains(kljucna, StringComparison.OrdinalIgnoreCase))
                {
                    pronadjen = true;
                    score += 3;

                    int pojavljivanja = 0;
                    int index = 0;

                    while ((index = tender.Opis.IndexOf(kljucna, index, StringComparison.OrdinalIgnoreCase)) != -1)
                    {
                        pojavljivanja++;
                        index += kljucna.Length;

                        if (pojavljivanja > 3)
                            break;
                    }

                    score += pojavljivanja;
                }

                if (pronadjen)
                {
                    rezultati.Add(tender);
                    scorovi.Add(score);
                }
            }

            for (int i = 0; i < rezultati.Count - 1; i++)
            {
                for (int j = 0; j < rezultati.Count - i - 1; j++)
                {
                    if (scorovi[j] < scorovi[j + 1])
                    {
                        int tempScore = scorovi[j];
                        scorovi[j] = scorovi[j + 1];
                        scorovi[j + 1] = tempScore;

                        var tempTender = rezultati[j];
                        rezultati[j] = rezultati[j + 1];
                        rezultati[j + 1] = tempTender;
                    }
                }
            }

            return rezultati;
        }
    }

    // ---------------- Tuning 2: spoj listi (rezultat+score) u jednu listu ----------------
    public class PretragaService_Tuning2 : IPretragaLike
    {
        private readonly DbClass _db;
        public PretragaService_Tuning2(DbClass db) => _db = db;

        private readonly struct ScoredTender
        {
            public Tender Tender { get; }
            public int Score { get; }
            public ScoredTender(Tender tender, int score) { Tender = tender; Score = score; }
        }

        public List<Tender> PretraziPoKljucnojRijeci(string kljucnaRijec)
        {
            if (string.IsNullOrWhiteSpace(kljucnaRijec))
                return _db.DohvatiSveTendere();

            var sviTenderi = _db.DohvatiSveTendere();
            var scored = new List<ScoredTender>();

            string kljucna = kljucnaRijec.Trim();

            for (int i = 0; i < sviTenderi.Count; i++)
            {
                var tender = sviTenderi[i];
                int score = 0;
                bool pronadjen = false;

                if (!string.IsNullOrEmpty(tender.Naziv) &&
                    tender.Naziv.Contains(kljucna, StringComparison.OrdinalIgnoreCase))
                {
                    pronadjen = true;
                    score += 10;

                    if (tender.Naziv.StartsWith(kljucna, StringComparison.OrdinalIgnoreCase))
                        score += 5;

                    if (string.Equals(tender.Naziv, kljucna, StringComparison.OrdinalIgnoreCase))
                        score += 10;
                }

                if (!string.IsNullOrEmpty(tender.Opis) &&
                    tender.Opis.Contains(kljucna, StringComparison.OrdinalIgnoreCase))
                {
                    pronadjen = true;
                    score += 3;

                    int pojavljivanja = 0;
                    int index = 0;

                    while ((index = tender.Opis.IndexOf(kljucna, index, StringComparison.OrdinalIgnoreCase)) != -1)
                    {
                        pojavljivanja++;
                        index += kljucna.Length;

                        if (pojavljivanja > 3)
                            break;
                    }

                    score += pojavljivanja;
                }

                if (pronadjen)
                    scored.Add(new ScoredTender(tender, score));
            }

            // bubble sort nad jednom listom
            for (int i = 0; i < scored.Count - 1; i++)
            {
                for (int j = 0; j < scored.Count - i - 1; j++)
                {
                    if (scored[j].Score < scored[j + 1].Score)
                    {
                        var tmp = scored[j];
                        scored[j] = scored[j + 1];
                        scored[j + 1] = tmp;
                    }
                }
            }

            var rezultati = new List<Tender>(scored.Count);
            for (int i = 0; i < scored.Count; i++)
                rezultati.Add(scored[i].Tender);

            return rezultati;
        }
    }

    // ---------------- Tuning 3: zamjena bubble sort sa O(n log n) sort + stable tie-break ----------------
    public class PretragaService_Tuning3 : IPretragaLike
    {
        private readonly DbClass _db;
        public PretragaService_Tuning3(DbClass db) => _db = db;

        private readonly struct ScoredTender
        {
            public Tender Tender { get; }
            public int Score { get; }
            public int OriginalIndex { get; }

            public ScoredTender(Tender tender, int score, int originalIndex)
            {
                Tender = tender;
                Score = score;
                OriginalIndex = originalIndex;
            }
        }

        public List<Tender> PretraziPoKljucnojRijeci(string kljucnaRijec)
        {
            if (string.IsNullOrWhiteSpace(kljucnaRijec))
                return _db.DohvatiSveTendere();

            var sviTenderi = _db.DohvatiSveTendere();
            var scored = new List<ScoredTender>();

            string kljucna = kljucnaRijec.Trim();

            for (int i = 0; i < sviTenderi.Count; i++)
            {
                var tender = sviTenderi[i];
                int score = 0;
                bool pronadjen = false;

                if (!string.IsNullOrEmpty(tender.Naziv) &&
                    tender.Naziv.Contains(kljucna, StringComparison.OrdinalIgnoreCase))
                {
                    pronadjen = true;
                    score += 10;

                    if (tender.Naziv.StartsWith(kljucna, StringComparison.OrdinalIgnoreCase))
                        score += 5;

                    if (string.Equals(tender.Naziv, kljucna, StringComparison.OrdinalIgnoreCase))
                        score += 10;
                }

                if (!string.IsNullOrEmpty(tender.Opis) &&
                    tender.Opis.Contains(kljucna, StringComparison.OrdinalIgnoreCase))
                {
                    pronadjen = true;
                    score += 3;

                    int pojavljivanja = 0;
                    int index = 0;

                    while ((index = tender.Opis.IndexOf(kljucna, index, StringComparison.OrdinalIgnoreCase)) != -1)
                    {
                        pojavljivanja++;
                        index += kljucna.Length;

                        if (pojavljivanja > 3)
                            break;
                    }

                    score += pojavljivanja;
                }

                if (pronadjen)
                    scored.Add(new ScoredTender(tender, score, i));
            }

            scored.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score); // desc
                if (cmp != 0) return cmp;
                return a.OriginalIndex.CompareTo(b.OriginalIndex); // stabilnost na jednakim score-ovima
            });

            var rezultati = new List<Tender>(scored.Count);
            for (int i = 0; i < scored.Count; i++)
                rezultati.Add(scored[i].Tender);

            return rezultati;
        }
    }
}
