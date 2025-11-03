using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    internal class AuthService : IAuthService
    {
        private readonly DbClass _db;

        public AuthService(DbClass dbClass)
        {
            _db = dbClass;
        }
        private string GenerisiHash(string lozinka)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(lozinka));
                return Convert.ToBase64String(bytes);
            }
        }
        public Korisnik Prijava(string email, string lozinka)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email je obavezan");

            if (string.IsNullOrWhiteSpace(lozinka))
                throw new ArgumentException("Lozinka je obavezna");

            var korisnik = _db.DohvatiKorisnikaPoEmailu(email);

            if (korisnik == null)
                throw new Exception("Pogrešan email ili lozinka");

            string lozinkaHash = GenerisiHash(lozinka);

            if (korisnik.LozinkaHash != lozinkaHash)
                throw new Exception("Pogrešan email ili lozinka");

            return korisnik;
        }
        public void RegistracijaFirme(string nazivFirme, string pib, string adresa, string emailFirme, string telefon,
                                      string imeKorisnika, string emailKorisnika, string lozinka)
        {

            if (string.IsNullOrWhiteSpace(nazivFirme))
                throw new ArgumentException("Naziv firme je obavezan");

            if (nazivFirme.Length < 3)
                throw new ArgumentException("Naziv firme mora imati minimum 3 karaktera");

            if (string.IsNullOrWhiteSpace(pib))
                throw new ArgumentException("PIB je obavezan");

            if (pib.Length != 9 || !pib.All(char.IsDigit))
                throw new ArgumentException("PIB mora imati tačno 9 cifara");

            if (_db.PIBPostoji(pib))
                throw new Exception("Firma sa ovim PIB-om već postoji u sistemu");

            if (string.IsNullOrWhiteSpace(emailFirme))
                throw new ArgumentException("Email firme je obavezan");

            if (!emailFirme.Contains("@") || !emailFirme.Contains("."))
                throw new ArgumentException("Nevažeći format email adrese firme");

            if (string.IsNullOrWhiteSpace(imeKorisnika))
                throw new ArgumentException("Ime korisnika je obavezno");

            if(!imeKorisnika.All(char.IsAsciiLetter) && imeKorisnika.Count(c => c == '-') != 1)
                throw new Exception("Nevažeći format imena korisnika");

            if (_db.DohvatiKorisnikaPoEmailu(emailKorisnika) != null)
                throw new Exception("Korisnik sa ovim emailom već postoji");

            if(!ValidanEmailFormat(emailKorisnika))
                throw new ArgumentException("Nevažeći format email adrese korisnika");

            if (string.IsNullOrWhiteSpace(lozinka) || lozinka.Length < 6)
                throw new ArgumentException("Lozinka mora imati minimum 6 karaktera");

            if (!lozinka.Any(char.IsDigit))
                throw new ArgumentException("Lozinka mora sadržavati bar jedan broj");

            //kreira se firma ako je sve ok plus korisnika
            var firma = new Firma
            {
                Naziv = nazivFirme,
                PIB = pib,
                Adresa = adresa,
                Email = emailFirme,
                Telefon = telefon
            };
            _db.DodajFirmu(firma);

            var korisnik = new Korisnik
            {
                Ime = imeKorisnika,
                Email = emailKorisnika,
                LozinkaHash = GenerisiHash(lozinka),
                Uloga = Uloga.Firma,
                FirmaId = firma.Id
            };
            _db.DodajKorisnika(korisnik);

            Console.WriteLine($"Firma '{firma.Naziv}' i korisnik '{korisnik.Ime}' uspješno registrovani!");
        }

        public void PromijeniLozinku(int korisnikId, string staraLozinka, string novaLozinka)
        {
            var korisnik = _db.DohvatiKorisnika(korisnikId);

            if (korisnik == null)
                throw new Exception("Korisnik ne postoji");

            string staraLozinkaHash = GenerisiHash(staraLozinka);
            if (korisnik.LozinkaHash != staraLozinkaHash)
                throw new Exception("Stara lozinka nije tačna");

            if (string.IsNullOrWhiteSpace(novaLozinka) || novaLozinka.Length < 6)
                throw new ArgumentException("Nova lozinka mora imati minimum 6 karaktera");

            if (!novaLozinka.Any(char.IsDigit))
                throw new ArgumentException("Nova lozinka mora sadržavati bar jedan broj");

            if (staraLozinka == novaLozinka)
                throw new ArgumentException("Nova lozinka ne može biti ista kao stara");

            korisnik.LozinkaHash = GenerisiHash(novaLozinka);
            _db.AzurirajKorisnika(korisnik);

            Console.WriteLine("Lozinka uspješno promijenjena!");
        }

        public bool EmailPostoji(string email)
        {
            return _db.DohvatiKorisnikaPoEmailu(email) != null;
        }
        public bool ValidanEmailFormat(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            if (email.Length < 5 || email.Length > 254)
                return false;

            if (!email.Contains("@") || !email.Contains("."))
                return false;

            int atIndex = email.IndexOf('@');
            if (atIndex == 0 || atIndex != email.LastIndexOf('@'))
                return false;

            int dotIndex = email.LastIndexOf('.');
            if (dotIndex <= atIndex + 1 || dotIndex == email.Length - 1)
                return false;

            char[] nevalidni = { ' ', ',', ';', ':', '[', ']', '(', ')', '<', '>' };
            for (int i = 0; i < nevalidni.Length; i++)
            {
                if (email.Contains(nevalidni[i]))
                    return false;
            }

            return true;
        }

    }
}
