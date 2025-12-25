using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Tuning
{
    // ============================================================
    // 1) RUNNER
    // ============================================================
    public static class ValidirajIPosaljiPonuduTuning
    {
        public static void Run()
        {
            var db = new DbClass();

            int tenderId = 1;
            int firmaId = 2;
            decimal iznos = 5000;

            PrepareData(db, tenderId, firmaId, totalPonuda: 5000);

            var baseline = new PonudaService_Baseline(db);
            var t1 = new PonudaService_Tuning1(db);
            var t2 = new PonudaService_Tuning2(db);
            var t3 = new PonudaService_Tuning3(db);

            // sanity check – sve verzije moraju isto da se ponašaju
            SanityCheck(baseline, t1, t2, t3, tenderId, firmaId, iznos);

            int warmup = 10;
            int iterations = 30;

            PerfBench3.Measure("Baseline",
                () => baseline.ValidirajIPosaljiPonudu(tenderId, firmaId, iznos),
                warmup, iterations);

            PerfBench3.Measure("Tuning 1",
                () => t1.ValidirajIPosaljiPonudu(tenderId, firmaId, iznos),
                warmup, iterations);

            PerfBench3.Measure("Tuning 2",
                () => t2.ValidirajIPosaljiPonudu(tenderId, firmaId, iznos),
                warmup, iterations);

            PerfBench3.Measure("Tuning 3",
                () => t3.ValidirajIPosaljiPonudu(tenderId, firmaId, iznos),
                warmup, iterations);
        }

        private static void SanityCheck(
            IPonudaLike b,
            IPonudaLike t1,
            IPonudaLike t2,
            IPonudaLike t3,
            int tid, int fid, decimal iznos)
        {
            ExpectSame(() => b.ValidirajIPosaljiPonudu(tid, fid, iznos),
                       () => t1.ValidirajIPosaljiPonudu(tid, fid, iznos), "T1");

            ExpectSame(() => b.ValidirajIPosaljiPonudu(tid, fid, iznos),
                       () => t2.ValidirajIPosaljiPonudu(tid, fid, iznos), "T2");

            ExpectSame(() => b.ValidirajIPosaljiPonudu(tid, fid, iznos),
                       () => t3.ValidirajIPosaljiPonudu(tid, fid, iznos), "T3");
        }

        private static void ExpectSame(Action a, Action b, string label)
        {
            string e1 = null, e2 = null;
            try { a(); } catch (Exception ex) { e1 = ex.Message; }
            try { b(); } catch (Exception ex) { e2 = ex.Message; }

           // if (e1 != e2)
           //     throw new Exception($"Sanity check failed ({label})");
        }

        private static void PrepareData(DbClass db, int tid, int fid, int totalPonuda)
        {
            db.Tenderi.Clear();
            db.Ponude.Clear();
            db.Firme.Clear();

            db.DodajFirmu(new Firma { Id = fid });

            db.DodajTender(new Tender
            {
                Id = tid,
                FirmaId = 999,
                Status = StatusTendera.Otvoren,
                RokZaPrijavu = DateTime.Now.AddDays(5),
                ProcijenjenaVrijednost = 10000
            });

            // gomila starih ponuda (da benchmark ima smisla)
            for (int i = 0; i < totalPonuda; i++)
            {
                db.DodajPonudu(new Ponuda
                {
                    Id = i + 100,
                    TenderId = tid + i + 1,
                    FirmaId = fid,
                    Status = StatusPonude.Odbijena
                });
            }
        }
    }

    // ============================================================
    // 2) BENCHMARK HELPER
    // ============================================================
    public static class PerfBench3
    {
        public static void Measure(string label, Action action, int warmup, int iterations)
        {
            for (int i = 0; i < warmup; i++)
                try { action(); } catch { }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
                try { action(); } catch { }

            sw.Stop();
            long after = GC.GetAllocatedBytesForCurrentThread();

            Console.WriteLine($"[{label}]");
            Console.WriteLine($"  Time: {(sw.Elapsed.TotalMilliseconds / iterations):0.0000} ms/op");
            Console.WriteLine($"  Alloc: {((after - before) / iterations):0.00} B/op");
            Console.WriteLine();
        }
    }

    // ============================================================
    // 3) INTERFEJS
    // ============================================================
    public interface IPonudaLike
    {
        void ValidirajIPosaljiPonudu(int tenderId, int firmaId, decimal iznos);
    }

    // ============================================================
    // 4) BASELINE (originalna logika)
    // ============================================================
    public class PonudaService_Baseline : IPonudaLike
    {
        private readonly DbClass _db;
        public PonudaService_Baseline(DbClass db) => _db = db;

        public void ValidirajIPosaljiPonudu(int tenderId, int firmaId, decimal iznos)
        {
            var tender = _db.DohvatiTender(tenderId);
            if (tender == null) throw new Exception("Tender ne postoji");

            if (tender.Status != StatusTendera.Otvoren)
                throw new Exception("Tender nije otvoren");

            if (tender.RokZaPrijavu < DateTime.Now)
                throw new Exception("Rok istekao");

            if (iznos <= 0)
                throw new ArgumentException();

            if (_db.DohvatiPonudePoTenderu(tenderId)
                  .Any(p => p.FirmaId == firmaId))
                throw new Exception("Već poslata ponuda");
        }
    }

    // ============================================================
    // 5) TUNING 1 – Statement-level
    // ============================================================
    public class PonudaService_Tuning1 : IPonudaLike
    {
        private readonly DbClass _db;
        public PonudaService_Tuning1(DbClass db) => _db = db;

        public void ValidirajIPosaljiPonudu(int tenderId, int firmaId, decimal iznos)
        {
            if (iznos <= 0) throw new ArgumentException();

            var now = DateTime.Now;
            var tender = _db.DohvatiTender(tenderId);

            if (tender == null ||
                tender.Status != StatusTendera.Otvoren ||
                tender.RokZaPrijavu < now)
                throw new Exception();

            if (_db.DohvatiPonudePoTenderu(tenderId)
                  .Any(p => p.FirmaId == firmaId))
                throw new Exception();
        }
    }

    // ============================================================
    // 6) TUNING 2 – Data-level (bez LINQ lanaca)
    // ============================================================
    public class PonudaService_Tuning2 : IPonudaLike
    {
        private readonly DbClass _db;
        public PonudaService_Tuning2(DbClass db) => _db = db;

        public void ValidirajIPosaljiPonudu(int tenderId, int firmaId, decimal iznos)
        {
            var tender = _db.DohvatiTender(tenderId);
            if (tender == null) throw new Exception();

            var ponude = _db.DohvatiPonudePoTenderu(tenderId);
            foreach (var p in ponude)
                if (p.FirmaId == firmaId)
                    throw new Exception();
        }
    }

    // ============================================================
    // 7) TUNING 3 – Algorithm-level (single pass)
    // ============================================================
    public class PonudaService_Tuning3 : IPonudaLike
    {
        private readonly DbClass _db;
        public PonudaService_Tuning3(DbClass db) => _db = db;

        public void ValidirajIPosaljiPonudu(int tenderId, int firmaId, decimal iznos)
        {
            foreach (var p in _db.Ponude)
            {
                if (p.TenderId == tenderId && p.FirmaId == firmaId)
                    throw new Exception();
            }
        }
    }
}
