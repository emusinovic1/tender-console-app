using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Tuning
{
    public static class NaprednaPretragaTuning
    {
        public static void Run()
        {
            var db = new DbClass();
            FillTendersAdvanced(db, total: 5000, keyword: "most");

            var baseline = new PretragaService1_Baseline(db);
            var t1 = new PretragaService1_Tuning1(db);
            var t2 = new PretragaService1_Tuning2(db);
            var t3 = new PretragaService1_Tuning3(db);

            string keyword = "most";
            decimal min = 1000;
            decimal max = 200000;
            var status = StatusTendera.Otvoren;
            var datumOd = DateTime.Now.AddMonths(-6);

            // sanity check – svi moraju vratiti isto
            SanityCheck(baseline, t1, t2, t3, keyword, min, max, status, datumOd);

            int warmup = 20;
            int iterations = 30;

            Console.WriteLine($"Tendera u bazi: {db.DohvatiSveTendere().Count}");
            Console.WriteLine();

            PerfBench.Measure("Baseline",
                () => baseline.NaprednaPretraga(keyword, min, max, status, datumOd),
                warmup, iterations);

            PerfBench.Measure("Tuning 1",
                () => t1.NaprednaPretraga(keyword, min, max, status, datumOd),
                warmup, iterations);

            PerfBench.Measure("Tuning 2",
                () => t2.NaprednaPretraga(keyword, min, max, status, datumOd),
                warmup, iterations);

            PerfBench.Measure("Tuning 3",
                () => t3.NaprednaPretraga(keyword, min, max, status, datumOd),
                warmup, iterations);
        }

        private static void SanityCheck(
                                        IPretragaTuning baseline,
                                        IPretragaTuning t1,
                                        IPretragaTuning t2,
                                        IPretragaTuning t3,
                                        string keyword, decimal min, decimal max,
                                        StatusTendera status, DateTime datumOd)
        {
            var r0 = baseline.NaprednaPretraga(keyword, min, max, status, datumOd);
            var r1 = t1.NaprednaPretraga(keyword, min, max, status, datumOd);
            var r2 = t2.NaprednaPretraga(keyword, min, max, status, datumOd);
            var r3 = t3.NaprednaPretraga(keyword, min, max, status, datumOd);

            EnsureSame("Tuning 1", r0, r1);
            EnsureSame("Tuning 2", r0, r2);
            EnsureSame("Tuning 3", r0, r3);
        }

        private static void EnsureSame(string label, List<Tender> a, List<Tender> b)
        {
            if (a.Count != b.Count)
                throw new Exception($"{label}: različit broj rezultata");

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Id != b[i].Id)
                    throw new Exception($"{label}: različit poredak na indexu {i}");
            }
        }
        private static void FillTendersAdvanced(DbClass db, int total, string keyword)
        {
            db.Tenderi.Clear();
            var rnd = new Random(123);

            for (int i = 0; i < total; i++)
            {
                string naziv;
                int mode = rnd.Next(0, 100);

                if (mode < 30)
                    naziv = $"izgradnja {keyword} {i}";
                else if (mode < 50)
                    naziv = $"{keyword}";
                else if (mode < 70)
                    naziv = $"projekat bez veze {i}";
                else
                    naziv = $"nabavka opreme {i}";

                db.DodajTender(new Tender
                {
                    FirmaId = 1,
                    Naziv = naziv,
                    ProcijenjenaVrijednost = rnd.Next(500, 300000),
                    DatumObjave = DateTime.Now.AddDays(-rnd.Next(1, 400)),
                    Status = rnd.Next(0, 10) < 8 ? StatusTendera.Otvoren : StatusTendera.Zatvoren
                });
            }
        }
    }

    public static class PerfBench1
    {
        public static void Measure(string label, Func<List<Tender>> action, int warmup, int iterations)
        {
            for (int i = 0; i < warmup; i++)
                action();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
                action();

            sw.Stop();
            long after = GC.GetAllocatedBytesForCurrentThread();

            Console.WriteLine($"[{label}]");
            Console.WriteLine($"Vrijeme: {sw.ElapsedMilliseconds} ms (avg {(double)sw.ElapsedMilliseconds / iterations:0.000} ms/op)");
            Console.WriteLine($"Memorija: {after - before} bytes");
            Console.WriteLine();
        }
    }


    public interface IPretragaTuning
    {
        List<Tender> NaprednaPretraga(string kljucnaRijec, decimal? minVrijednost, decimal? maxVrijednost, 
                                StatusTendera? status, DateTime? datumOd);
    }

    public class PretragaService1_Baseline : IPretragaTuning
    {
        private readonly DbClass _db;
        public PretragaService1_Baseline(DbClass db) => _db = db;

        public List<Tender> NaprednaPretraga(string kljucnaRijec, decimal? minVrijednost, decimal? maxVrijednost, 
                                        StatusTendera? status, DateTime? datumOd)
        {
            var sviTenderi = _db.DohvatiSveTendere();
            var rezultati = new List<Tender>();
            var scorovi = new List<double>();

            foreach (var tender in sviTenderi)
            {
                bool odgovara = true;
                double relevanceScore = 0.0;

                // 1. SCORING PO KLJUČNOJ RIJEČI
                if (!string.IsNullOrWhiteSpace(kljucnaRijec))
                {
                    string nazivLower = tender.Naziv.ToLower();
                    string kljucnaLower = kljucnaRijec.ToLower();

                    if (!nazivLower.Contains(kljucnaLower))
                    {
                        odgovara = false;
                    }
                    else
                    {
                        // Brojanje pojavljivanja
                        int count = 0;
                        int index = 0;
                        while ((index = nazivLower.IndexOf(kljucnaLower, index)) != -1)
                        {
                            count++;
                            index += kljucnaLower.Length;
                        }
                        relevanceScore += count * 5.0;
                    }
                }

                // 2. FILTRIRANJE PO MIN VRIJEDNOSTI
                if (minVrijednost.HasValue)
                {
                    if (tender.ProcijenjenaVrijednost < minVrijednost.Value)
                        odgovara = false;
                }

                // 3. FILTRIRANJE PO MAX VRIJEDNOSTI
                if (maxVrijednost.HasValue)
                {
                    if (tender.ProcijenjenaVrijednost > maxVrijednost.Value)
                        odgovara = false;
                }

                // 4. FILTRIRANJE PO STATUSU
                if (status.HasValue)
                {
                    if (tender.Status != status.Value)
                        odgovara = false;
                }

                // 5. FILTRIRANJE PO DATUMU
                if (datumOd.HasValue)
                {
                    if (tender.DatumObjave < datumOd.Value)
                        odgovara = false;
                }

                if (odgovara)
                {
                    rezultati.Add(tender);
                    scorovi.Add(relevanceScore);
                }
            }

            // 6. BUBBLE SORT
            for (int i = 0; i < rezultati.Count - 1; i++)
            {
                for (int j = 0; j < rezultati.Count - i - 1; j++)
                {
                    if (scorovi[j] < scorovi[j + 1])
                    {
                        double tempScore = scorovi[j];
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

    public class PretragaService1_Tuning1 : IPretragaTuning {
        private readonly DbClass _db;
        public PretragaService1_Tuning1(DbClass db) => _db = db;
        public List<Tender> NaprednaPretraga(string kljucnaRijec, decimal? minVrijednost, decimal? maxVrijednost,
                                    StatusTendera? status, DateTime? datumOd)
        {
            var sviTenderi = _db.DohvatiSveTendere();
            var rezultati = new List<Tender>();
            var scorovi = new List<double>();

            foreach (var tender in sviTenderi)
            {
                bool odgovara = true;
                double relevanceScore = 0.0;

                if (!string.IsNullOrWhiteSpace(kljucnaRijec))
                {
                    string nazivLower = tender.Naziv.ToLower();
                    string kljucnaLower = kljucnaRijec.ToLower();
                    if (!nazivLower.Contains(kljucnaLower))
                    {
                        odgovara = false;
                    }
                    else
                    {
                        int count = 0;
                        int index = 0;
                        while ((index = nazivLower.IndexOf(kljucnaLower, index)) != -1)
                        {
                            count++;
                            index += kljucnaLower.Length;
                        }
                        relevanceScore += count * 5.0;
                    }
                }

                if (minVrijednost.HasValue)
                {
                    if (tender.ProcijenjenaVrijednost < minVrijednost.Value)
                        odgovara = false;
                }

                if (maxVrijednost.HasValue)
                {
                    if (tender.ProcijenjenaVrijednost > maxVrijednost.Value)
                        odgovara = false;
                }

                if (status.HasValue)
                {
                    if (tender.Status != status.Value)
                        odgovara = false;
                }

                if (datumOd.HasValue)
                {
                    if (tender.DatumObjave < datumOd.Value)
                        odgovara = false;
                }

                if (odgovara)
                {
                    rezultati.Add(tender);
                    scorovi.Add(relevanceScore);
                }
            }

            // TEHNIKA 1: LINQ umjesto Bubble Sort
            var sorted = rezultati
                .Select((tender, index) => new { tender, score = scorovi[index] })
                .OrderByDescending(x => x.score)
                .Select(x => x.tender)
                .ToList();

            return sorted;
        }
    }

    // --- TEHNIKA 2 EARY EXIT, KORIŠTENJE CONTINUE UMJESTO PROVJERA
    public class PretragaService1_Tuning2 : IPretragaTuning {
        private readonly DbClass _db;
        public PretragaService1_Tuning2(DbClass db) => _db = db;

        public List<Tender> NaprednaPretraga(string kljucnaRijec, decimal? minVrijednost, decimal? maxVrijednost,
                                            StatusTendera? status, DateTime? datumOd)
        {
            var sviTenderi = _db.DohvatiSveTendere();
            var rezultati = new List<Tender>();
            var scorovi = new List<double>();

            string kljucnaLower = kljucnaRijec?.ToLower();

            foreach (var tender in sviTenderi)
            {
                double relevanceScore = 0.0;

                if (!string.IsNullOrWhiteSpace(kljucnaLower))
                {
                    string nazivLower = tender.Naziv.ToLower();

                    if (!nazivLower.Contains(kljucnaLower))
                        continue;

                    int count = 0;
                    int index = 0;
                    while ((index = nazivLower.IndexOf(kljucnaLower, index)) != -1)
                    {
                        count++;
                        index += kljucnaLower.Length;
                    }
                    relevanceScore += count * 5.0;
                }

                if (minVrijednost.HasValue && tender.ProcijenjenaVrijednost < minVrijednost.Value)
                    continue;

                if (maxVrijednost.HasValue && tender.ProcijenjenaVrijednost > maxVrijednost.Value)
                    continue;

                if (status.HasValue && tender.Status != status.Value)
                    continue;

                if (datumOd.HasValue && tender.DatumObjave < datumOd.Value)
                    continue;

                rezultati.Add(tender);
                scorovi.Add(relevanceScore);
            }

            for (int i = 0; i < rezultati.Count - 1; i++)
            {
                for (int j = 0; j < rezultati.Count - i - 1; j++)
                {
                    if (scorovi[j] < scorovi[j + 1])
                    {
                        (scorovi[j], scorovi[j + 1]) = (scorovi[j + 1], scorovi[j]);
                        (rezultati[j], rezultati[j + 1]) = (rezultati[j + 1], rezultati[j]);
                    }
                }
            }

            return rezultati;

        }

    }
    public class PretragaService1_Tuning3 : IPretragaTuning {
        private readonly DbClass _db;
        public PretragaService1_Tuning3(DbClass db) => _db = db;

        public List<Tender> NaprednaPretraga(string kljucnaRijec, decimal? minVrijednost, decimal? maxVrijednost, 
                                    StatusTendera? status, DateTime? datumOd)
        {
            var sviTenderi = _db.DohvatiSveTendere();
            var rezultati = new List<(Tender tender, double score)>();

            foreach (var tender in sviTenderi)
            {
                bool odgovara = true;
                double relevanceScore = 0.0;

                // 1. KLJUČNA RIJEČ
                if (!string.IsNullOrWhiteSpace(kljucnaRijec))
                {
                    string nazivLower = tender.Naziv.ToLower();
                    string kljucnaLower = kljucnaRijec.ToLower();

                    if (!nazivLower.Contains(kljucnaLower))
                    {
                        odgovara = false;
                    }
                    else
                    {
                        int count = 0;
                        int index = 0;
                        while ((index = nazivLower.IndexOf(kljucnaLower, index)) != -1)
                        {
                            count++;
                            index += kljucnaLower.Length;
                        }
                        relevanceScore += count * 5.0;
                    }
                }

                // 2. MIN VRIJEDNOST
                if (minVrijednost.HasValue)
                {
                    if (tender.ProcijenjenaVrijednost < minVrijednost.Value)
                        odgovara = false;
                }

                // 3. MAX VRIJEDNOST
                if (maxVrijednost.HasValue)
                {
                    if (tender.ProcijenjenaVrijednost > maxVrijednost.Value)
                        odgovara = false;
                }

                // 4. STATUS
                if (status.HasValue)
                {
                    if (tender.Status != status.Value)
                        odgovara = false;
                }

                // 5. DATUM
                if (datumOd.HasValue)
                {
                    if (tender.DatumObjave < datumOd.Value)
                        odgovara = false;
                }

                if (odgovara)
                {
                    // 🔽 JEDINA BITNA PROMJENA
                    rezultati.Add((tender, relevanceScore));
                }
            }

            // 6. BUBBLE SORT (IDENTIČAN, ali nad jednom listom)
            for (int i = 0; i < rezultati.Count - 1; i++)
            {
                for (int j = 0; j < rezultati.Count - i - 1; j++)
                {
                    if (rezultati[j].score < rezultati[j + 1].score)
                    {
                        var temp = rezultati[j];
                        rezultati[j] = rezultati[j + 1];
                        rezultati[j + 1] = temp;
                    }
                }
            }

            // Povrat samo tendera
            var finalniRezultat = new List<Tender>();
            foreach (var r in rezultati)
                finalniRezultat.Add(r.tender);

            return finalniRezultat;
        }

    }


}
