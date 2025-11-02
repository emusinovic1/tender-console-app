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
            if (Tenderi.Any())
                TenderId = Tenderi.Max(t => t.Id) + 1;
        }

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
