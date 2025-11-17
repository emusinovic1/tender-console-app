using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;
using System.IO;


namespace VVS_TenderApp.Tests.Services
{

    [TestClass]
    public class PonudaServiceTests
    {
        private Mock<DbClass> _mockDb;
        private PonudaService _service;

        public TestContext TestContext { get; set; }
        /*
         ***************************************************************************************+
         
                                            TDD TESTOVI

                                   FUNKCIONALNOST - Ocjenjivanje firmi
         
         
         ****************************************************************************************
         */
        [TestMethod]
        public void OcijeniFirmu_ValidnaOcjena_SnimiSe()
        {
            var firma = new Firma { Id = 2, Naziv = "TestFirma" };

            _mockDb.Setup(d => d.DohvatiFirmu(2)).Returns(firma);

            _service.OcijeniFirmu(2, 5);

            _mockDb.Verify(d => d.SnimiOcjenu(2, 5), Times.Once);
        }

        [TestMethod]
        public void OcijeniFirmu_FirmaNePostoji_BacaException()
        {
            _mockDb.Setup(d => d.DohvatiFirmu(2))
                   .Returns((Firma)null);

            Assert.ThrowsException<Exception>(() =>
                _service.OcijeniFirmu(2, 5));
        }


        [TestMethod]
        public void OcijeniFirmu_OcjenaNevalidna_BacaArgumentException()
        {
            var firma = new Firma { Id = 1 };
            _mockDb.Setup(d => d.DohvatiFirmu(1)).Returns(firma);

            Assert.ThrowsException<ArgumentException>(() =>
                _service.OcijeniFirmu(1, 0));

            Assert.ThrowsException<ArgumentException>(() =>
                _service.OcijeniFirmu(1, 6));
        }


        public static IEnumerable<object[]> NevalidniIznosi()
        {
            // Prva linija je header "Iznos"
            var lines = File.ReadAllLines(@"Data\NevalidniIznosi.csv")
                            .Skip(1); // preskoči header

            foreach (var line in lines)
            {
                if (decimal.TryParse(line, out var iznos))
                {
                    yield return new object[] { iznos };
                }
            }
        }


        [TestInitialize]
        public void Setup()
        {
            _mockDb = new Mock<DbClass>();
            _service = new PonudaService(_mockDb.Object);
        }

        // Helper metode za kreiranje default objekata
        private Tender CreateDefaultTender(decimal procijenjena = 1000m, int firmaId = 1)
        {
            return new Tender
            {
                Id = 10,
                Naziv = "Test tender",
                Status = StatusTendera.Otvoren,
                RokZaPrijavu = DateTime.Now.AddDays(1),
                FirmaId = firmaId,
                ProcijenjenaVrijednost = procijenjena,
                Kriteriji = new List<Kriterij>() // može ostati prazno za PonudaService
            };
        }

        private Firma CreateDefaultFirma(int id = 2)
        {
            return new Firma
            {
                Id = id,
                Naziv = "Test firma"
            };
        }

        private Ponuda CreatePonuda(
            int id, int tenderId, int firmaId, decimal iznos,
            StatusPonude status = StatusPonude.NaCekanju)
        {
            return new Ponuda
            {
                Id = id,
                TenderId = tenderId,
                FirmaId = firmaId,
                Iznos = iznos,
                DatumSlanja = DateTime.Now.AddDays(-1),
                Status = status
            };
        }

        // ------------------ VALIDIRAJ I POSALJI PONUDU ------------------

