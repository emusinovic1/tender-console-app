using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    public class PretragaService : IPretragaService
    {
        private readonly DbClass _db;
        

        public PretragaService(DbClass db)
        {
            _db = db;
        }
        private const double BODOVI_PO_POJAVLJIVANJU = 5.0;
        public List<Tender> NaprednaPretraga(string kljucnaRijec,
                                             decimal? minVrijednost,
                                             decimal? maxVrijednost,
                                             StatusTendera? status,
                                             DateTime? datumOd)
        {
            var sviTenderi = _db.DohvatiSveTendere();

            var tenderiSaScorovima = FiltrirajISkorirajTendere(
                sviTenderi,
                kljucnaRijec,
                minVrijednost,
                maxVrijednost,
                status,
                datumOd
            );

            return SortirajPoRelevantnosti(tenderiSaScorovima);
        }

        private List<TenderScore> FiltrirajISkorirajTendere(
            List<Tender> tenderi,
            string kljucnaRijec,
            decimal? minVrijednost,
            decimal? maxVrijednost,
            StatusTendera? status,
            DateTime? datumOd)
        {
            return tenderi
                .Select(tender => new TenderScore
                {
                    Tender = tender,
                    Score = IzracunajRelevantnost(tender, kljucnaRijec)
                })
                .Where(ts => ZadovoljavaKriterije(
                    ts.Tender,
                    kljucnaRijec,
                    minVrijednost,
                    maxVrijednost,
                    status,
                    datumOd
                ))
                .ToList();
        }

        private bool ZadovoljavaKriterije(
            Tender tender,
            string kljucnaRijec,
            decimal? minVrijednost,
            decimal? maxVrijednost,
            StatusTendera? status,
            DateTime? datumOd)
        {
            return ZadovoljavaKljucnuRijec(tender, kljucnaRijec) &&
                   ZadovoljavaVrijednost(tender, minVrijednost, maxVrijednost) &&
                   ZadovoljavaStatus(tender, status) &&
                   ZadovoljavaDatum(tender, datumOd);
        }

        private bool ZadovoljavaKljucnuRijec(Tender tender, string kljucnaRijec)
        {
            if (string.IsNullOrWhiteSpace(kljucnaRijec))
                return true;

            return tender.Naziv.ToLower().Contains(kljucnaRijec.ToLower());
        }

        private bool ZadovoljavaVrijednost(
            Tender tender,
            decimal? minVrijednost,
            decimal? maxVrijednost)
        {
            bool zadovoljavaMin = !minVrijednost.HasValue ||
                                  tender.ProcijenjenaVrijednost >= minVrijednost.Value;

            bool zadovoljavaMax = !maxVrijednost.HasValue ||
                                  tender.ProcijenjenaVrijednost <= maxVrijednost.Value;

            return zadovoljavaMin && zadovoljavaMax;
        }

        private bool ZadovoljavaStatus(Tender tender, StatusTendera? status)
        {
            return !status.HasValue || tender.Status == status.Value;
        }

        private bool ZadovoljavaDatum(Tender tender, DateTime? datumOd)
        {
            return !datumOd.HasValue || tender.DatumObjave >= datumOd.Value;
        }

        private double IzracunajRelevantnost(Tender tender, string kljucnaRijec)
        {
            if (string.IsNullOrWhiteSpace(kljucnaRijec))
                return 0.0;

            int brojPojavljivanja = IzbrojiPojavljivanja(
                tender.Naziv.ToLower(),
                kljucnaRijec.ToLower()
            );

            return brojPojavljivanja * BODOVI_PO_POJAVLJIVANJU;
        }

        private int IzbrojiPojavljivanja(string tekst, string kljucnaRijec)
        {
            int count = 0;
            int index = 0;

            while ((index = tekst.IndexOf(kljucnaRijec, index)) != -1)
            {
                count++;
                index += kljucnaRijec.Length;
            }

            return count;
        }

        private List<Tender> SortirajPoRelevantnosti(List<TenderScore> tenderiSaScorovima)
        {
            return tenderiSaScorovima
                .OrderByDescending(ts => ts.Score)
                .Select(ts => ts.Tender)
                .ToList();
        }

        private class TenderScore
        {
            public Tender Tender { get; set; }
            public double Score { get; set; }
        }


        // Refaktoring
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

        // Data Level Refactoring: Replace magic numbers with named constants
        private const int TitleContainsScore = 10;
        private const int TitleStartsWithBonus = 5;
        private const int TitleExactBonus = 10;
        private const int DescriptionContainsScore = 3;
        private const int MaxDescriptionHits = 3;

        public List<Tender> PretraziPoKljucnojRijeci(string kljucnaRijec)
        {
            // Statement Level: Return as soon as you know the answer
            if (string.IsNullOrWhiteSpace(kljucnaRijec))
                return _db.DohvatiSveTendere();

            var sviTenderi = _db.DohvatiSveTendere();
            var scored = new List<ScoredTender>(capacity: sviTenderi.Count);

            // Data Level: Introduce an intermediate variable
            string keyword = kljucnaRijec.Trim();

            for (int i = 0; i < sviTenderi.Count; i++)
            {
                var tender = sviTenderi[i];

                // Data Level: Replace expression with a routine
                int score = ComputeScore(tender, keyword);

                if (score > 0)
                    scored.Add(new ScoredTender(tender, score, i));
            }

            scored.Sort(CompareScored);

            var rezultati = new List<Tender>(scored.Count);
            for (int i = 0; i < scored.Count; i++)
                rezultati.Add(scored[i].Tender);

            return rezultati;
        }

        // Statement Level: Move complex boolean expression into a well-named function
        private static int CompareScored(ScoredTender a, ScoredTender b)
        {
            int cmp = b.Score.CompareTo(a.Score); // desc
            return (cmp != 0) ? cmp : a.OriginalIndex.CompareTo(b.OriginalIndex);
        }

        private int ComputeScore(Tender tender, string keyword)
        {
            if (tender == null) return 0;

            int score = 0;

            score += ScoreTitle(tender.Naziv, keyword);
            score += ScoreDescription(tender.Opis, keyword);

            return score;
        }

        private int ScoreTitle(string title, string keyword)
        {
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(keyword))
                return 0;

            if (!title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return 0;

            int score = TitleContainsScore;

            if (title.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                score += TitleStartsWithBonus;

            if (string.Equals(title, keyword, StringComparison.OrdinalIgnoreCase))
                score += TitleExactBonus;

            return score;
        }

        private int ScoreDescription(string description, string keyword)
        {
            if (string.IsNullOrEmpty(description) || string.IsNullOrEmpty(keyword))
                return 0;

            if (!description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return 0;

            int score = DescriptionContainsScore;
            score += CountOccurrencesUpTo(description, keyword, MaxDescriptionHits);

            return score;
        }

        private static int CountOccurrencesUpTo(string text, string keyword, int maxHits)
        {
            int hits = 0;
            int index = 0;

            while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                hits++;
                if (hits > maxHits) break;

                index += keyword.Length;
            }

            return hits;
        }

        public List<Tender> PretraziPoVrijednosti(decimal minVrijednost, decimal maxVrijednost)
        {
            return _db.DohvatiSveTendere()
                .Where(t => t.ProcijenjenaVrijednost >= minVrijednost &&
                            t.ProcijenjenaVrijednost <= maxVrijednost)
                .OrderBy(t => t.ProcijenjenaVrijednost)
                .ToList();
        }


        public List<Tender> PretraziPoStatusu(StatusTendera status)
        {
            return _db.DohvatiSveTendere()
                .Where(t => t.Status == status)
                .OrderByDescending(t => t.DatumObjave)
                .ToList();
        }


        public List<Tender> PretraziPoDatumu(DateTime datumOd, DateTime datumDo)
        {
            return _db.DohvatiSveTendere()
                .Where(t => t.DatumObjave >= datumOd && t.DatumObjave <= datumDo)
                .OrderByDescending(t => t.DatumObjave)
                .ToList();
        }

        public List<Tender> SortirajPoNazivu(List<Tender> tenderi, bool uzlazno = true)
        {
            return uzlazno
                ? tenderi.OrderBy(t => t.Naziv).ToList()
                : tenderi.OrderByDescending(t => t.Naziv).ToList();
        }

        public List<Tender> SortirajPoVrijednosti(List<Tender> tenderi, bool uzlazno = true)
        {
            return uzlazno
                ? tenderi.OrderBy(t => t.ProcijenjenaVrijednost).ToList()
                : tenderi.OrderByDescending(t => t.ProcijenjenaVrijednost).ToList();
        }

        public List<Tender> SortirajPoRoku(List<Tender> tenderi, bool uzlazno = true)
        {
            return uzlazno
                ? tenderi.OrderBy(t => t.RokZaPrijavu).ToList()
                : tenderi.OrderByDescending(t => t.RokZaPrijavu).ToList();
        }

        public List<Tender> SortirajPoDatumu(List<Tender> tenderi, bool uzlazno = true)
        {
            return uzlazno
                ? tenderi.OrderBy(t => t.DatumObjave).ToList()
                : tenderi.OrderByDescending(t => t.DatumObjave).ToList();
        }

    }
}
