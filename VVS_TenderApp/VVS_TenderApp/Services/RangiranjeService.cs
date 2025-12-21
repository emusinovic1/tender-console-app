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

                if (!ponude.Any() || !tender.Kriteriji.Any())
                    return new List<(Ponuda, decimal)>();

                var rezultat = new List<(Ponuda, decimal skor)>();

                // referentne vrijednosti za normalizaciju
                var minCijena = ponude.Min(p => p.Iznos);
                var minRok = ponude.Min(p => p.RokIsporukeDana);
                var maxGarancija = ponude.Max(p => p.GarancijaMjeseci);

               

                foreach (var p in ponude)
                {
                //ovaj dio dodan naknadno da bi se povecala kompleksnost 
                    decimal ukupno = 0;
                if (p == null || p.Iznos <= 0 || p.RokIsporukeDana < 0 || p.GarancijaMjeseci <= 0)
                    continue;

                if (p.RokIsporukeDana < 0)  // +1
                    continue;
                foreach (var o in ponude)
                {
                    if (p != o) // Ne uspoređujte istu ponudu sa samom sobom
                    {
                        if (p.Iznos < o.Iznos)
                        {
                            ukupno += 0.5m; // Dodajte bodove za povoljniju cijenu
                        }
                        else
                        {
                            ukupno -= 0.3m; // Oduzmite bodove za skuplju cijenu
                        }
                    }
                }
                //do ovdje

                foreach (var k in tender.Kriteriji)

                    {
                    if (k == null || k.Tezina <= 0)  // +1 if, +1 ||
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

            // Sortiraj KOPIJU
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

            return sortiranRezultat; // vrati sortiranu kopiju
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
