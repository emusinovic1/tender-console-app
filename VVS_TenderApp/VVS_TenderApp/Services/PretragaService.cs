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
        public List<Tender> NaprednaPretraga(string kljucnaRijec, decimal? minVrijednost, decimal? maxVrijednost, StatusTendera? status,
                                             DateTime? datumOd, DateTime? datumDo,  int? firmaId)
        {
            var sviTenderi = _db.DohvatiSveTendere();
            var rezultat = new List<Tender>();

            foreach (var tender in sviTenderi)
            {
                bool odgovara = true;

                //po kljucnoj rijeci
                if (!string.IsNullOrWhiteSpace(kljucnaRijec))
                {
                    if (!tender.Naziv.ToLower().Contains(kljucnaRijec.ToLower()) &&
                        !tender.Opis.ToLower().Contains(kljucnaRijec.ToLower()))
                        odgovara = false;
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

                if (datumDo.HasValue)
                {
                    if (tender.DatumObjave > datumDo.Value)
                        odgovara = false;
                }

                if (firmaId.HasValue)
                {
                    if (tender.FirmaId != firmaId.Value)
                        odgovara = false;
                }

                if (odgovara)
                    rezultat.Add(tender);
            }

            return rezultat;
        }


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
