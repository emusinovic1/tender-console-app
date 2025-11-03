using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Data
{
    public class DbClass
    {
        public List<Korisnik> Korisnici { get; set; }
        public List<Firma> Firme { get; set; }
        public List<Tender> Tenderi { get; set; }
        public List<Ponuda> Ponude { get; set; }

        //ovo da imamo za id-eve jer se nece nista automatski bez baze povecavati
        private int TenderId = 1;
        private int FirmaId = 1;
        private int KorisnikId = 1;
        private int PonudaId = 1;
        public DbClass() {

            Tenderi = new List<Tender>();
            Firme = new List<Firma>();
            Korisnici = new List<Korisnik>();
            Ponude = new List<Ponuda>();
            KreirajAdmina();
            // vjerovatno mozemo bez ove linije
            // svuda gdje se kreira se i inkrementira
            if (Tenderi.Any())
                TenderId = Tenderi.Max(t => t.Id) + 1;

            KreirajPrimjerneFirmeIKorisnike();
            KreirajPrimjerneTendere();
            KreirajPrimjernePonude();
        }

        // INICIJALNI PODACI

        private void KreirajAdmina()
        {
            var admin = new Korisnik
            {
                Id = 1,
                Ime = "Administrator",
                Prezime = "Administrator",
                Email = "admin@tender.ba",
                LozinkaHash = GenerisiHash("admin123"),
                Uloga = Uloga.Admin,
                FirmaId = null
            };
            Korisnici.Add(admin);
        }

        private void KreirajPrimjerneFirmeIKorisnike()
        {
            var firma1 = new Firma
            {
                Id = FirmaId++,
                Naziv = "TechNova d.o.o.",
                PIB = "123456789",
                Adresa = "Zmaja od Bosne 12, Sarajevo",
                Email = "info@technova.ba",
                Telefon = "+387 33 555 777"
            };

            var firma2 = new Firma
            {
                Id = FirmaId++,
                Naziv = "BuildPro d.o.o.",
                PIB = "987654321",
                Adresa = "Kneza Miloša 45, Banja Luka",
                Email = "kontakt@buildpro.ba",
                Telefon = "+387 51 444 999"
            };

            var firma3 = new Firma
            {
                Id = FirmaId++,
                Naziv = "Atlas d.o.o.",
                PIB = "123123123",
                Adresa = "Trg djece Sarajeva 1, Sarajevo",
                Email = "info@atlas.ba",
                Telefon = "+387 33 111 222"
            };

            Firme.AddRange(new[] { firma1, firma2, firma3 });

            var korisnik1 = new Korisnik
            {
                Id = KorisnikId++,
                Ime = "Marko",
                Prezime = "Ivić",
                Email = "marko@technova.ba",
                LozinkaHash = GenerisiHash("marko123"),
                Uloga = Uloga.Firma,
                FirmaId = firma1.Id
            };

            var korisnik2 = new Korisnik
            {
                Id = KorisnikId++,
                Ime = "Ivana",
                Prezime = "Petrović",
                Email = "ivana@buildpro.ba",
                LozinkaHash = GenerisiHash("ivana123"),
                Uloga = Uloga.Firma,
                FirmaId = firma2.Id
            };

            var korisnik3 = new Korisnik
            {
                Id = KorisnikId++,
                Ime = "Selma",
                Prezime = "Begović",
                Email = "selma@atlas.ba",
                LozinkaHash = GenerisiHash("selma123"),
                Uloga = Uloga.Firma,
                FirmaId = firma3.Id
            };

            Korisnici.AddRange(new[] { korisnik1, korisnik2, korisnik3 });
        }

        private void KreirajPrimjerneTendere()
        {
            var tender1 = new Tender
            {
                Id = TenderId++,
                FirmaId = Firme.First().Id,
                Naziv = "Nabavka računarske opreme",
                Opis = "Tender za nabavku laptopa i monitora za IT odjel.",
                DatumObjave = DateTime.Now.AddDays(-5),
                RokZaPrijavu = DateTime.Now.AddDays(10),
                ProcijenjenaVrijednost = 25000m,
                Status = StatusTendera.Otvoren,
                Kriteriji = new List<Kriterij>
                {
                    new Kriterij {Tip = TipKriterija.Cijena, Tezina = 0.5M},
                    new Kriterij {Tip = TipKriterija.RokIsporuke, Tezina = 0.3M},
                    new Kriterij {Tip = TipKriterija.Garancija, Tezina = 0.2M}
                }
            };

            var tender2 = new Tender
            {
                Id = TenderId++,
                FirmaId = Firme.Last().Id,
                Naziv = "Izgradnja poslovnog prostora",
                Opis = "Tender za izvođenje građevinskih radova na novom objektu.",
                DatumObjave = DateTime.Now.AddDays(-20),
                RokZaPrijavu = DateTime.Now.AddDays(4),
                ProcijenjenaVrijednost = 120000m,
                Status = StatusTendera.Otvoren,
                Kriteriji = new List<Kriterij>
                {
                    new Kriterij {Tip = TipKriterija.Cijena, Tezina = 0.6M},
                    new Kriterij {Tip = TipKriterija.RokIsporuke, Tezina = 0.2M},
                    new Kriterij {Tip = TipKriterija.Garancija, Tezina = 0.2M}
                }
            };

            Tenderi.AddRange(new[] { tender1, tender2 });
        }

        private void KreirajPrimjernePonude()
        {
            var ponuda1 = new Ponuda
            {
                Id = PonudaId++,
                TenderId = Tenderi[0].Id,
                FirmaId = Firme[1].Id,
                Iznos = 24000m,
                DatumSlanja = DateTime.Now.AddDays(-2),
                Status = StatusPonude.NaCekanju,
                RokIsporukeDana = 5,
                GarancijaMjeseci = 30,
          
            };

            var ponuda1_1 = new Ponuda
            {
                Id = PonudaId++,
                TenderId = Tenderi[0].Id,
                FirmaId = Firme[2].Id,
                Iznos = 25000m,
                DatumSlanja = DateTime.Now.AddDays(-1),
                Status = StatusPonude.NaCekanju,
                RokIsporukeDana = 10,
                GarancijaMjeseci = 24
            };

            var ponuda2 = new Ponuda
            {
                Id = PonudaId++,
                TenderId = Tenderi[1].Id,
                FirmaId = Firme[0].Id,
                Iznos = 118000m,
                DatumSlanja = DateTime.Now.AddDays(-3),
                Status = StatusPonude.NaCekanju,
                RokIsporukeDana = 3,
                GarancijaMjeseci = 30
            };

            Ponude.AddRange(new[] { ponuda1, ponuda2, ponuda1_1 });
        }

        private string GenerisiHash(string lozinka)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(lozinka));
                return Convert.ToBase64String(bytes);
            }
        }
        public void DodajKorisnika(Korisnik korisnik)
        {
            korisnik.Id = KorisnikId++;
            Korisnici.Add(korisnik);
        }

        public Korisnik DohvatiKorisnika(int id)
        {
            return Korisnici.FirstOrDefault(k => k.Id == id);
        }

        public Korisnik DohvatiKorisnikaPoEmailu(string email)
        {
            return Korisnici.FirstOrDefault(k => k.Email == email);
        }
        public void AzurirajKorisnika(Korisnik korisnik)
        {
            var stari = DohvatiKorisnika(korisnik.Id);
            if (stari != null)
            {
                int index = Korisnici.IndexOf(stari);
                Korisnici[index] = korisnik;
            }
        }
        public List<Korisnik> DohvatiSveKorisnike()
        {
            return Korisnici;
        }

        //FIRME
        public void DodajFirmu(Firma firma)
        {
            firma.Id = FirmaId++;
            Firme.Add(firma);
        }
        public Firma DohvatiFirmu(int firmaId)
        {
            return Firme.FirstOrDefault(f => f.Id == firmaId);
        }
        public List<Firma> DohvatiSveFirme()
        {
            return Firme;
        }
        public bool PIBPostoji(string pib)
        {
            return Firme.Any(f => f.PIB == pib);
        }

        //TENDERI
        public void DodajTender(Tender tender)
        {
            tender.Id = TenderId++;
            Tenderi.Add(tender);
        }
        public Tender DohvatiTender(int tenderId)
        {
            return Tenderi.FirstOrDefault(t => t.Id == tenderId);
        }
        public List<Tender> DohvatiSveTendere()
        {
            return Tenderi;
        }
        public List<Tender> DohvatiTenderePoFirmi(int firmaId)
        {
            return Tenderi.Where(t => t.FirmaId == firmaId).ToList();
        }
        public List<Tender> DohvatiAktivneTendere()
        {
            return Tenderi.Where(t => t.Status == StatusTendera.Otvoren).ToList();
        }
        public void AzurirajTender(Tender tender)
        {
            var stari = DohvatiTender(tender.Id);
            if(stari != null)
            {
                int index = Tenderi.IndexOf(stari);
                Tenderi[index] = tender;
            }
        }
        public void ObrisiTender(int tenderId)
        {
            var tender = DohvatiTender(tenderId);
            if (tender != null)
            {
                Tenderi.Remove(tender);
            }
        }
        //PONUDE
        public void DodajPonudu(Ponuda ponuda)
        {
            ponuda.Id = PonudaId++;
            Ponude.Add(ponuda);
        }

        public Ponuda DohvatiPonudu(int id)
        {
            return Ponude.FirstOrDefault(p => p.Id == id);
        }

        public List<Ponuda> DohvatiPonudePoTenderu(int tenderId)
        {
            return Ponude.Where(p => p.TenderId == tenderId).ToList();
        }

        public List<Ponuda> DohvatiPonudePoFirmi(int firmaId)
        {
            return Ponude.Where(p => p.FirmaId == firmaId).ToList();
        }

        public void AzurirajPonudu(Ponuda ponuda)
        {
            var stara = DohvatiPonudu(ponuda.Id);
            if (stara != null)
            {
                int index = Ponude.IndexOf(stara);
                Ponude[index] = ponuda;
            }
        }

        public void ObrisiPonudu(int id)
        {
            var ponuda = DohvatiPonudu(id);
            if (ponuda != null)
            {
                Ponude.Remove(ponuda);
            }
                
        }
    }
}
