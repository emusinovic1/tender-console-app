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
        private Mock<DbClass> _mockDb;
        private TenderService _service;

        /*
         ***************************************************************************************+
         
                                            TDD TESTOVI

                    FUNKCIONALNOST - Automatska dodjela tendera po rangiranju ponuda
         
         
         ****************************************************************************************
         */

        [TestInitialize]
        public void Setup()
        {
            _mockDb = new Mock<DbClass>();
            _service = new TenderService(_mockDb.Object);
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

            _mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId)).Returns(ponude);
            _mockDb.Setup(d => d.DohvatiFirmu(p2.FirmaId))
                   .Returns(new Firma { Id = p2.FirmaId, Naziv = "Pobjednička firma" });

            // RangiranjeService će koristiti isti _db mock, pa mu vraćamo ove vrijednosti
            // Kriterij je samo cijena (Tezina = 1), tako da će ponuda s manjom cijenom biti prva

            var pobjednik = _service.AutomatskiDodijeliTender(tenderId, firmaNaruciocaId);

            Assert.IsNotNull(pobjednik);
            Assert.AreEqual(p2.Id, pobjednik.Id, "Očekujemo da je ponuda sa manjom cijenom pobjednik.");

            _mockDb.Verify(d => d.AzurirajTender(It.Is<Tender>(t =>
                t.Id == tenderId && t.Status == StatusTendera.Zavrsen)), Times.Once);

            _mockDb.Verify(d => d.AzurirajPonudu(It.Is<Ponuda>(p =>
                p.Id == p2.Id && p.Status == StatusPonude.Prihvacena)), Times.Once);

            _mockDb.Verify(d => d.AzurirajPonudu(It.Is<Ponuda>(p =>
                p.Id == p1.Id && p.Status == StatusPonude.Odbijena)), Times.Once);
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_TenderNePostoji_BacaException()
        {
            _mockDb.Setup(d => d.DohvatiTender(It.IsAny<int>()))
                   .Returns((Tender)null);

            Assert.ThrowsException<Exception>(() =>
                _service.AutomatskiDodijeliTender(1, 1));
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_TenderNePripadaFirmi_BacaUnauthorizedAccessException()
        {
            var tender = CreateDefaultTender(id: 1, firmaId: 99); // druga firma

            _mockDb.Setup(d => d.DohvatiTender(1)).Returns(tender);

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                _service.AutomatskiDodijeliTender(1, 1));
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_TenderNijeZatvoren_BacaException()
        {
            var tender = CreateDefaultTender(status: StatusTendera.Otvoren);

            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);

            Assert.ThrowsException<Exception>(() =>
                _service.AutomatskiDodijeliTender(tender.Id, tender.FirmaId));
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_NemaPonuda_BacaException()
        {
            var tender = CreateDefaultTender();

            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tender.Id))
                   .Returns(new List<Ponuda>());

            Assert.ThrowsException<Exception>(() =>
                _service.AutomatskiDodijeliTender(tender.Id, tender.FirmaId));
        }

        [TestMethod]
        public void AutomatskiDodijeliTender_NemaRangiranihPonuda_BacaException()
        {
            var tender = CreateDefaultTender();
            tender.Kriteriji.Clear(); // bez kriterija -> RangiranjeService vraća praznu listu

            var p1 = CreatePonuda(1, tender.Id, 2, 9000m);
            var ponude = new List<Ponuda> { p1 };

            _mockDb.Setup(d => d.DohvatiTender(tender.Id)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tender.Id)).Returns(ponude);

            Assert.ThrowsException<Exception>(() =>
                _service.AutomatskiDodijeliTender(tender.Id, tender.FirmaId));
        }

        /*
         ***************************************************************************************+
         
                                        KRAJ TDD TESTOVA
         
         ****************************************************************************************
         */
    }
}
