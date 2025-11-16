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
                    decimal ukupno = 0;

                    foreach (var k in tender.Kriteriji)
                    {
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
