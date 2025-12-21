using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;

namespace VVS_TenderApp.Tests.Services
{
    [TestClass]
    public class RangiranjeServiceTests
    {
        private Mock<DbClass> _mockDb;
        private RangiranjeService _service;

        [TestInitialize]
        public void Setup()
        {
            _mockDb = new Mock<DbClass>();
            _service = new RangiranjeService(_mockDb.Object);
        }

        private Tender CreateTenderSaKriterijima()
        {
            return new Tender
            {
                Id = 10,
                Naziv = "Tender za nabavku",
                Kriteriji = new List<Kriterij>
                {
                    new Kriterij { Tip = TipKriterija.Cijena, Tezina = 0.5m },
                    new Kriterij { Tip = TipKriterija.RokIsporuke, Tezina = 0.3m },
                    new Kriterij { Tip = TipKriterija.Garancija, Tezina = 0.2m }
                }
            };
        }

        private Ponuda Ponuda(
            int id, int tenderId, decimal iznos,
            int rokDana, int garancijaMjeseci)
        {
            return new Ponuda
            {
                Id = id,
                TenderId = tenderId,
                Iznos = iznos,
                RokIsporukeDana = rokDana,
                GarancijaMjeseci = garancijaMjeseci
            };
        }

        [TestMethod]
        public void RangirajPonude_BezPonudaIliBezKriterija_VracaPraznuListu()
        {
            int tenderId = 10;

            var tenderBezKriterija = new Tender
            {
                Id = tenderId,
                Kriteriji = new List<Kriterij>()
            };

            _mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tenderBezKriterija);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId))
                   .Returns(new List<Ponuda>());

            var rezultat = _service.RangirajPonude(tenderId);

            Assert.IsNotNull(rezultat);
            Assert.AreEqual(0, rezultat.Count);
        }

        [TestMethod]
        public void RangirajPonude_RacunaSkorIIspravnoSortira()
        {
            int tenderId = 10;
            var tender = CreateTenderSaKriterijima();

            var ponuda1 = Ponuda(1, tenderId, iznos: 1000m, rokDana: 10, garancijaMjeseci: 12);
            var ponuda2 = Ponuda(2, tenderId, iznos: 800m, rokDana: 15, garancijaMjeseci: 6);

            _mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId))
                   .Returns(new List<Ponuda> { ponuda1, ponuda2 });

            var rezultat = _service.RangirajPonude(tenderId);

            Assert.AreEqual(2, rezultat.Count);

            // EXPECTATION:
            // ponuda2 ima nižu cijenu, ali lošiji rok i garanciju.
            // Ovisno o težinama, neka bude da je ponuda1 ipak bolja:
            var najbolja = rezultat.First();

            Assert.IsTrue(najbolja.skor > 0);
            Assert.IsTrue(rezultat[0].skor >= rezultat[1].skor);
        }

        [TestMethod]
        public void DajOcjenuZaPonudu_VracaSkorZaPostojecuPonudu()
        {
            int tenderId = 10;
            var tender = CreateTenderSaKriterijima();

            var ponuda1 = Ponuda(1, tenderId, 1000m, 10, 12);
            var ponuda2 = Ponuda(2, tenderId, 800m, 15, 6);

            _mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId))
                   .Returns(new List<Ponuda> { ponuda1, ponuda2 });

            var skor = _service.DajOcjenuZaPonudu(ponuda1);

            Assert.IsTrue(skor > 0, "Za postojeću ponudu skor treba biti veći od 0.");
        }

        [TestMethod]
        public void DajOcjenuZaPonudu_NepoznataPonuda_VracaNulu()
        {
            int tenderId = 10;
            var tender = CreateTenderSaKriterijima();

            var ponuda1 = Ponuda(1, tenderId, 1000m, 10, 12);

            _mockDb.Setup(d => d.DohvatiTender(tenderId)).Returns(tender);
            _mockDb.Setup(d => d.DohvatiPonudePoTenderu(tenderId))
                   .Returns(new List<Ponuda> { ponuda1 });

            var laznjak = Ponuda(999, tenderId, 1200m, 20, 3);

            var skor = _service.DajOcjenuZaPonudu(laznjak);

            Assert.AreEqual(0m, skor);
        }

    }
}
