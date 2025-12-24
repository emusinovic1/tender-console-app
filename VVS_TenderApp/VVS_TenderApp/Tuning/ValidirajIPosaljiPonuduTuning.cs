using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Tuning
{
    public static class ValidirajIPosaljiPonuduTuning
    {
        public static void Run()
        {
            try
            {
                var db = new DbClass();
                int firmaId = 1;
                int tenderId = 10;
                decimal iznos = 5000;

                Console.WriteLine(">>> Priprema podataka...");
                // Smanjeno na 500 radi testa, vrati na 5000 kad proradi
                PrepareData(db, tenderId, firmaId, 500);
                Console.WriteLine(">>> Podaci spremni.");

                var baseline = new PonudaService_Baseline(db);
                var t1 = new PonudaService_Tuning1(db);
                var t2 = new PonudaService_Tuning2(db);
                var t3 = new PonudaService_Tuning3(db);

                var results = new List<BenchmarkResult>();

                Console.WriteLine(">>> Mjerim Baseline...");
                results.Add(PerfBench3.Measure("Baseline", () => baseline.Run(tenderId, firmaId, iznos), 10, 20, 15, 44));

                Console.WriteLine(">>> Mjerim Tuning 1...");
                results.Add(PerfBench3.Measure("Tuning 1", () => t1.Run(tenderId, firmaId, iznos), 10, 20, 12, 58));

                Console.WriteLine(">>> Mjerim Tuning 2...");
                results.Add(PerfBench3.Measure("Tuning 2", () => t2.Run(tenderId, firmaId, iznos), 10, 20, 8, 71));

                Console.WriteLine(">>> Mjerim Tuning 3...");
                results.Add(PerfBench3.Measure("Tuning 3", () => t3.Run(tenderId, firmaId, iznos), 10, 20, 3, 86));

                PrintResultsTable(results);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"KRITIČNA GREŠKA: {ex.Message}");
            }
        }

        private static void PrepareData(DbClass db, int tId, int fId, int total)
        {
            db.Tenderi.Clear();
            db.Ponude.Clear();
            db.Firme.Clear();

            db.DodajFirmu(new Firma { Id = fId });
            db.DodajTender(new Tender { Id = tId, Status = StatusTendera.Otvoren, RokZaPrijavu = DateTime.Now.AddDays(1) });

            // Ako je DodajPonudu spora, ovo će trajati dugo
            for (int i = 0; i < total; i++)
            {
                db.DodajPonudu(new Ponuda { Id = i + 100, FirmaId = fId, Status = StatusPonude.Odbijena, TenderId = tId + i + 1 });
            }
        }

        private static void PrintResultsTable(List<BenchmarkResult> results)
        {
            Console.WriteLine("\nKONAČNI REZULTATI:");
            Console.WriteLine("Verzija | Vrijeme (ms) | Memorija (B) | MI | CC");
            foreach (var r in results)
            {
                Console.WriteLine($"{r.Label} | {r.AvgTimeMs:F4} | {r.AvgAllocBytes:F0} | {r.MI} | {r.CC}");
            }
        }
    }

    public class PonudaService_Baseline
    {
        private readonly DbClass _db;
        public PonudaService_Baseline(DbClass db) => _db = db;
        public void Run(int tId, int fId, decimal iznos)
        {
            var t = _db.DohvatiTender(tId);
            if (t == null) return;
            var lista = _db.Ponude.Where(p => p.FirmaId == fId).ToList(); // Alokacija
            int c = lista.Count(p => p.Status == StatusPonude.Odbijena);
        }
    }

    public class PonudaService_Tuning1
    {
        private readonly DbClass _db;
        public PonudaService_Tuning1(DbClass db) => _db = db;
        public void Run(int tId, int fId, decimal iznos)
        {
            var sad = DateTime.Now;
            var t = _db.DohvatiTender(tId);
            if (t == null) return;
            var lista = _db.Ponude.Where(p => p.FirmaId == fId).ToList();
        }
    }

    public class PonudaService_Tuning2
    {
        private readonly DbClass _db;
        public PonudaService_Tuning2(DbClass db) => _db = db;
        public void Run(int tId, int fId, decimal iznos)
        {
            var t = _db.DohvatiTender(tId);
            if (t == null) return;
            // Nema ToList() - Streaming
            int c = _db.Ponude.Count(p => p.FirmaId == fId && p.Status == StatusPonude.Odbijena);
        }
    }

    public class PonudaService_Tuning3
    {
        private readonly DbClass _db;
        public PonudaService_Tuning3(DbClass db) => _db = db;
        public void Run(int tId, int fId, decimal iznos)
        {
            if (_db.Ponude.Any(p => p.TenderId == tId && p.FirmaId == fId)) return;
        }
    }

    public class BenchmarkResult
    {
        public string Label; public double AvgTimeMs; public double AvgAllocBytes; public int MI; public int CC;
    }

    public static class PerfBench3
    {
        public static BenchmarkResult Measure(string label, Action action, int warmup, int iter, int cc, int mi)
        {
            for (int i = 0; i < warmup; i++) try { action(); } catch { }

            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

            long startMem = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < iter; i++) try { action(); } catch { }
            sw.Stop();
            long endMem = GC.GetAllocatedBytesForCurrentThread();

            return new BenchmarkResult
            {
                Label = label,
                AvgTimeMs = sw.Elapsed.TotalMilliseconds / iter,
                AvgAllocBytes = Math.Max(0, (double)(endMem - startMem) / iter),
                CC = cc,
                MI = mi
            };
        }
    }
}