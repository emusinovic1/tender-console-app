using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    public class RangiranjeService
    {
            private readonly DbClass _db;

            public RangiranjeService(DbClass db)
            {
                _db = db;
            }

        public List<(Ponuda ponuda, decimal skor)> RangirajPonude(int tenderId)
        {
            var tender = _db.DohvatiTender(tenderId);
            var ponude = _db.DohvatiPonudePoTenderu(tenderId);

            if (!ImaPodatakaZaObradu(ponude, tender))
                return new List<(Ponuda, decimal)>();

            var validnePonude = FiltrirajValidnePonude(ponude);

            if (!validnePonude.Any())
                return new List<(Ponuda, decimal)>();

            var referentneVrijednosti = IzracunajReferentneVrijednosti(validnePonude);
            var rezultat = new List<(Ponuda, decimal skor)>();

            foreach (var ponuda in ponude)
            {
                if (!JeValidnaPonuda(ponuda))
                    continue;

                decimal ukupanSkor = IzracunajUkupanSkor(ponuda, ponude, tender, referentneVrijednosti);
                rezultat.Add((ponuda, ukupanSkor));
            }

            return SortirajPoSkoru(rezultat);
        }

        private bool ImaPodatakaZaObradu(List<Ponuda> ponude, Tender tender)
        {
            return ponude.Any() && tender.Kriteriji.Any();
        }

        private List<Ponuda> FiltrirajValidnePonude(List<Ponuda> ponude)
        {
            return ponude
                .Where(p => p != null
                         && p.Iznos > 0
                         && p.RokIsporukeDana >= 0
                         && p.GarancijaMjeseci > 0)
                .ToList();
        }

        private bool JeValidnaPonuda(Ponuda ponuda)
        {
            if (ponuda == null)
                return false;

            if (ponuda.Iznos <= 0 || ponuda.GarancijaMjeseci <= 0)
                return false;

            if (ponuda.RokIsporukeDana < 0)
                return false;

            return true;
        }

        private ReferentneVrijednosti IzracunajReferentneVrijednosti(List<Ponuda> validnePonude)
        {
            return new ReferentneVrijednosti
            {
                MinCijena = validnePonude.Min(p => p.Iznos),
                MinRok = validnePonude.Min(p => p.RokIsporukeDana),
                MaxGarancija = validnePonude.Max(p => p.GarancijaMjeseci)
            };
        }

        private decimal IzracunajUkupanSkor(Ponuda ponuda, List<Ponuda> svePonude,
                                            Tender tender, ReferentneVrijednosti referentne)
        {
            decimal skor = IzracunajKomparativniSkor(ponuda, svePonude);
            skor += IzracunajSkorPoKriterijima(ponuda, tender, referentne);
            return skor;
        }

        private decimal IzracunajKomparativniSkor(Ponuda ponuda, List<Ponuda> svePonude)
        {
            const decimal BONUS_ZA_POVOLJNIJU = 0.5m;
            const decimal PENALIZACIJA_ZA_SKUPLJU = 0.3m;

            decimal skor = 0;

            foreach (var drugaPonuda in svePonude)
            {
                if (JeIstaPonuda(ponuda, drugaPonuda) || drugaPonuda == null)
                    continue;

                if (ponuda.Iznos < drugaPonuda.Iznos)
                    skor += BONUS_ZA_POVOLJNIJU;
                else
                    skor -= PENALIZACIJA_ZA_SKUPLJU;
            }

            return skor;
        }

        private bool JeIstaPonuda(Ponuda ponuda1, Ponuda ponuda2)
        {
            return ponuda1 == ponuda2;
        }

        private decimal IzracunajSkorPoKriterijima(Ponuda ponuda, Tender tender,
                                                   ReferentneVrijednosti referentne)
        {
            decimal skor = 0;

            foreach (var kriterij in tender.Kriteriji)
            {
                if (!JeValidanKriterij(kriterij))
                    continue;

                skor += IzracunajSkorZaKriterij(ponuda, kriterij, referentne);
            }

            return skor;
        }

        private bool JeValidanKriterij(Kriterij kriterij)
        {
            return kriterij != null && kriterij.Tezina > 0;
        }

        private decimal IzracunajSkorZaKriterij(Ponuda ponuda, Kriterij kriterij,
                                                ReferentneVrijednosti referentne)
        {
            if (kriterij.Tip.Equals(TipKriterija.Cijena))
                return IzracunajSkorZaCijenu(ponuda, kriterij, referentne);

            if (kriterij.Tip.Equals(TipKriterija.RokIsporuke))
                return IzracunajSkorZaRok(ponuda, kriterij, referentne);

            if (kriterij.Tip.Equals(TipKriterija.Garancija))
                return IzracunajSkorZaGaranciju(ponuda, kriterij, referentne);

            return 0;
        }

        private decimal IzracunajSkorZaCijenu(Ponuda ponuda, Kriterij kriterij,
                                              ReferentneVrijednosti referentne)
        {
            return (referentne.MinCijena / ponuda.Iznos) * kriterij.Tezina;
        }

        private decimal IzracunajSkorZaRok(Ponuda ponuda, Kriterij kriterij,
                                           ReferentneVrijednosti referentne)
        {
            return ((decimal)referentne.MinRok / ponuda.RokIsporukeDana) * kriterij.Tezina;
        }

        private decimal IzracunajSkorZaGaranciju(Ponuda ponuda, Kriterij kriterij,
                                                 ReferentneVrijednosti referentne)
        {
            return ((decimal)ponuda.GarancijaMjeseci / referentne.MaxGarancija) * kriterij.Tezina;
        }

        private List<(Ponuda ponuda, decimal skor)> SortirajPoSkoru(
            List<(Ponuda ponuda, decimal skor)> rezultat)
        {
            return rezultat.OrderByDescending(r => r.skor).ToList();
        }

        // Nova klasa za čuvanje referentnih vrijednosti
        private class ReferentneVrijednosti
        {
            public decimal MinCijena { get; set; }
            public int MinRok { get; set; }
            public int MaxGarancija { get; set; }
        }
 
        public decimal DajOcjenuZaPonudu(Ponuda p)
            {
                var rangirane = RangirajPonude(p.TenderId);
                var mojaPonuda = rangirane.FirstOrDefault(pon => pon.ponuda.Id == p.Id);

            if (mojaPonuda == default)
                return 0;
                
                return mojaPonuda.skor;
            }
        }
}
