using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Tuning
{
    public static class RangirajPonudeTuning
    {
        public static void Run()
        {
            var db = new DbClass();
            PrepareTestData(db, brojPonuda: 50);

            int tenderId = 1;

            var baseline = new RangiranjeService_Baseline(db);
            var t1 = new RangiranjeService_Tuning1(db);
            var t2 = new RangiranjeService_Tuning2(db);
            var t3 = new RangiranjeService_Tuning3(db);

            SanityCheck(tenderId, baseline, t1, t2, t3);

            int warmup = 20;
            int iterations = 100;

            var ponude = db.DohvatiPonudePoTenderu(tenderId);
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("     RANGIRANJE PONUDA - CODE TUNING BENCHMARK");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Broj ponuda: {ponude.Count}");
            Console.WriteLine($"Tender ID: {tenderId}");
            Console.WriteLine($"Warmup: {warmup}, Iterations: {iterations}");
            Console.WriteLine();

            PerfBench2.Measure("BASELINE (Original)",
                () => baseline.RangirajPonude(tenderId), warmup, iterations);

            PerfBench2.Measure("TUNING 1 (Lookup Tabela)",
                () => t1.RangirajPonude(tenderId), warmup, iterations);

            PerfBench2.Measure("TUNING 2 (Strength Reduction)",
                () => t2.RangirajPonude(tenderId), warmup, iterations);

            PerfBench2.Measure("TUNING 3 (Ugrađeni Sort)",
                () => t3.RangirajPonude(tenderId), warmup, iterations);

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("Done.");
        }

        private static void SanityCheck(
            int tenderId,
            IRangiranjeLike baseline,
            IRangiranjeLike t1,
            IRangiranjeLike t2,
            IRangiranjeLike t3)
        {
            Console.WriteLine("Izvršavam SanityCheck...");

            var r0 = baseline.RangirajPonude(tenderId);
            var r1 = t1.RangirajPonude(tenderId);
            var r2 = t2.RangirajPonude(tenderId);
            var r3 = t3.RangirajPonude(tenderId);

            EnsureSameResults("Tuning 1 (Lookup)", r0, r1);
            EnsureSameResults("Tuning 2 (Strength Reduction)", r0, r2);
            EnsureSameResults("Tuning 3 (Ugrađeni Sort)", r0, r3);

            Console.WriteLine("✅ SanityCheck prošao - Prilikom primjene 3 tehnike code tunning sa svakom tehnikom smo dobili identicne rezultate!\n");
        }

        private static void EnsureSameResults(
            string label,
            List<(Ponuda ponuda, decimal skor)> expected,
            List<(Ponuda ponuda, decimal skor)> actual)
        {
            if (expected.Count != actual.Count)
                throw new Exception($"{label}: Different count. Expected={expected.Count}, Actual={actual.Count}");

            for (int i = 0; i < expected.Count; i++)
            {
                if (expected[i].ponuda.Id != actual[i].ponuda.Id)
                    throw new Exception(
                        $"{label}: Different order at index {i}. " +
                        $"ExpectedId={expected[i].ponuda.Id}, ActualId={actual[i].ponuda.Id}");

                decimal scoreDiff = Math.Abs(expected[i].skor - actual[i].skor);
                if (scoreDiff > 0.001m)
                    throw new Exception(
                        $"{label}: Different score at index {i}. " +
                        $"Expected={expected[i].skor:F2}, Actual={actual[i].skor:F2}");
            }
        }

        private static void PrepareTestData(DbClass db, int brojPonuda)
        {
            var tender = db.DohvatiTender(1);
            if (tender == null)
            {
                tender = new Tender
                {
                    Id = 1,
                    FirmaId = 1,
                    Naziv = "Test Tender za Benchmark",
                    Opis = "Test opis",
                    DatumObjave = DateTime.Now.AddDays(-30),
                    RokZaPrijavu = DateTime.Now.AddDays(30),
                    ProcijenjenaVrijednost = 100000,
                    Status = StatusTendera.Otvoren,
                    Kriteriji = new List<Kriterij>
                    {
                        new Kriterij { Tip = TipKriterija.Cijena, Tezina = 60 },
                        new Kriterij { Tip = TipKriterija.RokIsporuke, Tezina = 25 },
                        new Kriterij { Tip = TipKriterija.Garancija, Tezina = 15 }
                    }
                };
                db.DodajTender(tender);
            }

            var postojecePonude = db.DohvatiPonudePoTenderu(1);
            if (postojecePonude.Count < brojPonuda)
            {
                var rnd = new Random(42);
                int startId = postojecePonude.Any() ? postojecePonude.Max(p => p.Id) + 1 : 1;

                for (int i = startId; i < startId + brojPonuda; i++)
                {
                    var ponuda = new Ponuda
                    {
                        Id = i,
                        TenderId = 1,
                       
                        Iznos = 40000 + rnd.Next(0, 30000),
                        RokIsporukeDana = 20 + rnd.Next(0, 60),
                        GarancijaMjeseci = 12 + rnd.Next(0, 48),
                        
                        Status = StatusPonude.NaCekanju
                    };
                    db.DodajPonudu(ponuda);
                }
            }

            Console.WriteLine($"✅ Baza pripremljena: {db.DohvatiPonudePoTenderu(1).Count} ponuda\n");
        }
    }

    public static class PerfBench2
    {
        public static void Measure<T>(string label, Func<T> action, int warmup, int iterations)
        {
            for (int i = 0; i < warmup; i++)
                _ = action();

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

            Console.WriteLine($"┌─────────────────────────────────────────────────────────┐");
            Console.WriteLine($"│ {label,-55} │");
            Console.WriteLine($"├─────────────────────────────────────────────────────────┤");
            Console.WriteLine($"│ Vrijeme:    {elapsedMs,10} ms (avg {(double)elapsedMs / iterations,7:F2} ms/op) │");
            Console.WriteLine($"│ Memorija:   {allocBytes,10} bytes (avg {(double)allocBytes / iterations,7:F0} B/op)  │");
            Console.WriteLine($"└─────────────────────────────────────────────────────────┘");
            Console.WriteLine();
        }

        private static long GetAllocatedBytesSafe()
        {
            try
            {
                return GC.GetAllocatedBytesForCurrentThread();
            }
            catch
            {
                return GC.GetTotalMemory(false);
            }
        }
    }

    public interface IRangiranjeLike
    {
        List<(Ponuda ponuda, decimal skor)> RangirajPonude(int tenderId);
    }

    public class RangiranjeService_Baseline : IRangiranjeLike
    {
        private readonly DbClass _db;
        public RangiranjeService_Baseline(DbClass db) => _db = db;

        public List<(Ponuda ponuda, decimal skor)> RangirajPonude(int tenderId)
        {
            var tender = _db.DohvatiTender(tenderId);
            var ponude = _db.DohvatiPonudePoTenderu(tenderId);
            if (!ponude.Any() || !tender.Kriteriji.Any())
                return new List<(Ponuda ponuda, decimal skor)>();  

            var rezultat = new List<(Ponuda ponuda, decimal skor)>();
            var validneZaReferentneVrijednosti = ponude
                .Where(p => p != null && p.Iznos > 0 && p.RokIsporukeDana >= 0 && p.GarancijaMjeseci > 0)
                .ToList();

            if (!validneZaReferentneVrijednosti.Any())
                return new List<(Ponuda ponuda, decimal skor)>();  

            var minCijena = validneZaReferentneVrijednosti.Min(p => p.Iznos);
            var minRok = validneZaReferentneVrijednosti.Min(p => p.RokIsporukeDana);
            var maxGarancija = validneZaReferentneVrijednosti.Max(p => p.GarancijaMjeseci);

            foreach (var p in ponude)
            {
                decimal ukupno = 0;
                if (p == null || p.Iznos <= 0 || p.GarancijaMjeseci <= 0)
                    continue;
                if (p.RokIsporukeDana < 0)
                    continue;

                foreach (var o in ponude)
                {
                    if (p != o && o != null)
                    {
                        if (p.Iznos < o.Iznos)
                            ukupno += 0.5m;
                        else
                            ukupno -= 0.3m;
                    }
                }

                foreach (var k in tender.Kriteriji)
                {
                    if (k == null || k.Tezina <= 0)
                        continue;
                    if (k.Tip.Equals(TipKriterija.Cijena))
                    {
                        ukupno += (minCijena / p.Iznos) * k.Tezina;
                    }
                    else if (k.Tip.Equals(TipKriterija.RokIsporuke))
                    {
                        ukupno += ((decimal)minRok / p.RokIsporukeDana) * k.Tezina;
                    }
                    else if (k.Tip.Equals(TipKriterija.Garancija))
                    {
                        ukupno += ((decimal)p.GarancijaMjeseci / maxGarancija) * k.Tezina;
                    }
                }
                rezultat.Add((p, ukupno));
            }

            var sortiranRezultat = new List<(Ponuda ponuda, decimal skor)>(rezultat);
            for (int i = 0; i < sortiranRezultat.Count - 1; i++)
            {
                for (int j = 0; j < sortiranRezultat.Count - i - 1; j++)
                {
                    if (sortiranRezultat[j].skor < sortiranRezultat[j + 1].skor)
                    {
                        var temp = sortiranRezultat[j];
                        sortiranRezultat[j] = sortiranRezultat[j + 1];
                        sortiranRezultat[j + 1] = temp;
                    }
                }
            }
            return sortiranRezultat;
        }
    }
    //-------------------------------------------- Tuning koristenjem lookup tabele------------------------------------------------

    public class RangiranjeService_Tuning1 : IRangiranjeLike
    {
        private readonly DbClass _db;
        public RangiranjeService_Tuning1(DbClass db) => _db = db;

        public List<(Ponuda ponuda, decimal skor)> RangirajPonude(int tenderId)
        {
            var tender = _db.DohvatiTender(tenderId);
            var ponude = _db.DohvatiPonudePoTenderu(tenderId);
            if (!ponude.Any() || !tender.Kriteriji.Any())
                return new List<(Ponuda ponuda, decimal skor)>();  

            var rezultat = new List<(Ponuda ponuda, decimal skor)>();
            var validneZaReferentneVrijednosti = ponude
                .Where(p => p != null && p.Iznos > 0 && p.RokIsporukeDana >= 0 && p.GarancijaMjeseci > 0)
                .ToList();

            if (!validneZaReferentneVrijednosti.Any())
                return new List<(Ponuda ponuda, decimal skor)>();  

            var minCijena = validneZaReferentneVrijednosti.Min(p => p.Iznos);
            var minRok = validneZaReferentneVrijednosti.Min(p => p.RokIsporukeDana);
            var maxGarancija = validneZaReferentneVrijednosti.Max(p => p.GarancijaMjeseci);

            var kriterijLookup = new Dictionary<TipKriterija, Func<Ponuda, decimal, decimal, decimal, decimal>>
            {
                [TipKriterija.Cijena] = (p, minC, minR, maxG) => minC / p.Iznos,
                [TipKriterija.RokIsporuke] = (p, minC, minR, maxG) => (decimal)minR / p.RokIsporukeDana,
                [TipKriterija.Garancija] = (p, minC, minR, maxG) => (decimal)p.GarancijaMjeseci / maxG
            };

            foreach (var p in ponude)
            {
                decimal ukupno = 0;
                if (p == null || p.Iznos <= 0 || p.GarancijaMjeseci <= 0)
                    continue;
                if (p.RokIsporukeDana < 0)
                    continue;

                foreach (var o in ponude)
                {
                    if (p != o && o != null)
                    {
                        if (p.Iznos < o.Iznos)
                            ukupno += 0.5m;
                        else
                            ukupno -= 0.3m;
                    }
                }

                foreach (var k in tender.Kriteriji)
                {
                    if (k == null || k.Tezina <= 0)
                        continue;

                    if (kriterijLookup.TryGetValue(k.Tip, out var kalkulacija))
                    {
                        ukupno += kalkulacija(p, minCijena, minRok, maxGarancija) * k.Tezina;
                    }
                }
                rezultat.Add((p, ukupno));
            }

            var sortiranRezultat = new List<(Ponuda ponuda, decimal skor)>(rezultat);
            for (int i = 0; i < sortiranRezultat.Count - 1; i++)
            {
                for (int j = 0; j < sortiranRezultat.Count - i - 1; j++)
                {
                    if (sortiranRezultat[j].skor < sortiranRezultat[j + 1].skor)
                    {
                        var temp = sortiranRezultat[j];
                        sortiranRezultat[j] = sortiranRezultat[j + 1];
                        sortiranRezultat[j + 1] = temp;
                    }
                }
            }
            return sortiranRezultat;
        }
    }
    //--------------------------------------------------------Tuning koristenjem strength reduction ------------------------------------
    public class RangiranjeService_Tuning2 : IRangiranjeLike
    {
        private readonly DbClass _db;
        public RangiranjeService_Tuning2(DbClass db) => _db = db;

        public List<(Ponuda ponuda, decimal skor)> RangirajPonude(int tenderId)
        {
            var tender = _db.DohvatiTender(tenderId);
            var ponude = _db.DohvatiPonudePoTenderu(tenderId);

            if (!ponude.Any() || !tender.Kriteriji.Any())
                return new List<(Ponuda, decimal)>();

            var rezultat = new List<(Ponuda, decimal skor)>();

            var validneZaReferentneVrijednosti = ponude
                .Where(p => p != null
                         && p.Iznos > 0
                         && p.RokIsporukeDana >= 0
                         && p.GarancijaMjeseci > 0)
                .ToList();

            if (!validneZaReferentneVrijednosti.Any())
                return new List<(Ponuda, decimal)>();

            var minCijena = validneZaReferentneVrijednosti.Min(p => p.Iznos);
            var minRok = validneZaReferentneVrijednosti.Min(p => p.RokIsporukeDana);
            var maxGarancija = validneZaReferentneVrijednosti.Max(p => p.GarancijaMjeseci);

           
            var maxGarancija_Reciprocal = 1.0m / maxGarancija;
            var minRok_decimal = (decimal)minRok;  

            foreach (var p in ponude)
            {
                decimal ukupno = 0;

                if (p == null || p.Iznos <= 0 || p.GarancijaMjeseci <= 0)
                    continue;
                if (p.RokIsporukeDana < 0)
                    continue;

                
                foreach (var o in ponude)
                {
                    if (p != o && o != null)
                    {
                        if (p.Iznos < o.Iznos)
                        {
                            ukupno += 0.5m;
                        }
                        else
                        {
                            ukupno -= 0.3m;
                        }
                    }
                }

                
                var p_Iznos_Reciprocal = 1.0m / p.Iznos;
                var p_RokIsporuke_Reciprocal = 1.0m / p.RokIsporukeDana;
                var p_GarancijaMjeseci_decimal = (decimal)p.GarancijaMjeseci;

                foreach (var k in tender.Kriteriji)
                {
                    if (k == null || k.Tezina <= 0)
                        continue;

                    
                    ukupno += k.Tip switch
                    {
                        TipKriterija.Cijena => minCijena * p_Iznos_Reciprocal * k.Tezina,
                        TipKriterija.RokIsporuke => minRok_decimal * p_RokIsporuke_Reciprocal * k.Tezina,
                        TipKriterija.Garancija => p_GarancijaMjeseci_decimal * maxGarancija_Reciprocal * k.Tezina,
                        _ => 0
                    };
                }

                rezultat.Add((p, ukupno));
            }

            
            var sortiranRezultat = new List<(Ponuda ponuda, decimal skor)>(rezultat);
            for (int i = 0; i < sortiranRezultat.Count - 1; i++)
            {
                for (int j = 0; j < sortiranRezultat.Count - i - 1; j++)
                {
                    if (sortiranRezultat[j].skor < sortiranRezultat[j + 1].skor)
                    {
                        var temp = sortiranRezultat[j];
                        sortiranRezultat[j] = sortiranRezultat[j + 1];
                        sortiranRezultat[j + 1] = temp;
                    }
                }
            }

            return sortiranRezultat;
        }
    }
     //---------------------------------------------------Tuning koristenjem ugradene sort funkcije-------------------------------------
    public class RangiranjeService_Tuning3 : IRangiranjeLike
    {
        private readonly DbClass _db;
        public RangiranjeService_Tuning3(DbClass db) => _db = db;

        public List<(Ponuda ponuda, decimal skor)> RangirajPonude(int tenderId)
        {
            var tender = _db.DohvatiTender(tenderId);
            var ponude = _db.DohvatiPonudePoTenderu(tenderId);
            if (!ponude.Any() || !tender.Kriteriji.Any())
                return new List<(Ponuda ponuda, decimal skor)>();  

            var rezultat = new List<(Ponuda ponuda, decimal skor)>();
            var validneZaReferentneVrijednosti = ponude
                .Where(p => p != null && p.Iznos > 0 && p.RokIsporukeDana >= 0 && p.GarancijaMjeseci > 0)
                .ToList();

            if (!validneZaReferentneVrijednosti.Any())
                return new List<(Ponuda ponuda, decimal skor)>(); 

            var minCijena = validneZaReferentneVrijednosti.Min(p => p.Iznos);
            var minRok = validneZaReferentneVrijednosti.Min(p => p.RokIsporukeDana);
            var maxGarancija = validneZaReferentneVrijednosti.Max(p => p.GarancijaMjeseci);

            foreach (var p in ponude)
            {
                decimal ukupno = 0;
                if (p == null || p.Iznos <= 0 || p.GarancijaMjeseci <= 0)
                    continue;
                if (p.RokIsporukeDana < 0)
                    continue;

                foreach (var o in ponude)
                {
                    if (p != o && o != null)
                    {
                        if (p.Iznos < o.Iznos)
                            ukupno += 0.5m;
                        else
                            ukupno -= 0.3m;
                    }
                }

                foreach (var k in tender.Kriteriji)
                {
                    if (k == null || k.Tezina <= 0)
                        continue;
                    if (k.Tip.Equals(TipKriterija.Cijena))
                    {
                        ukupno += (minCijena / p.Iznos) * k.Tezina;
                    }
                    else if (k.Tip.Equals(TipKriterija.RokIsporuke))
                    {
                        ukupno += ((decimal)minRok / p.RokIsporukeDana) * k.Tezina;
                    }
                    else if (k.Tip.Equals(TipKriterija.Garancija))
                    {
                        ukupno += ((decimal)p.GarancijaMjeseci / maxGarancija) * k.Tezina;
                    }
                }
                rezultat.Add((p, ukupno));
            }

            return rezultat.OrderByDescending(x => x.skor).ToList();
        }
    }
}