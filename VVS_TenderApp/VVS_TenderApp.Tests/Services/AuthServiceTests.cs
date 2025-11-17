using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Services;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;

namespace VVS_TenderApp.Tests.Services
{
    [TestClass]
    public class AuthServiceTests
    {
        private Mock<DbClass> _mockDb;
        private AuthService _service;

        public TestContext TestContext { get; set; }

        public static IEnumerable<object[]> NevalidniEmailoviIzCsv()
        {
            var lines = File.ReadAllLines(@"Data\NevalidniEmailovi.csv")
                            .Skip(1);

            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return new object[] { line.Trim() };
                }
            }
        }

        [TestMethod]
        public void Test_DaLiCsvPostoji()
        {
            var path = @"Data\NevalidniEmailovi.csv";
            var exists = File.Exists(path);

            Console.WriteLine($"Path: {path}");
            Console.WriteLine($"Exists: {exists}");
            Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");

            Assert.IsTrue(exists, $"CSV fajl ne postoji na: {path}");
        }


        [TestInitialize]
        public void Setup()
        {
            _mockDb = new Mock<DbClass>();
            _service = new AuthService(_mockDb.Object);
        }

        private Korisnik CreateDefaultKorisnik(
          int id = 1,
          string email = "test@example.com",
          string lozinkaHash = null)
        {
            if (lozinkaHash == null)
            {
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes("test123"));
                    lozinkaHash = Convert.ToBase64String(bytes);
                }
            }

            return new Korisnik
            {
                Id = id,
                Ime = "Test",
                Prezime = "Korisnik",
                Email = email,
                LozinkaHash = lozinkaHash,
                Uloga = Uloga.Firma,
                FirmaId = 1
            };
        }

        //METODA PRIJAVA

        [TestMethod]
        public void Prijava_ValidniPodaci_VracaKorisnika()
        {
            //podaci
            string email = "test@example.com";
            string lozinka = "test123";
            var korisnik = CreateDefaultKorisnik(email: email);

            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(email))
                   .Returns(korisnik);

            var rezultat = _service.Prijava(email, lozinka);

            Assert.IsNotNull(rezultat);
            Assert.AreEqual(email, rezultat.Email);
            Assert.AreEqual(korisnik.Id, rezultat.Id);
        }

        //prazan email
        [TestMethod]
        public void Prijava_PrazanEmail_BacaArgumentException()
        {
            string email = "";
            string lozinka = "test123";

            var exception = Assert.ThrowsException<ArgumentException>(() =>
                _service.Prijava(email, lozinka));

            Assert.AreEqual("Email je obavezan", exception.Message);
        }

        [TestMethod]
        public void Prijava_NullEmail_BacaArgumentException()
        {
            string email = null;
            string lozinka = "test123";

            var exception = Assert.ThrowsException<ArgumentException>(() =>
                _service.Prijava(email, lozinka));

            Assert.AreEqual("Email je obavezan", exception.Message);
        }

        
        [TestMethod]
        public void Prijava_WhitespaceEmail_BacaArgumentException()
        {
            string email = "   ";
            string lozinka = "test123";

            var exception = Assert.ThrowsException<ArgumentException>(() =>
                _service.Prijava(email, lozinka));

            Assert.AreEqual("Email je obavezan", exception.Message);
        }

        [TestMethod]
        public void Prijava_PraznaLozinka_BacaArgumentException()
        {
            string email = "test@example.com";
            string lozinka = "";

            var exception = Assert.ThrowsException<ArgumentException>(() =>
                _service.Prijava(email, lozinka));

            Assert.AreEqual("Lozinka je obavezna", exception.Message);
        }

        [TestMethod]
        public void Prijava_KorisnikNePostoji_BacaException()
        {
            string email = "nepostojeci@example.com";
            string lozinka = "test123";

            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(email))
                   .Returns((Korisnik)null);

            var exception = Assert.ThrowsException<Exception>(() =>
                _service.Prijava(email, lozinka));

            Assert.AreEqual("Pogrešan email ili lozinka", exception.Message);
        }

        [TestMethod]
        public void Prijava_PogresnaLozinka_BacaException()
        {
            string email = "test@example.com";
            string lozinka = "pogresna123";
            var korisnik = CreateDefaultKorisnik(email: email);

            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(email))
                   .Returns(korisnik);

            var exception = Assert.ThrowsException<Exception>(() =>
                _service.Prijava(email, lozinka));

            Assert.AreEqual("Pogrešan email ili lozinka", exception.Message);
        }

        //METODA REGISTRACIJA FIRME

        [TestMethod]
        public void RegistracijaFirme_ValidniPodaci_KreiraFirmuIKorisnika()
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);
            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(emailKorisnika))
                   .Returns((Korisnik)null);

            _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                       imeKorisnika, emailKorisnika, lozinka);

            
            /*_mockDb.Verify(d => d.DodajFirmu(It.Is<Firma>(f =>
                f.Naziv == nazivFirme &&
                f.PIB == pib &&
                f.Adresa == adresa &&
                f.Email == emailFirme &&
                f.Telefon == telefon)),
                Times.Once); */
            _mockDb.Verify(d => d.DodajFirmu(It.IsAny<Firma>()), Times.Once);

            _mockDb.Verify(d => d.DodajKorisnika(It.Is<Korisnik>(k =>
                k.Ime == imeKorisnika &&
                k.Email == emailKorisnika &&
                k.Uloga == Uloga.Firma)),
                Times.Once);
        }

        [DataTestMethod]
        [DataRow("", "Prazan naziv")] //DATA DRIVEN
        [DataRow(null, "Null naziv")]
        [DataRow("AB", "Prekratak naziv")]
        public void RegistracijaFirme_NevalidanNazivFirme_BacaArgumentException(
            string nazivFirme, string opis)
        {
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";

            Assert.ThrowsException<ArgumentException>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka),
                $"Trebao baciti exception za: {opis}");
        }

        [TestMethod]
        public void RegistracijaFirme_NazivTacno3Karaktera_Uspjesno()
        {
            string nazivFirme = "ABC";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";
            string pib = "123456789";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);
            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(emailKorisnika))
                   .Returns((Korisnik)null);

            //ne bi trebao bit izuzetak
            _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                       imeKorisnika, emailKorisnika, lozinka);

            _mockDb.Verify(d => d.DodajFirmu(It.IsAny<Firma>()), Times.Once);
        }

        [DataTestMethod]
        [DataRow("", "Prazan PIB")]
        [DataRow(null, "Null PIB")]
        [DataRow("12345678", "Prekratak PIB - 8 cifara")]
        [DataRow("12345678a", "PIB sa slovom")]
        public void RegistracijaFirme_NevalidanPIB_BacaArgumentException(
            string pib, string opis)
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";

            Assert.ThrowsException<ArgumentException>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka),
                $"Trebao baciti exception za: {opis}");
        }

        [TestMethod]
        public void RegistracijaFirme_PIBVecPostoji_BacaException()
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(true);

            var exception = Assert.ThrowsException<Exception>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka));

            Assert.AreEqual("Firma sa ovim PIB-om već postoji u sistemu", exception.Message);
        }

        [DataTestMethod]
        [DataRow("", "Prazan email")]
        [DataRow("test", "Bez @ i .")]
        [DataRow("test@", "Bez domena")]
        public void RegistracijaFirme_NevalidanEmailFirme_BacaArgumentException(
            string emailFirme, string opis)
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);

            Assert.ThrowsException<ArgumentException>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka),
                $"Trebao baciti exception za: {opis}");
        }

        [TestMethod]
        public void RegistracijaFirme_PraznoImeKorisnika_BacaArgumentException()
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);

            var exception = Assert.ThrowsException<ArgumentException>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka));

            Assert.AreEqual("Ime korisnika je obavezno", exception.Message);
        }

        [DataTestMethod]
        [DataRow("Marko123", "Ime sa brojevima")]
        [DataRow("Marko Marić", "Ime sa spaceom")]
        [DataRow("Marko-Pero-Ivan", "Ime sa više od jedne crtice")]
        public void RegistracijaFirme_NevalidanFormatImenaKorisnika_BacaException(
            string imeKorisnika, string opis)
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);
            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(emailKorisnika))
                   .Returns((Korisnik)null);

            Assert.ThrowsException<Exception>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka),
                $"Trebao baciti exception za: {opis}");
        }

        [TestMethod]
        public void RegistracijaFirme_ValidnoImeSaCrticom_Uspjesno()
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko-Pero";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "marko123";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);
            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(emailKorisnika))
                   .Returns((Korisnik)null);

            _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                       imeKorisnika, emailKorisnika, lozinka);

            _mockDb.Verify(d => d.DodajKorisnika(It.Is<Korisnik>(k =>
                k.Ime == imeKorisnika)), Times.Once);
        }

        [TestMethod]
        public void RegistracijaFirme_EmailKorisnikaVecPostoji_BacaException()
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "postojeci@nova.ba";
            string lozinka = "marko123";

            var postojeciKorisnik = CreateDefaultKorisnik(email: emailKorisnika);

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);
            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(emailKorisnika))
                   .Returns(postojeciKorisnik);

            var exception = Assert.ThrowsException<Exception>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka));

            Assert.AreEqual("Korisnik sa ovim emailom već postoji", exception.Message);
        }

        [DataTestMethod]
        [DynamicData(nameof(NevalidniEmailoviIzCsv), DynamicDataSourceType.Method)]
        public void RegistracijaFirme_NevalidanEmailKorisnikaIzCsv_BacaArgumentException(
            string emailKorisnika)
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string lozinka = "marko123";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);
            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(emailKorisnika))
                   .Returns((Korisnik)null);

            Assert.ThrowsException<ArgumentException>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka),
                $"Email '{emailKorisnika}' trebao biti nevalidan");
        }

        [DataTestMethod]
        [DataRow("", "Prazna lozinka")]
        [DataRow(null, "Null lozinka")]
        [DataRow("12345", "Prekratka lozinka - 5 karaktera")]
        [DataRow("abcdef", "Lozinka bez broja")]
        [DataRow("abcdefghij", "Lozinka bez broja - duža")]
        public void RegistracijaFirme_NevalidnaLozinka_BacaArgumentException(
            string lozinka, string opis)
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "marko@nova.ba";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);
            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(emailKorisnika))
                   .Returns((Korisnik)null);

            Assert.ThrowsException<ArgumentException>(() =>
                _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                           imeKorisnika, emailKorisnika, lozinka),
                $"Trebao baciti exception za: {opis}");
        }

        [TestMethod]
        public void RegistracijaFirme_LozinkaTacno6KarakteraSaBrojem_Uspjesno()
        {
            string nazivFirme = "Nova Firma d.o.o.";
            string pib = "123456789";
            string adresa = "Adresa 123";
            string emailFirme = "firma@nova.ba";
            string telefon = "+387 33 123 456";
            string imeKorisnika = "Marko";
            string emailKorisnika = "marko@nova.ba";
            string lozinka = "pass12";

            _mockDb.Setup(d => d.PIBPostoji(pib)).Returns(false);
            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(emailKorisnika))
                   .Returns((Korisnik)null);

            _service.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon,
                                       imeKorisnika, emailKorisnika, lozinka);

            _mockDb.Verify(d => d.DodajKorisnika(It.IsAny<Korisnik>()), Times.Once);
        }

        //METODA PROMIJENI LOZINKU
        [TestMethod]
        public void PromijeniLozinku_ValidniPodaci_AzuriraLozinku()
        {
            int korisnikId = 1;
            string staraLozinka = "test123";
            string novaLozinka = "nova123";

            var korisnik = CreateDefaultKorisnik(id: korisnikId);

            _mockDb.Setup(d => d.DohvatiKorisnika(korisnikId))
                   .Returns(korisnik);

            _service.PromijeniLozinku(korisnikId, staraLozinka, novaLozinka);

            _mockDb.Verify(d => d.AzurirajKorisnika(It.Is<Korisnik>(k =>
                k.Id == korisnikId &&
                k.LozinkaHash != null)),
                Times.Once);
        }

        [TestMethod]
        public void PromijeniLozinku_KorisnikNePostoji_BacaException()
        {
            int korisnikId = 999;
            string staraLozinka = "test123";
            string novaLozinka = "nova123";

            _mockDb.Setup(d => d.DohvatiKorisnika(korisnikId))
                   .Returns((Korisnik)null);

            var exception = Assert.ThrowsException<Exception>(() =>
                _service.PromijeniLozinku(korisnikId, staraLozinka, novaLozinka));

            Assert.AreEqual("Korisnik ne postoji", exception.Message);
        }

        [TestMethod]
        public void PromijeniLozinku_PogresnaStaraLozinka_BacaException()
        {
            int korisnikId = 1;
            string staraLozinka = "pogresna123";
            string novaLozinka = "nova123";

            var korisnik = CreateDefaultKorisnik(id: korisnikId);

            _mockDb.Setup(d => d.DohvatiKorisnika(korisnikId))
                   .Returns(korisnik);

            var exception = Assert.ThrowsException<Exception>(() =>
                _service.PromijeniLozinku(korisnikId, staraLozinka, novaLozinka));

            Assert.AreEqual("Stara lozinka nije tačna", exception.Message);
        }

        [DataTestMethod]
        [DataRow("", "Prazna nova lozinka")]
        [DataRow(null, "Null nova lozinka")]
        [DataRow("12345", "Prekratka nova lozinka")]
        [DataRow("abcdef", "Nova lozinka bez broja")]
        public void PromijeniLozinku_NevalidnaNovaLozinka_BacaArgumentException(
            string novaLozinka, string opis)
        {
            int korisnikId = 1;
            string staraLozinka = "test123";

            var korisnik = CreateDefaultKorisnik(id: korisnikId);

            _mockDb.Setup(d => d.DohvatiKorisnika(korisnikId))
                   .Returns(korisnik);

            Assert.ThrowsException<ArgumentException>(() =>
                _service.PromijeniLozinku(korisnikId, staraLozinka, novaLozinka),
                $"Trebao baciti exception za: {opis}");
        }

        [TestMethod]
        public void PromijeniLozinku_NovaLozinkaIstaKaoStara_BacaArgumentException()
        {
            int korisnikId = 1;
            string staraLozinka = "test123";
            string novaLozinka = "test123"; 

            var korisnik = CreateDefaultKorisnik(id: korisnikId);

            _mockDb.Setup(d => d.DohvatiKorisnika(korisnikId))
                   .Returns(korisnik);

            var exception = Assert.ThrowsException<ArgumentException>(() =>
                _service.PromijeniLozinku(korisnikId, staraLozinka, novaLozinka));

            Assert.AreEqual("Nova lozinka ne može biti ista kao stara", exception.Message);
        }

        //METODA EMail postoji
        [TestMethod]
        public void EmailPostoji_EmailPostoji_VracaTrue()
        {
            string email = "postojeci@example.com";
            var korisnik = CreateDefaultKorisnik(email: email);

            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(email))
                   .Returns(korisnik);

            var rezultat = _service.EmailPostoji(email);

            Assert.IsTrue(rezultat);
        }

        [TestMethod]
        public void EmailPostoji_EmailNePostoji_VracaFalse()
        {
            string email = "nepostojeci@example.com";

            _mockDb.Setup(d => d.DohvatiKorisnikaPoEmailu(email))
                   .Returns((Korisnik)null);

            var rezultat = _service.EmailPostoji(email);

            Assert.IsFalse(rezultat);
        }

        //metoda VALIDAN EMAIL FORMAT
        [DataTestMethod]
        [DataRow("test@example.com", "Osnovni validan email")]
        [DataRow("user.name@example.com", "Email sa tačkom")]
        [DataRow("a@b.co", "Minimalni validan email")]
        public void ValidanEmailFormat_ValidniEmail_VracaTrue(string email, string opis)
        {
            var rezultat = _service.ValidanEmailFormat(email);

            Assert.IsTrue(rezultat, $"Email '{email}' trebao biti validan: {opis}");
        }

        [DataTestMethod]
        [DataRow("", "Prazan email")]
        [DataRow(null, "Null email")]
        [DataRow("   ", "Whitespace email")]
        [DataRow("test", "Bez @ i .")]
        [DataRow("test@", "Bez domena")]
        [DataRow("@example.com", "Bez lokalnog dijela")]
        [DataRow("test@@example.com", "Dupli @")]
        [DataRow("test@example", "Bez tačke u domenu")]
        [DataRow("test.example.com", "Bez @")]
        [DataRow("test@example.", "Tačka na kraju")]
        [DataRow("test @example.com", "Sa spaceom")]
        [DataRow("test,user@example.com", "Sa zarezom")]
        [DataRow("test;user@example.com", "Sa tačka-zarezom")]
        public void ValidanEmailFormat_NevalidanEmail_VracaFalse(string email, string opis)
        {
            var rezultat = _service.ValidanEmailFormat(email);

            Assert.IsFalse(rezultat, $"Email '{email}' trebao biti nevalidan: {opis}");
        }

        [TestMethod]
        public void ValidanEmailFormat_EmailSa5Karaktera_VracaTrue()
        {
            string email = "a@b.c";

            var rezultat = _service.ValidanEmailFormat(email);

            Assert.IsTrue(rezultat);
        }

        [TestMethod]
        public void ValidanEmailFormat_EmailSa4Karaktera_VracaFalse()
        {
            string email = "a@bc";

            var rezultat = _service.ValidanEmailFormat(email);

            Assert.IsFalse(rezultat);
        }

        [TestMethod]
        public void ValidanEmailFormat_EmailSa254Karaktera_VracaTrue()
        {
            string lokalni = new string('a', 240);
            string email = lokalni + "@example.com";

            var rezultat = _service.ValidanEmailFormat(email);

            Assert.IsTrue(rezultat);
        }

        [TestMethod]
        public void ValidanEmailFormat_EmailSa255Karaktera_VracaFalse()
        {
            string lokalni = new string('a', 243);
            string email = lokalni + "@example.com";

            var rezultat = _service.ValidanEmailFormat(email);

            Assert.IsFalse(rezultat);
        }

        [DataTestMethod]
        [DataRow("test[user]@example.com", "Sa uglatim zagradama")]
        [DataRow("test(user)@example.com", "Sa običnim zagradama")]
        [DataRow("test<user>@example.com", "Sa znakovima veće/manje")]
        [DataRow("test:user@example.com", "Sa dvotačkom")]
        public void ValidanEmailFormat_EmailSaNevalidnimKarakterima_VracaFalse(
            string email, string opis)
        {
            var rezultat = _service.ValidanEmailFormat(email);

            Assert.IsFalse(rezultat, $"Email '{email}' trebao biti nevalidan: {opis}");
        }
    }
}

