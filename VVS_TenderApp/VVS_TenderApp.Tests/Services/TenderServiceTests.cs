using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace VVS_TenderApp.Tests.Services
{
    [TestClass]
    public class TenderServiceTests
    {
        private Mock<DbClass> mockDb;
        private TenderService _tenderService;
        private readonly int ValidFirmaId = 1;
        private Firma _testFirma;

        /*
         ***************************************************************************************+
         
                                            TDD TESTOVI

                    FUNKCIONALNOST - Automatska dodjela tendera po rangiranju ponuda
         
         
         ****************************************************************************************
         */

        [TestInitialize]
        public void Setup()
        {
            mockDb = new Mock<DbClass>();
            _tenderService = new TenderService(mockDb.Object);
            mockDb.Setup(db => db.DohvatiFirmu(ValidFirmaId))
              .Returns(new Firma { Id = ValidFirmaId });
            mockDb.Setup(db => db.DohvatiTenderePoFirmi(ValidFirmaId))
                   .Returns(new List<Tender>());
            _testFirma = new Firma
            {
                Id = 101,
                Naziv = "Test Firma"
            };

        }

        private Tender CreateDefaultTender(
            int id = 1,
            int firmaId = 1,
            StatusTendera status = StatusTendera.Zatvoren)
        {
            return new Tender
            {
                Id = id,
                FirmaId = firmaId,
                Naziv = "Test tender",
                Opis = "Opis test tendera dovoljno dug...",
                DatumObjave = DateTime.Now.AddDays(-10),
                RokZaPrijavu = DateTime.Now.AddDays(-1),
                ProcijenjenaVrijednost = 10000m,
                Status = status,
                Kriteriji = new List<Kriterij>
                {
                    new Kriterij
                    {
                        Tip = TipKriterija.Cijena,
                        Tezina = 1.0m
                    },
                    new Kriterij
                    {
                        Tip = TipKriterija.RokIsporuke,
                        Tezina = 0.0m
                    },
                    new Kriterij
                    {
                        Tip = TipKriterija.Garancija,
                        Tezina = 0.0m
                    }
                }
            };
        }

        private Ponuda CreatePonuda(int id, int tenderId, int firmaId, decimal iznos)
        {
            return new Ponuda
            {
                Id = id,
                TenderId = tenderId,
                FirmaId = firmaId,
                Iznos = iznos,
                RokIsporukeDana = 10,
                GarancijaMjeseci = 12,
                DatumSlanja = DateTime.Now.AddDays(-2),
                Status = StatusPonude.NaCekanju
            };
        }

        // SRETAN PUT: tender zatvoren, ima ponuda, rangiranje vrati listu, pobjednik se postavlja
        [TestMethod]
        public void AutomatskiDodijeliTender_ValidniPodaci_PostavljaPobjednikaIZavrsavaTender()
        {
            int tenderId = 1;
            int firmaNaruciocaId = 1;

            var tender = CreateDefaultTender(id: tenderId, firmaId: firmaNaruciocaId);

            var p1 = CreatePonuda(1, tenderId, 2, 9000m); // skuplja
            var p2 = CreatePonuda(2, tenderId, 3, 8000m); // jeftinija -> pobjednik
            var ponude = new List<Ponuda> { p1, p2 };

            mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tender);
            mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId)).Returns(ponude);
            mockDb.Setup(d => d.DohvatiFirmu(p2.FirmaId))
                   .Returns(new Firma { Id = p2.FirmaId, Naziv = "Pobjednička firma" });

            // RangiranjeService će koristiti isti _db mock, pa mu vraćamo ove vrijednosti
            // Kriterij je samo cijena (Tezina = 1), tako da će ponuda s manjom cijenom biti prva

            var pobjednik = _tenderService.AutomatskiDodijeliTender(tenderId, firmaNaruciocaId);

            Assert.IsNotNull(pobjednik);
            Assert.AreEqual(p2.Id, pobjednik.Id, "Očekujemo da je ponuda sa manjom cijenom pobjednik.");

            mockDb.Verify(d => d.AzurirajTender(It.Is<Tender>(t =>
                t.Id == tenderId && t.Status == StatusTendera.Zavrsen)), Times.Once);

            mockDb.Verify(d => d.AzurirajPonudu(It.Is<Ponuda>(p =>
                p.Id == p2.Id && p.Status == StatusPonude.Prihvacena)), Times.Once);

            mockDb.Verify(d => d.AzurirajPonudu(It.Is<Ponuda>(p =>
                p.Id == p1.Id && p.Status == StatusPonude.Odbijena)), Times.Once);
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_TenderNePostoji_BacaException()
        {
            mockDb.Setup(d => d.DohvatiTender(It.IsAny<int>()))
                   .Returns((Tender)null);

            Assert.ThrowsException<Exception>(() =>
               _tenderService.AutomatskiDodijeliTender(1, 1));
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_TenderNePripadaFirmi_BacaUnauthorizedAccessException()
        {
            var tender = CreateDefaultTender(id: 1, firmaId: 99); // druga firma

            mockDb.Setup(d => d.DohvatiTender(1)).Returns(tender);

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                _tenderService.AutomatskiDodijeliTender(1, 1));
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_TenderNijeZatvoren_BacaException()
        {
            var tender = CreateDefaultTender(status: StatusTendera.Otvoren);

            mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            Assert.ThrowsException<Exception>(() =>
                _tenderService.AutomatskiDodijeliTender(tender.Id, tender.FirmaId));
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_NemaPonuda_BacaException()
        {
            var tender = CreateDefaultTender();

            mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);
            mockDb.Setup(d => d.DohvatiPonudePoTenderu(tender.Id))
                   .Returns(new List<Ponuda>());

            Assert.ThrowsException<Exception>(() =>
                _tenderService.AutomatskiDodijeliTender(tender.Id, tender.FirmaId));
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_NemaRangiranihPonuda_BacaException()
        {
            var tender = CreateDefaultTender();
            tender.Kriteriji.Clear(); // bez kriterija -> RangiranjeService vraća praznu listu

            var p1 = CreatePonuda(1, tender.Id, 2, 9000m);
            var ponude = new List<Ponuda> { p1 };

            mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);
            mockDb.Setup(d => d.DohvatiPonudePoTenderu(tender.Id)).Returns(ponude);

            Assert.ThrowsException<Exception>(() =>
             _tenderService.AutomatskiDodijeliTender(tender.Id, tender.FirmaId));
        }

        /*
         ***************************************************************************************+
         
                                        KRAJ TDD TESTOVA
         
         ****************************************************************************************
         */


        [TestMethod]
        public void ValidirajIKreirajTender_FirmaNePostoji_TrebaBacitiException()
        {
            mockDb.Setup(db => db.DohvatiFirmu(It.IsAny<int>())).Returns((Firma)null);

            var exception = Assert.ThrowsException<Exception>(() =>
            {
                _tenderService.ValidirajIKreirajTender(
                    999,
                    "Tender za most Izgradnja mosta u podrucju Sarajeva rok ce biti objavljen naknadno",
                    "Izgradnja mosta u podrucju Sarajeva rok ce biti objavljen naknadno",
                    DateTime.Now.AddMonths(1),
                    1000000,
                    new List<Kriterij>());
            });

            Assert.AreEqual("Firma ne postoji", exception.Message);
        }

        [TestMethod]
        public void ValidirajIKreirajTender_FirmaPostoji_TenderSeKreira()
        {
            var firma = new Firma { Id = 101, Naziv = "Firma 101" };

            mockDb.Setup(db => db.DohvatiFirmu(101)).Returns(firma);
            mockDb.Setup(db => db.DohvatiTenderePoFirmi(101)).Returns(new List<Tender>());

            _tenderService.ValidirajIKreirajTender(
                101,
                "Tender za most Izgradnja mosta u podrucju Sarajeva rok ce biti objavljen naknadno",
                "Izgradnja mosta u podrucju Sarajeva rok ce biti objavljen naknadno koji ima više od 50 karaktera.",
                DateTime.Now.AddDays(10),
                10000m,
                new List<Kriterij> { new Kriterij { Tezina = 1.0m } }
            );

            mockDb.Verify(db => db.DohvatiFirmu(101), Times.Once());
            mockDb.Verify(db => db.DodajTender(It.IsAny<Tender>()), Times.Once(), "Tender nije dodan.");
        }

       

        [DataTestMethod]
        [DataRow(null, "Naziv tendera je obavezan", DisplayName = "Null naziv")]
        [DataRow("", "Naziv tendera je obavezan", DisplayName = "Prazan naziv")]
        [DataRow("Naziv", "Naziv mora imati minimum 10 karaktera", DisplayName = "Prekratak naziv")]
        [DataRow("Test      ", "Naziv mora imati minimum 10 karaktera", DisplayName = "Razmaci na kraju")]
        [DataRow("     Test", "Naziv mora imati minimum 10 karaktera", DisplayName = "Razmaci na početku")]
        public void ValidirajIKreirajTender_NevalidanNaziv_BacaException(string naziv, string expectedMessage)
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);
            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                _tenderService.ValidirajIKreirajTender(
                    101,
                    naziv,
                    "Ovo je validan opis tendera koji ima više od pedeset karaktera",
                    DateTime.Now.AddDays(10),
                    5000m,
                    kriteriji
                );
            });

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethod]
        public void ValidirajIKreirajTender_ValidanNaziv_TenderKreiran()
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);
            mockDb.Setup(x => x.DohvatiTenderePoFirmi(101)).Returns(new List<Tender>());

            Tender kreiraniTender = null;
            mockDb.Setup(x => x.DodajTender(It.IsAny<Tender>()))
                   .Callback<Tender>(t => kreiraniTender = t);

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var naziv = "Tender za izgradnju mosta";
            var opis = "Ovo je detaljan opis tendera koji ima više od pedeset karaktera";

            _tenderService.ValidirajIKreirajTender(
                101,
                naziv,
                opis,
                DateTime.Now.AddDays(10),
                5000m,
                kriteriji
            );

            Assert.IsNotNull(kreiraniTender);
            Assert.AreEqual(101, kreiraniTender.FirmaId);
            Assert.AreEqual(naziv, kreiraniTender.Naziv);
            Assert.AreEqual(StatusTendera.Otvoren, kreiraniTender.Status);
        }

       

        [DataTestMethod]
        [DynamicData(nameof(NevalidniOpisiData))]
        public void ValidirajIKreirajTender_NevalidanOpis_BacaException(string opis, string expectedMessage)
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                _tenderService.ValidirajIKreirajTender(
                    101,
                    "Validan naziv tendera",
                    opis,
                    DateTime.Now.AddDays(10),
                    5000m,
                    kriteriji
                );
            });

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        public static IEnumerable<object[]> NevalidniOpisiData
        {
            get
            {
                return new List<object[]>
            {
                new object[] { "", "Opis tendera je obavezan" },
                new object[] { null, "Opis tendera je obavezan" },
                new object[] { "     ", "Opis tendera je obavezan" },
                new object[] { "\t\n\r", "Opis tendera je obavezan" },
                new object[] { new string(' ', 100), "Opis tendera je obavezan" },
                new object[] { "Kratak opis", "Opis mora imati minimum 50 karaktera" },
                new object[] { new string('x', 49), "Opis mora imati minimum 50 karaktera" },
                new object[] { "Test              ", "Opis mora imati minimum 50 karaktera" }
            };
            }
        }

        [DataTestMethod]
        [DataRow(50, DisplayName = "50 karaktera - na granici")]
        [DataRow(51, DisplayName = "51 karakter")]
        [DataRow(100, DisplayName = "100 karaktera")]
        public void ValidirajIKreirajTender_ValidanOpis_UspjesnoKreiranje(int duzina)
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);
            mockDb.Setup(x => x.DohvatiTenderePoFirmi(101)).Returns(new List<Tender>());
            mockDb.Setup(x => x.DodajTender(It.IsAny<Tender>()));

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var opis = new string('x', duzina);

            _tenderService.ValidirajIKreirajTender(
                101,
                "Validan naziv tendera",
                opis,
                DateTime.Now.AddDays(10),
                5000m,
                kriteriji
            );

            mockDb.Verify(x => x.DodajTender(It.IsAny<Tender>()), Times.Once);
        }

       

        [DataTestMethod]
        [DataRow(-10, "Rok za prijavu mora biti u budućnosti", DisplayName = "10 dana u prošlosti")]
        [DataRow(-1, "Rok za prijavu mora biti u budućnosti", DisplayName = "1 dan u prošlosti")]
        [DataRow(0, "Rok za prijavu mora biti u budućnosti", DisplayName = "Danas")]
        [DataRow(1, "Rok mora biti minimum 7 dana unaprijed", DisplayName = "1 dan u budućnosti")]
        [DataRow(6, "Rok mora biti minimum 7 dana unaprijed", DisplayName = "6 dana")]
        [DataRow(7, "Rok mora biti minimum 7 dana unaprijed", DisplayName = "7 dana - granica")]
        [DataRow(366, "Rok ne može biti duži od 1 godine", DisplayName = "366 dana")]
        [DataRow(730, "Rok ne može biti duži od 1 godine", DisplayName = "730 dana")]
        public void ValidirajIKreirajTender_NevalidanRok_BacaException(int brojDana, string expectedMessage)
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var rok = DateTime.Now.AddDays(brojDana);

            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                _tenderService.ValidirajIKreirajTender(
                    101,
                    "Validan naziv tendera",
                    "Ovo je validan opis tendera koji ima više od pedeset karaktera",
                    rok,
                    5000m,
                    kriteriji
                );
            });

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [DataTestMethod]
        [DataRow(8, DisplayName = "8 dana")]
        [DataRow(30, DisplayName = "30 dana")]
        [DataRow(180, DisplayName = "180 dana")]
        [DataRow(364, DisplayName = "364 dana")]
        public void ValidirajIKreirajTender_ValidanRok_UspjesnoKreiranje(int brojDana)
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);
            mockDb.Setup(x => x.DohvatiTenderePoFirmi(101)).Returns(new List<Tender>());
            mockDb.Setup(x => x.DodajTender(It.IsAny<Tender>()));

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var rok = DateTime.Now.AddDays(brojDana);

            _tenderService.ValidirajIKreirajTender(
                101,
                "Validan naziv tendera",
                "Ovo je validan opis tendera koji ima više od pedeset karaktera",
                rok,
                5000m,
                kriteriji
            );

            mockDb.Verify(x => x.DodajTender(It.IsAny<Tender>()), Times.Once);
        }

        

        [DataTestMethod]
        [DynamicData(nameof(NevalidneVrijednostiData))]
        public void ValidirajIKreirajTender_NevalidnaVrijednost_BacaException(decimal vrijednost, string expectedMessage)
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                _tenderService.ValidirajIKreirajTender(
                    101,
                    "Validan naziv tendera",
                    "Ovo je validan opis tendera koji ima više od pedeset karaktera",
                    DateTime.Now.AddDays(10),
                    vrijednost,
                    kriteriji
                );
            });

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        public static IEnumerable<object[]> NevalidneVrijednostiData
        {
            get
            {
                return new List<object[]>
            {
                new object[] { -5000m, "Procijenjena vrijednost mora biti veća od 0" },
                new object[] { 0m, "Procijenjena vrijednost mora biti veća od 0" },
                new object[] { 1m, "Minimalna vrijednost tendera je 1000 KM" },
                new object[] { 999m, "Minimalna vrijednost tendera je 1000 KM" },
                new object[] { 999.99m, "Minimalna vrijednost tendera je 1000 KM" },
                new object[] { 10000001m, "Maksimalna vrijednost tendera je 10.000.000 KM" },
                new object[] { 50000000m, "Maksimalna vrijednost tendera je 10.000.000 KM" }
            };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetValidneVrijednosti), DynamicDataSourceType.Method)]
        public void ValidirajIKreirajTender_ValidnaVrijednost_UspjesnoKreiranje(decimal vrijednost, string opis)
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);
            mockDb.Setup(x => x.DohvatiTenderePoFirmi(101)).Returns(new List<Tender>());
            mockDb.Setup(x => x.DodajTender(It.IsAny<Tender>()));

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            _tenderService.ValidirajIKreirajTender(
                101,
                "Validan naziv tendera",
                "Ovo je validan opis tendera koji ima više od pedeset karaktera",
                DateTime.Now.AddDays(10),
                vrijednost,
                kriteriji
            );

            mockDb.Verify(x => x.DodajTender(It.Is<Tender>(t =>
                t.ProcijenjenaVrijednost == vrijednost
            )), Times.Once);
        }

        public static IEnumerable<object[]> GetValidneVrijednosti()
        {
            yield return new object[] { 1000m, "Minimalna vrijednost" };
            yield return new object[] { 5000m, "Srednja vrijednost" };
            yield return new object[] { 100000m, "100 hiljada" };
            yield return new object[] { 1000000m, "Milion" };
            yield return new object[] { 10000000m, "Maksimalna vrijednost" };
        }

       

    

        [TestMethod]
        public void ValidirajIKreirajTender_SviUsloviIspunjeni_TenderUspjesnoKreiran()
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);
            mockDb.Setup(x => x.DohvatiTenderePoFirmi(101)).Returns(new List<Tender>());

            Tender kreiraniTender = null;
            mockDb.Setup(x => x.DodajTender(It.IsAny<Tender>()))
                   .Callback<Tender>(t => kreiraniTender = t);

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 0.5m },
            new Kriterij { Tip = TipKriterija.RokIsporuke, Tezina = 0.3m },
            new Kriterij { Tip = TipKriterija.Garancija, Tezina = 0.2m }
        };

            _tenderService.ValidirajIKreirajTender(
                101,
                "Nabavka računarske opreme",
                "Detaljan opis tendera za nabavku računarske opreme za potrebe firme",
                DateTime.Now.AddDays(15),
                50000m,
                kriteriji
            );

            mockDb.Verify(x => x.DodajTender(It.IsAny<Tender>()), Times.Once);
            Assert.IsNotNull(kreiraniTender);
            Assert.AreEqual(101, kreiraniTender.FirmaId);
            Assert.AreEqual("Nabavka računarske opreme", kreiraniTender.Naziv);
            Assert.AreEqual(StatusTendera.Otvoren, kreiraniTender.Status);
        }

        [TestMethod]
        public void ValidirajIKreirajTender_10AktivnihTendera_BacaException()
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);

            var postojeciTenderi = new List<Tender>();
            for (int i = 1; i <= 10; i++)
            {
                postojeciTenderi.Add(new Tender
                {
                    Id = i,
                    FirmaId = 101,
                    Naziv = $"Tender {i}",
                    Opis = "Opis",
                    Status = StatusTendera.Otvoren
                });
            }

            mockDb.Setup(x => x.DohvatiTenderePoFirmi(101)).Returns(postojeciTenderi);

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var exception = Assert.ThrowsException<Exception>(() =>
            {
                _tenderService.ValidirajIKreirajTender(
                    101,
                    "Novi tender",
                    "Ovo je validan opis tendera koji ima više od pedeset karaktera",
                    DateTime.Now.AddDays(10),
                    5000m,
                    kriteriji
                );
            });

            Assert.AreEqual("Ne možete imati više od 10 aktivnih tendera istovremeno", exception.Message);
        }

        [TestMethod]
        public void ValidirajIKreirajTender_DuplikatNaziva_BacaException()
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);

            var postojeciTenderi = new List<Tender>
        {
            new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Nabavka opreme",
                Opis = "Opis",
                Status = StatusTendera.Otvoren
            }
        };

            mockDb.Setup(x => x.DohvatiTenderePoFirmi(101)).Returns(postojeciTenderi);

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = 1.0m }
        };

            var exception = Assert.ThrowsException<Exception>(() =>
            {
                _tenderService.ValidirajIKreirajTender(
                    101,
                    "NABAVKA OPREME",
                    "Ovo je validan opis tendera koji ima više od pedeset karaktera",
                    DateTime.Now.AddDays(10),
                    5000m,
                    kriteriji
                );
            });

            Assert.AreEqual("Već imate aktivan tender sa istim nazivom", exception.Message);
        }

        [DataTestMethod]
        [DataRow(0.5, 0.3, DisplayName = "Zbir = 0.8")]
        [DataRow(0.6, 0.5, DisplayName = "Zbir = 1.1")]
        [DataRow(0.3, 0.3, DisplayName = "Zbir = 0.6")]
        public void ValidirajIKreirajTender_NevalidanZbirTezina_BacaException(double tezina1, double tezina2)
        {
            mockDb.Setup(x => x.DohvatiFirmu(101)).Returns(_testFirma);
            mockDb.Setup(x => x.DohvatiTenderePoFirmi(101)).Returns(new List<Tender>());

            var kriteriji = new List<Kriterij>
        {
            new Kriterij { Tip = TipKriterija.Cijena, Tezina = (decimal)tezina1 },
            new Kriterij { Tip = TipKriterija.RokIsporuke, Tezina = (decimal)tezina2 }
        };

            var exception = Assert.ThrowsException<Exception>(() =>
            {
                _tenderService.ValidirajIKreirajTender(
                    101,
                    "Validan naziv tendera",
                    "Ovo je validan opis tendera koji ima više od pedeset karaktera",
                    DateTime.Now.AddDays(10),
                    5000m,
                    kriteriji
                );
            });

            Assert.AreEqual("Zbir težina za kriterije mora biti 1,0!", exception.Message);
        }

        [TestMethod]
        public void AzurirajTender_TenderNePostoji_BacaException()
        {
            // Arrange
            mockDb.Setup(x => x.DohvatiTender(999)).Returns((Tender)null);

            // Act & Assert
            var exception = Assert.ThrowsException<Exception>(() =>
            {
                _tenderService.AzurirajTender(999, 101, "Novi naziv", null, null, null);
            });

            Assert.AreEqual("Tender ne postoji", exception.Message);
        }

        [TestMethod]
        public void AzurirajTender_TudjaTenderFirma_BacaUnauthorizedException()
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Stari naziv",
                Opis = "Stari opis koji ima vise od pedeset karaktera za validaciju",
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);

            // Act & Assert
            var exception = Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _tenderService.AzurirajTender(1, 999, "Novi naziv", null, null, null);
            });

            Assert.AreEqual("Ne možete ažurirati tuđi tender", exception.Message);
        }

        [DataTestMethod]
        [DataRow(StatusTendera.Zatvoren, DisplayName = "Zatvoren tender")]
        [DataRow(StatusTendera.Zavrsen, DisplayName = "Završen tender")]
        [DataRow(StatusTendera.Otkazan, DisplayName = "Otkazan tender")]
        public void AzurirajTender_TenderNijeOtvoren_BacaException(StatusTendera status)
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Naziv tendera",
                Opis = "Opis tendera koji ima vise od pedeset karaktera za validaciju",
                Status = status
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);

            // Act & Assert
            var exception = Assert.ThrowsException<Exception>(() =>
            {
                _tenderService.AzurirajTender(1, 101, "Novi naziv", null, null, null);
            });

            Assert.AreEqual("Možete ažurirati samo otvorene tendere", exception.Message);
        }

        [TestMethod]
        public void AzurirajTender_ImaPonude_BacaException()
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Naziv tendera",
                Opis = "Opis tendera koji ima vise od pedeset karaktera za validaciju",
                Status = StatusTendera.Otvoren
            };

            var ponude = new List<Ponuda>
    {
        new Ponuda { Id = 1, TenderId = 1, FirmaId = 102, Iznos = 5000m }
    };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(ponude);

            // Act & Assert
            var exception = Assert.ThrowsException<Exception>(() =>
            {
                _tenderService.AzurirajTender(1, 101, "Novi naziv", null, null, null);
            });

            Assert.AreEqual("Ne možete ažurirati tender koji već ima ponude", exception.Message);
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Prazan naziv")]
        [DataRow("Kratak", DisplayName = "Prekratak naziv - 6 karaktera")]
        [DataRow("123456789", DisplayName = "9 karaktera")]
        public void AzurirajTender_NevalidanNaziv_BacaException(string noviNaziv)
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Stari validan naziv",
                Opis = "Stari opis koji ima vise od pedeset karaktera za validaciju",
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                _tenderService.AzurirajTender(1, 101, noviNaziv, null, null, null);
            });

            Assert.AreEqual("Naziv mora imati minimum 10 karaktera", exception.Message);
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Prazan opis")]
        [DataRow("Kratak", DisplayName = "Prekratak opis")]
        [DataRow("12345678901234567890123456789012345678901234567890", DisplayName = "49 karaktera")]
        public void AzurirajTender_NevalidanOpis_BacaException(string noviOpis)
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Validan naziv tendera",
                Opis = "Stari opis koji ima vise od pedeset karaktera za validaciju",
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                _tenderService.AzurirajTender(1, 101, null, noviOpis, null, null);
            });

            Assert.AreEqual("Opis mora imati minimum 50 karaktera", exception.Message);
        }

        [DataTestMethod]
        [DataRow(-1, "Rok mora biti u budućnosti", DisplayName = "Rok u prošlosti")]
        [DataRow(0, "Rok mora biti u budućnosti", DisplayName = "Danas")]
        [DataRow(5, "Rok mora biti minimum 7 dana unaprijed", DisplayName = "5 dana")]
        [DataRow(7, "Rok mora biti minimum 7 dana unaprijed", DisplayName = "7 dana - granica")]
        public void AzurirajTender_NevalidanRok_BacaException(int danaDodati, string expectedMessage)
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Validan naziv tendera",
                Opis = "Validan opis koji ima vise od pedeset karaktera za validaciju",
                RokZaPrijavu = DateTime.Now.AddDays(30),
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                _tenderService.AzurirajTender(1, 101, null, null, DateTime.Now.AddDays(danaDodati), null);
            });

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        

        [TestMethod]
        public void AzurirajTender_SviUsloviIspunjeni_UspjesnoAzuriranje()
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Stari naziv",
                Opis = "Stari opis koji ima vise od pedeset karaktera za validaciju",
                RokZaPrijavu = DateTime.Now.AddDays(30),
                ProcijenjenaVrijednost = 5000m,
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            Tender azuriraniTender = null;
            mockDb.Setup(x => x.AzurirajTender(It.IsAny<Tender>()))
                   .Callback<Tender>(t => azuriraniTender = t);

            var noviRok = DateTime.Now.AddDays(20);

            // Act
            _tenderService.AzurirajTender(
                1,
                101,
                "Novi validan naziv",
                "Novi validan opis koji ima vise od pedeset karaktera za validaciju",
                noviRok,
                10000m
            );

            // Assert
            mockDb.Verify(x => x.AzurirajTender(It.IsAny<Tender>()), Times.Once);
            Assert.IsNotNull(azuriraniTender);
            Assert.AreEqual("Novi validan naziv", azuriraniTender.Naziv);
            Assert.AreEqual("Novi validan opis koji ima vise od pedeset karaktera za validaciju", azuriraniTender.Opis);
            Assert.AreEqual(10000m, azuriraniTender.ProcijenjenaVrijednost);
            Assert.AreEqual(noviRok.Date, azuriraniTender.RokZaPrijavu.Date);
        }

        [TestMethod]
        public void AzurirajTender_SamoNaziv_OstaloNepromijenjeno()
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Stari naziv",
                Opis = "Stari opis koji ima vise od pedeset karaktera za validaciju",
                RokZaPrijavu = DateTime.Now.AddDays(30),
                ProcijenjenaVrijednost = 5000m,
                Status = StatusTendera.Otvoren
            };

            var originalniRok = postojeciTender.RokZaPrijavu;
            var originalniOpis = postojeciTender.Opis;
            var originalnaVrijednost = postojeciTender.ProcijenjenaVrijednost;

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            Tender azuriraniTender = null;
            mockDb.Setup(x => x.AzurirajTender(It.IsAny<Tender>()))
                   .Callback<Tender>(t => azuriraniTender = t);

            // Act
            _tenderService.AzurirajTender(1, 101, "Novi validan naziv", null, null, null);

            // Assert
            mockDb.Verify(x => x.AzurirajTender(It.IsAny<Tender>()), Times.Once);
            Assert.IsNotNull(azuriraniTender);
            Assert.AreEqual("Novi validan naziv", azuriraniTender.Naziv);
            Assert.AreEqual(originalniOpis, azuriraniTender.Opis);
            Assert.AreEqual(originalnaVrijednost, azuriraniTender.ProcijenjenaVrijednost);
            Assert.AreEqual(originalniRok, azuriraniTender.RokZaPrijavu);
        }

        [TestMethod]
        public void AzurirajTender_NullNaziv_ZadrzavaStariNaziv()
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Originalni naziv",
                Opis = "Originalni opis koji ima vise od pedeset karaktera za validaciju",
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            Tender azuriraniTender = null;
            mockDb.Setup(x => x.AzurirajTender(It.IsAny<Tender>()))
                   .Callback<Tender>(t => azuriraniTender = t);

            // Act
            _tenderService.AzurirajTender(1, 101, null, null, null, null);

            // Assert
            Assert.IsNotNull(azuriraniTender);
            Assert.AreEqual("Originalni naziv", azuriraniTender.Naziv);
        }

        [TestMethod]
        public void AzurirajTender_WhitespaceNaziv_ZadrzavaStariNaziv()
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Originalni naziv",
                Opis = "Originalni opis koji ima vise od pedeset karaktera za validaciju",
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            Tender azuriraniTender = null;
            mockDb.Setup(x => x.AzurirajTender(It.IsAny<Tender>()))
                   .Callback<Tender>(t => azuriraniTender = t);

            // Act
            _tenderService.AzurirajTender(1, 101, "     ", null, null, null);

            // Assert
            Assert.IsNotNull(azuriraniTender);
            Assert.AreEqual("Originalni naziv", azuriraniTender.Naziv);
        }

        [TestMethod]
        public void AzurirajTender_NullOpis_ZadrzavaStariOpis()
        {
            // Arrange
            var postojeciTender = new Tender
            {
                Id = 1,
                FirmaId = 101,
                Naziv = "Originalni naziv",
                Opis = "Originalni opis koji ima vise od pedeset karaktera za validaciju",
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(x => x.DohvatiTender(1)).Returns(postojeciTender);
            mockDb.Setup(x => x.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            Tender azuriraniTender = null;
            mockDb.Setup(x => x.AzurirajTender(It.IsAny<Tender>()))
                   .Callback<Tender>(t => azuriraniTender = t);

            // Act
            _tenderService.AzurirajTender(1, 101, null, null, null, null);

            // Assert
            Assert.IsNotNull(azuriraniTender);
            Assert.AreEqual("Originalni opis koji ima vise od pedeset karaktera za validaciju", azuriraniTender.Opis);
        }

        

        [TestMethod]
        public void ZatvoriTender_ValidTender_ClosesTender()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;
            var tender = new Tender
            {
                Id = tenderId,
                FirmaId = firmaId,
                Status = StatusTendera.Otvoren,
                Naziv = "Test Tender"
            };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);  // Mockamo da dobijemo ovaj tender
            mockDb.Setup(db => db.AzurirajTender(It.IsAny<Tender>())).Verifiable(); // Mockamo AzurirajTender metodu

            // Act
            _tenderService.ZatvoriTender(tenderId, firmaId);

            // Assert
            Assert.AreEqual(StatusTendera.Zatvoren, tender.Status);  // Provjeravamo da je status promijenjen u "Zatvoren"
            mockDb.Verify(db => db.AzurirajTender(It.IsAny<Tender>()), Times.Once);  // Provjeravamo da je AzurirajTender pozvan
        }

        // Test 2: Tender ne postoji
        [TestMethod]
        [ExpectedException(typeof(Exception), "Tender ne postoji")]
        public void ZatvoriTender_TenderDoesNotExist_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns((Tender)null); // Mockamo da tender ne postoji

            // Act
            _tenderService.ZatvoriTender(tenderId, firmaId); // Ovdje očekujemo izuzetak
        }

        // Test 3: Neautorizirani pristup - Zatvaranje tuđeg tendera
        [TestMethod]
        [ExpectedException(typeof(UnauthorizedAccessException), "Ne možete zatvoriti tuđi tender")]
        public void ZatvoriTender_PokusajZatvaranjaTudegTendera_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;
            var tender = new Tender
            {
                Id = tenderId,
                FirmaId = 2,  // Tender je vezan za drugu firmu
                Status = StatusTendera.Otvoren
            };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender); // Mockamo da dobijemo ovaj tender

            // Act
            _tenderService.ZatvoriTender(tenderId, firmaId); // Ovdje očekujemo izuzetak
        }

        // Test 4: Tender nije otvoren
        [TestMethod]
        [ExpectedException(typeof(Exception), "Samo otvoreni tender može biti zatvoren")]
        public void ZatvoriTender_TenderNijeOtvoren_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;
            var tender = new Tender
            {
                Id = tenderId,
                FirmaId = firmaId,
                Status = StatusTendera.Zatvoren, // Tender je već zatvoren
                Naziv = "Test Tender"
            };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender); // Mockamo da dobijemo ovaj tender

            // Act
            _tenderService.ZatvoriTender(tenderId, firmaId); // Ovdje očekujemo izuzetak
        }
        // Test 1: Validan slučaj, tender uspješno dodijeljen
        
        // Test 2: Tender ne postoji
        [TestMethod]
        public void DodijeliTender_TenderNePostoji_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var ponudaId = 1;
            var firmaId = 1;

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns((Tender)null);  // Tender ne postoji

            // Act and Assert
            var ex = Assert.ThrowsException<Exception>(() => _tenderService.DodijeliTender(tenderId, ponudaId, firmaId));
            Assert.AreEqual("Tender ne postoji", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }

        // Test 3: Ponuda ne postoji
        [TestMethod]
        public void DodijeliTender_PonudaNePostoji_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var ponudaId = 1;
            var firmaId = 1;

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(new Tender { Id = tenderId, FirmaId = firmaId });
            mockDb.Setup(db => db.DohvatiPonudu(ponudaId)).Returns((Ponuda)null);  // Ponuda ne postoji

            // Act and Assert
            var ex = Assert.ThrowsException<Exception>(() => _tenderService.DodijeliTender(tenderId, ponudaId, firmaId));
            Assert.AreEqual("Ponuda ne postoji", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }

        // Test 4: Neautorizirani pristup - Zatvaranje tuđeg tendera
        [TestMethod]
        public void DodijeliTender_ZatvaranjeTudegTendera_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var tenderId = 1;
            var ponudaId = 1;
            var firmaId = 1;
            var tender = new Tender { Id = tenderId, FirmaId = 2, Status = StatusTendera.Zatvoren };  // Firma nije ista
            var ponuda = new Ponuda { Id = ponudaId, TenderId = tenderId, FirmaId = firmaId };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);
            mockDb.Setup(db => db.DohvatiPonudu(ponudaId)).Returns(ponuda);

            // Act and Assert
            var ex = Assert.ThrowsException<UnauthorizedAccessException>(() => _tenderService.DodijeliTender(tenderId, ponudaId, firmaId));
            Assert.AreEqual("Ne možete dodijeliti tuđi tender", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }

        // Test 5: Tender nije zatvoren
        [TestMethod]
        public void DodijeliTender_TenderNijeZatvoren_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var ponudaId = 1;
            var firmaId = 1;
            var tender = new Tender { Id = tenderId, FirmaId = firmaId, Status = StatusTendera.Otvoren };  // Tender nije zatvoren
            var ponuda = new Ponuda { Id = ponudaId, TenderId = tenderId, FirmaId = firmaId };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);
            mockDb.Setup(db => db.DohvatiPonudu(ponudaId)).Returns(ponuda);

            // Act and Assert
            var ex = Assert.ThrowsException<Exception>(() => _tenderService.DodijeliTender(tenderId, ponudaId, firmaId));
            Assert.AreEqual("Tender mora biti zatvoren prije dodjele", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }

        // Test 6: Ponuda ne pripada tenderu
        [TestMethod]
        public void DodijeliTender_PonudaNePripadaTenderu_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var ponudaId = 1;
            var firmaId = 1;
            var tender = new Tender { Id = tenderId, FirmaId = firmaId, Status = StatusTendera.Zatvoren };
            var ponuda = new Ponuda { Id = ponudaId, TenderId = 2, FirmaId = firmaId };  // Ponuda nije povezana s tenderom

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);
            mockDb.Setup(db => db.DohvatiPonudu(ponudaId)).Returns(ponuda);

            // Act and Assert
            var ex = Assert.ThrowsException<Exception>(() => _tenderService.DodijeliTender(tenderId, ponudaId, firmaId));
            Assert.AreEqual("Ponuda ne pripada ovom tenderu", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }
        [TestMethod]
        public void DodijeliTender_ValidanTenderIPonuda_DodjelaTenderaIAzuriranjeDrugihPonuda()
        {
            // Arrange
            var tenderId = 1;
            var ponudaId = 1;
            var firmaId = 1;
            var tender = new Tender
            {
                Id = tenderId,
                FirmaId = firmaId,
                Status = StatusTendera.Zatvoren,
                Naziv = "Test Tender"
            };
            var ponuda = new Ponuda
            {
                Id = ponudaId,
                TenderId = tenderId,
                FirmaId = firmaId,
                Status = StatusPonude.NaCekanju
            };
            var firma = new Firma
            {
                Id = firmaId,
                Naziv = "Test Firma"
            };
            var ostalePonude = new List<Ponuda>
    {
        new Ponuda { Id = 2, TenderId = tenderId, FirmaId = 2, Status = StatusPonude.NaCekanju },
        new Ponuda { Id = 3, TenderId = tenderId, FirmaId = 3, Status = StatusPonude.NaCekanju }
    };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);
            mockDb.Setup(db => db.DohvatiPonudu(ponudaId)).Returns(ponuda);
            mockDb.Setup(db => db.DohvatiFirmu(firmaId)).Returns(firma);
            mockDb.Setup(db => db.DohvatiPonudePoTenderu(tenderId)).Returns(ostalePonude.Append(ponuda).ToList());
            mockDb.Setup(db => db.AzurirajTender(It.IsAny<Tender>())).Verifiable();
            mockDb.Setup(db => db.AzurirajPonudu(It.IsAny<Ponuda>())).Verifiable();

            // Act
            _tenderService.DodijeliTender(tenderId, ponudaId, firmaId);

            // Assert
            // Provjeravamo da li je tender označen kao završen
            Assert.AreEqual(StatusTendera.Zavrsen, tender.Status);

            // Provjeravamo da li je ponuda označena kao prihvaćena
            Assert.AreEqual(StatusPonude.Prihvacena, ponuda.Status);

            // Provjeravamo da li su ostale ponude označene kao odbijene
            foreach (var p in ostalePonude)
            {
                mockDb.Verify(db => db.AzurirajPonudu(It.Is<Ponuda>(pon => pon.Id == p.Id && pon.Status == StatusPonude.Odbijena)), Times.Once);
            }

            // Provjeravamo da je tender ažuriran
            mockDb.Verify(db => db.AzurirajTender(It.Is<Tender>(t => t.Id == tenderId && t.Status == StatusTendera.Zavrsen)), Times.Once);

            // Provjeravamo da je ponuda ažurirana
            mockDb.Verify(db => db.AzurirajPonudu(It.Is<Ponuda>(p => p.Id == ponudaId && p.Status == StatusPonude.Prihvacena)), Times.Once);

            // Provjeravamo da je ispisana poruka o pobjedničkoj firmi
            mockDb.Verify(db => db.DohvatiFirmu(firmaId), Times.Once);
        }

        [TestMethod]
        public void OtkaziTender_TenderNePostoji_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns((Tender)null);  // Tender ne postoji

            // Act and Assert
            var ex = Assert.ThrowsException<Exception>(() => _tenderService.OtkaziTender(tenderId, firmaId, "Razlog otkazivanja"));
            Assert.AreEqual("Tender ne postoji", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }

        [TestMethod]
        public void OtkaziTender_UnauthorizedAccess_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;
            var tender = new Tender { Id = tenderId, FirmaId = 2, Status = StatusTendera.Otvoren };  // Firma nije ista
            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);

            // Act and Assert
            var ex = Assert.ThrowsException<UnauthorizedAccessException>(() => _tenderService.OtkaziTender(tenderId, firmaId, "Razlog otkazivanja"));
            Assert.AreEqual("Ne možete otkazati tuđi tender", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }

        [TestMethod]
        public void OtkaziTender_TenderZavrsen_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;
            var tender = new Tender { Id = tenderId, FirmaId = firmaId, Status = StatusTendera.Zavrsen };  // Tender je već završen
            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);

            // Act and Assert
            var ex = Assert.ThrowsException<Exception>(() => _tenderService.OtkaziTender(tenderId, firmaId, "Razlog otkazivanja"));
            Assert.AreEqual("Ne možete otkazati već dodijeljen tender", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }

        [TestMethod]
        public void OtkaziTender_NevalidanRazlog_ThrowsException()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;
            var tender = new Tender { Id = tenderId, FirmaId = firmaId, Status = StatusTendera.Otvoren };  // Tender je otvoren
            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);

            // Act and Assert
            var ex = Assert.ThrowsException<ArgumentException>(() => _tenderService.OtkaziTender(tenderId, firmaId, null));
            Assert.AreEqual("Razlog otkazivanja je obavezan", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }

        [TestMethod]
        public void OtkaziTender_UspjesnoOtkazivanjePonudaITendera()
        {
            // Arrange
            var tenderId = 1;
            var firmaId = 1;
            var razlog = "Nema više potrebe za tenderom";
            var tender = new Tender { Id = tenderId, FirmaId = firmaId, Status = StatusTendera.Otvoren, Naziv = "Test Tender" };
            var ponuda1 = new Ponuda { Id = 1, TenderId = tenderId, FirmaId = 2, Status = StatusPonude.NaCekanju };
            var ponuda2 = new Ponuda { Id = 2, TenderId = tenderId, FirmaId = 3, Status = StatusPonude.NaCekanju };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);
            mockDb.Setup(db => db.DohvatiPonudePoTenderu(tenderId)).Returns(new List<Ponuda> { ponuda1, ponuda2 });
            mockDb.Setup(db => db.AzurirajTender(It.IsAny<Tender>())).Verifiable();
            mockDb.Setup(db => db.AzurirajPonudu(It.IsAny<Ponuda>())).Verifiable();

            // Mockiranje DohvatiFirmu
            mockDb.Setup(db => db.DohvatiFirmu(firmaId)).Returns(new Firma { Id = firmaId, Naziv = "Test Firma" });

            // Act
            _tenderService.OtkaziTender(tenderId, firmaId, razlog);

            // Assert
            // Provjeravamo da je tender označen kao otkazan
            Assert.AreEqual(StatusTendera.Otkazan, tender.Status);

            // Provjeravamo da su ponude označene kao odbijene
            Assert.AreEqual(StatusPonude.Odbijena, ponuda1.Status);
            Assert.AreEqual(StatusPonude.Odbijena, ponuda2.Status);



        }
        [TestMethod]
        public void ObrisiTender_TenderPostoji_BrisanjeTenderaIPonuda()
        {
            // Arrange
            var tenderId = 1;
            var tender = new Tender { Id = tenderId, FirmaId = 1, Status = StatusTendera.Otvoren };
            var ponuda1 = new Ponuda { Id = 1, TenderId = tenderId, Status = StatusPonude.NaCekanju };
            var ponuda2 = new Ponuda { Id = 2, TenderId = tenderId, Status = StatusPonude.NaCekanju };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);
            mockDb.Setup(db => db.DohvatiPonudePoTenderu(tenderId)).Returns(new List<Ponuda> { ponuda1, ponuda2 });
            mockDb.Setup(db => db.ObrisiPonudu(It.IsAny<int>())).Verifiable();
            mockDb.Setup(db => db.ObrisiTender(It.IsAny<int>())).Verifiable();

            // Act
            _tenderService.ObrisiTender(tenderId);

            // Assert
            mockDb.Verify(db => db.ObrisiPonudu(It.Is<int>(id => id == ponuda1.Id)), Times.Once);  // Provjeravamo da je prva ponuda obrisana
            mockDb.Verify(db => db.ObrisiPonudu(It.Is<int>(id => id == ponuda2.Id)), Times.Once);  // Provjeravamo da je druga ponuda obrisana
            mockDb.Verify(db => db.ObrisiTender(It.Is<int>(id => id == tenderId)), Times.Once);  // Provjeravamo da je tender obrisan
        }

        // Test za DohvatiMojeTendere
        [TestMethod]
        public void DohvatiMojeTendere_ValidnaFirma_VracaTendere()
        {
            // Arrange
            var firmaId = 1;
            var tender1 = new Tender { Id = 1, FirmaId = firmaId, Status = StatusTendera.Otvoren };
            var tender2 = new Tender { Id = 2, FirmaId = firmaId, Status = StatusTendera.Zatvoren };

            mockDb.Setup(db => db.DohvatiTenderePoFirmi(firmaId)).Returns(new List<Tender> { tender1, tender2 });

            // Act
            var result = _tenderService.DohvatiMojeTendere(firmaId);

            // Assert
            Assert.AreEqual(2, result.Count);  // Provjeravamo da su dva tendere vraćena
            Assert.IsTrue(result.All(t => t.FirmaId == firmaId));  // Provjeravamo da su svi tendere za tu firmu
        }

        // Test za DohvatiTudjeTendere
        [TestMethod]
        public void DohvatiTudjeTendere_ValidnaFirma_VracaTendereDrugeFirme()
        {
            // Arrange
            var firmaId = 1;
            var tender1 = new Tender { Id = 1, FirmaId = 2, Status = StatusTendera.Otvoren };  // Tender od druge firme
            var tender2 = new Tender { Id = 2, FirmaId = 3, Status = StatusTendera.Otvoren };  // Tender od treće firme

            mockDb.Setup(db => db.DohvatiAktivneTendere()).Returns(new List<Tender> { tender1, tender2 });

            // Act
            var result = _tenderService.DohvatiTudjeTendere(firmaId);

            // Assert
            Assert.AreEqual(2, result.Count);  // Provjeravamo da su dva tendere vraćena
            Assert.IsTrue(result.All(t => t.FirmaId != firmaId));  // Provjeravamo da su tendere za druge firme
        }

        // Test za DohvatiSveTendere
        [TestMethod]
        public void DohvatiSveTendereTest()
        {
            // Arrange
            var tender1 = new Tender { Id = 1, FirmaId = 1, Status = StatusTendera.Otvoren };
            var tender2 = new Tender { Id = 2, FirmaId = 2, Status = StatusTendera.Zatvoren };

            mockDb.Setup(db => db.DohvatiSveTendere()).Returns(new List<Tender> { tender1, tender2 });

            // Act
            var result = _tenderService.DohvatiSveTendere();

            // Assert
            Assert.AreEqual(2, result.Count);  // Provjeravamo da su oba tendere vraćena
        }

        // Test za DohvatiTender
        [TestMethod]
        public void DohvatiTender_TenderPostoji()
        {
            // Arrange
            var tenderId = 1;
            var tender = new Tender { Id = tenderId, FirmaId = 1, Status = StatusTendera.Otvoren };

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns(tender);

            // Act
            var result = _tenderService.DohvatiTender(tenderId);

            // Assert
            Assert.AreEqual(tenderId, result.Id);  // Provjeravamo da je vraćen ispravan tender
        }

        [TestMethod]
        public void DohvatiTender_TenderNePostoji_ThrowsException()
        {
            // Arrange
            var tenderId = 1;

            mockDb.Setup(db => db.DohvatiTender(tenderId)).Returns((Tender)null);  // Tender ne postoji

            // Act and Assert
            var ex = Assert.ThrowsException<Exception>(() => _tenderService.DohvatiTender(tenderId));
            Assert.AreEqual("Tender ne postoji", ex.Message);  // Provjeravamo da li je poruka iznimke točna
        }
    }


}