        [DataTestMethod]
        [DataRow(1000, 200)]  // 20% procjenjene vrijednosti
        [DataRow(5000, 1000)] // 20% procjenjene vrijednosti
        public void ValidirajIPosaljiPonudu_ValidPodaci_DodajePonudu(int procijenjena, int iznos)
        {
            // Arrange
            int tenderId = 10;
            int firmaId = 2;

            var tender = CreateDefaultTender(procijenjena, firmaId: 1); // tender objavio firma 1
            var firma = CreateDefaultFirma(firmaId);

            _mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiFirmu(firmaId)).Returns(firma);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId))
                   .Returns(new List<Ponuda>());
            _mockDb.Setup(d => d.DohvatiPonudePoFirmi(firmaId))
                   .Returns(new List<Ponuda>());

            // Act
            _service.ValidirajIPosaljiPonudu(tenderId, firmaId, iznos);

            // Assert - provjeri da je dodana ponuda
            _mockDb.Verify(d => d.DodajPonudu(It.Is<Ponuda>(p =>
                p.TenderId == tenderId &&
                p.FirmaId == firmaId &&
                p.Iznos == iznos &&
                p.Status == StatusPonude.NaCekanju)),
                Times.Once);
        }

        [TestMethod]
        public void ValidirajIPosaljiPonudu_TenderNePostoji_BacaException()
        {
            // Arrange
            _mockDb.Setup(d => d.DohvatiTender(It.IsAny<int>()))
                   .Returns((Tender)null);

            // Act + Assert
            Assert.ThrowsException<Exception>(() =>
                _service.ValidirajIPosaljiPonudu(1, 2, 1000m));
        }

        [TestMethod]
        public void ValidirajIPosaljiPonudu_TenderNijeOtvoren_BacaException()
        {
            var tender = CreateDefaultTender();
            tender.Status = StatusTendera.Zatvoren;

            _mockDb.Setup(d => d.DohvatiTender(1)).Returns(tender);

            Assert.ThrowsException<Exception>(() =>
                _service.ValidirajIPosaljiPonudu(1, 2, 1000m));
        }

        [TestMethod]
        public void ValidirajIPosaljiPonudu_RokIstekao_BacaException()
        {
            var tender = CreateDefaultTender();
            tender.RokZaPrijavu = DateTime.Now.AddDays(-1);

            _mockDb.Setup(d => d.DohvatiTender(1)).Returns(tender);

            Assert.ThrowsException<Exception>(() =>
                _service.ValidirajIPosaljiPonudu(1, 2, 1000m));
        }

        [TestMethod]
        public void ValidirajIPosaljiPonudu_FirmaNePostoji_BacaException()
        {
            var tender = CreateDefaultTender();

            _mockDb.Setup(d => d.DohvatiTender(1)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiFirmu(2))
                   .Returns((Firma)null);

            Assert.ThrowsException<Exception>(() =>
                _service.ValidirajIPosaljiPonudu(1, 2, 1000m));
        }

        [TestMethod]
        public void ValidirajIPosaljiPonudu_PrijavaNaSopstveniTender_BacaException()
        {
            var tender = CreateDefaultTender(firmaId: 2); // isti firmaId
            var firma = CreateDefaultFirma(2);

            _mockDb.Setup(d => d.DohvatiTender(1)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiFirmu(2)).Returns(firma);

            Assert.ThrowsException<Exception>(() =>
                _service.ValidirajIPosaljiPonudu(1, 2, 1000m));
        }

        // Data-driven preko CSV fajla (druga forma pohranjenih podataka)
        [DataTestMethod]
        [DynamicData(nameof(NevalidniIznosi), DynamicDataSourceType.Method)]
        public void ValidirajIPosaljiPonudu_NevalidanIznosIzCsv_BacaArgumentException(decimal iznos)
        {
            // Arrange
            int tenderId = 10;
            int firmaId = 2;

            var tender = CreateDefaultTender(procijenjena: 1000m, firmaId: 1);
            var firma = CreateDefaultFirma(firmaId);

            _mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiFirmu(firmaId)).Returns(firma);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId))
                   .Returns(new List<Ponuda>());
            _mockDb.Setup(d => d.DohvatiPonudePoFirmi(firmaId))
                   .Returns(new List<Ponuda>());

            // Act + Assert
            Assert.ThrowsException<ArgumentException>(() =>
                _service.ValidirajIPosaljiPonudu(tenderId, firmaId, iznos));
        }


        [TestMethod]
        public void ValidirajIPosaljiPonudu_PostojiPonudaZaTender_BacaException()
        {
            var tender = CreateDefaultTender(procijenjena: 1000m, firmaId: 1);
            var firma = CreateDefaultFirma(2);
            var postojeca = CreatePonuda(1, tender.Id, 2, 500m);

            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiFirmu(2)).Returns(firma);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tender.Id))
                   .Returns(new List<Ponuda> { postojeca });

            Assert.ThrowsException<Exception>(() =>
                _service.ValidirajIPosaljiPonudu(tender.Id, 2, 600m));
        }

        [TestMethod]
        public void ValidirajIPosaljiPonudu_PreviseAktivnihPonuda_BacaException()
        {
            var tender = CreateDefaultTender(procijenjena: 1000m, firmaId: 1);
            var firma = CreateDefaultFirma(2);

            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiFirmu(2)).Returns(firma);

            var aktivne = Enumerable.Range(1, 20)
                .Select(i => CreatePonuda(i, tender.Id, 2, 500m))
                .ToList();

            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tender.Id))
                   .Returns(new List<Ponuda>());
            _mockDb.Setup(d => d.DohvatiPonudePoFirmi(2))
                   .Returns(aktivne);

            Assert.ThrowsException<Exception>(() =>
                _service.ValidirajIPosaljiPonudu(tender.Id, 2, 600m));
        }

        // ------------------ AZURIRAJ PONUDU ------------------

        [TestMethod]
        public void AzurirajPonudu_ValidniPodaci_AzuriraPonudu()
        {
            var tender = CreateDefaultTender(procijenjena: 1000m);
            var ponuda = CreatePonuda(1, tender.Id, 2, 500m);

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);
            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            _service.AzurirajPonudu(1, 2, 700m);

            _mockDb.Verify(d => d.AzurirajPonudu(It.Is<Ponuda>(p =>
                p.Id == 1 && p.Iznos == 700m)), Times.Once);
        }

        [TestMethod]
        public void AzurirajPonudu_PonudaNePostoji_BacaException()
        {
            _mockDb.Setup(d => d.DohvatiPonudu(1))
                   .Returns((Ponuda)null);

            Assert.ThrowsException<Exception>(() =>
                _service.AzurirajPonudu(1, 2, 700m));
        }

        [TestMethod]
        public void AzurirajPonudu_TudjaPonuda_BacaUnauthorizedAccessException()
        {
            var ponuda = CreatePonuda(1, 10, firmaId: 3, iznos: 500m);

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                _service.AzurirajPonudu(1, 2, 700m));
        }

        // ------------------ POVUCI PONUDU ------------------

        [TestMethod]
        public void PovuciPonudu_ValidniPodaci_BrisePonudu()
        {
            var tender = CreateDefaultTender();
            var ponuda = CreatePonuda(1, tender.Id, 2, 500m);

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);
            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            _service.PovuciPonudu(1, 2);

            _mockDb.Verify(d => d.ObrisiPonudu(1), Times.Once);
        }

        [TestMethod]
        public void PovuciPonudu_TudjaPonuda_BacaUnauthorizedAccessException()
        {
            var tender = CreateDefaultTender();
            var ponuda = CreatePonuda(1, tender.Id, 3, 500m);

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                _service.PovuciPonudu(1, 2));
        }

        // ------------------ DOHVATI MOJE PONUDE ------------------

        [TestMethod]
        public void DohvatiMojePonude_VracaSortiranuListu()
        {
            int firmaId = 2;
            var p1 = CreatePonuda(1, 10, firmaId, 500m);
            p1.DatumSlanja = DateTime.Now.AddDays(-2);

            var p2 = CreatePonuda(2, 10, firmaId, 600m);
            p2.DatumSlanja = DateTime.Now;

            _mockDb.Setup(d => d.DohvatiPonudePoFirmi(firmaId))
                   .Returns(new List<Ponuda> { p1, p2 });

            var rezultat = _service.DohvatiMojePonude(firmaId);

            Assert.AreEqual(2, rezultat.Count);
            Assert.AreEqual(2, rezultat.First().Id, "Najnovija ponuda treba biti prva.");
        }

        // ------------------ DOHVATI PONUDU ------------------

        [TestMethod]
        public void DohvatiPonudu_PonudaPostoji_VracaPonudu()
        {
            var ponuda = CreatePonuda(1, 10, 2, 500m);

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);

            var result = _service.DohvatiPonudu(1);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Id);
        }

        [TestMethod]
        public void DohvatiPonudu_PonudaNePostoji_BacaException()
        {
            _mockDb.Setup(d => d.DohvatiPonudu(1))
                   .Returns((Ponuda)null);

            Assert.ThrowsException<Exception>(() =>
                _service.DohvatiPonudu(1));
        }

        // ------------------ RANGIRAJ PONUDE (u PonudaService) ------------------

        [TestMethod]
        public void RangirajPonude_FiltriraPoStatusuISortiraPoIznosu()
        {
            int tenderId = 10;
            var p1 = CreatePonuda(1, tenderId, 2, 300m, StatusPonude.NaCekanju);
            var p2 = CreatePonuda(2, tenderId, 3, 200m, StatusPonude.NaCekanju);
            var p3 = CreatePonuda(3, tenderId, 4, 100m, StatusPonude.Odbijena); // ne treba ući

            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId))
                   .Returns(new List<Ponuda> { p1, p2, p3 });

            var rezultat = _service.RangirajPonude(tenderId);

            Assert.AreEqual(2, rezultat.Count);
            Assert.AreEqual(2, rezultat.First().Id); // najniža cijena 200
        }

        // ================== DODATNI TESTOVI ZA PUNU POKRIVENOST ==================

        // --------- VALIDIRAJ I POSALJI PONUDU: max 50 ponuda na tender ---------

        [TestMethod]
        public void ValidirajIPosaljiPonudu_PrevisePonudaNaTender_BacaException()
        {
            int tenderId = 10;
            int firmaId = 2;

            var tender = CreateDefaultTender(procijenjena: 1000m, firmaId: 1);
            var firma = CreateDefaultFirma(firmaId);

            // 50 već postojećih ponuda na istom tenderu
            var ponudeNaTender = Enumerable.Range(1, 50)
                .Select(i => CreatePonuda(i, tenderId, firmaId + i, 500m))
                .ToList();

            _mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiFirmu(firmaId)).Returns(firma);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId))
                   .Returns(ponudeNaTender);
            // Da ne padne na ograničenju po firmi
            _mockDb.Setup(d => d.DohvatiPonudePoFirmi(firmaId))
                   .Returns(new List<Ponuda>());

            Assert.ThrowsException<Exception>(() =>
                _service.ValidirajIPosaljiPonudu(tenderId, firmaId, 500m));
        }

        // ------------------ AZURIRAJ PONUDU: dodatne grane ------------------

        [TestMethod]
        public void AzurirajPonudu_PonudaNijeNaCekanju_BacaException()
        {
            var ponuda = CreatePonuda(1, tenderId: 10, firmaId: 2, iznos: 500m,
                                      status: StatusPonude.Odbijena);

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);

            Assert.ThrowsException<Exception>(() =>
                _service.AzurirajPonudu(1, 2, 700m));
        }

        [TestMethod]
        public void AzurirajPonudu_TenderNijeOtvoren_BacaException()
        {
            var ponuda = CreatePonuda(1, tenderId: 10, firmaId: 2, iznos: 500m);
            var tender = CreateDefaultTender(procijenjena: 1000m);
            tender.Status = StatusTendera.Zatvoren; // trigger

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);
            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            Assert.ThrowsException<Exception>(() =>
                _service.AzurirajPonudu(1, 2, 500m));
        }

        [TestMethod]
        public void AzurirajPonudu_RokIstekao_BacaException()
        {
            var ponuda = CreatePonuda(1, tenderId: 10, firmaId: 2, iznos: 500m);
            var tender = CreateDefaultTender(procijenjena: 1000m);
            tender.RokZaPrijavu = DateTime.Now.AddDays(-1); // istekao

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);
            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            Assert.ThrowsException<Exception>(() =>
                _service.AzurirajPonudu(1, 2, 500m));
        }

        [TestMethod]
        public void AzurirajPonudu_NoviIznosNulaIliNegativan_BacaArgumentException()
        {
            var ponuda = CreatePonuda(1, tenderId: 10, firmaId: 2, iznos: 500m);
            var tender = CreateDefaultTender(procijenjena: 1000m);

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);
            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            Assert.ThrowsException<ArgumentException>(() =>
                _service.AzurirajPonudu(1, 2, 0m));
        }

        [TestMethod]
        public void AzurirajPonudu_NoviIznosIspodMinimuma_BacaArgumentException()
        {
            var ponuda = CreatePonuda(1, tenderId: 10, firmaId: 2, iznos: 500m);
            var tender = CreateDefaultTender(procijenjena: 1000m); // min = 100

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);
            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            Assert.ThrowsException<ArgumentException>(() =>
                _service.AzurirajPonudu(1, 2, 50m)); // ispod min
        }

        [TestMethod]
        public void AzurirajPonudu_NoviIznosIznadMaksimuma_BacaArgumentException()
        {
            var ponuda = CreatePonuda(1, tenderId: 10, firmaId: 2, iznos: 500m);
            var tender = CreateDefaultTender(procijenjena: 1000m); // max = 5000

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);
            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            Assert.ThrowsException<ArgumentException>(() =>
                _service.AzurirajPonudu(1, 2, 6000m)); // iznad max
        }

        // ------------------ POVUCI PONUDU: dodatne grane ------------------

        [TestMethod]
        public void PovuciPonudu_PonudaNePostoji_BacaException()
        {
            _mockDb.Setup(d => d.DohvatiPonudu(1))
                   .Returns((Ponuda)null);

            Assert.ThrowsException<Exception>(() =>
                _service.PovuciPonudu(1, 2));
        }

        [TestMethod]
        public void PovuciPonudu_PonudaNijeNaCekanju_BacaException()
        {
            var ponuda = CreatePonuda(1, tenderId: 10, firmaId: 2, iznos: 500m,
                                      status: StatusPonude.Odbijena);

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);

            Assert.ThrowsException<Exception>(() =>
                _service.PovuciPonudu(1, 2));
        }

        [TestMethod]
        public void PovuciPonudu_TenderZatvoren_BacaException()
        {
            var ponuda = CreatePonuda(1, tenderId: 10, firmaId: 2, iznos: 500m);
            var tender = CreateDefaultTender();
            tender.Status = StatusTendera.Zatvoren; // trigger

            _mockDb.Setup(d => d.DohvatiPonudu(1)).Returns(ponuda);
            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            Assert.ThrowsException<Exception>(() =>
                _service.PovuciPonudu(1, 2));
        }

    }
}
