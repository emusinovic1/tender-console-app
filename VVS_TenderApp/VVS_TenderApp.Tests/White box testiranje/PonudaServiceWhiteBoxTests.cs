using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;

namespace VVS_TenderApp.Tests.White_box_testiranje
{
	[TestClass]
	public class PonudaServiceWhiteBoxTests
	{
		private Mock<DbClass> _mockDb;
		private PonudaService _service;

		[TestInitialize]
		public void Setup()
		{
			// Moramo kreirati Mock za DbClass. 
			// Napomena: Da bi Moq radio, metode u DbClass bi trebale biti 'virtual' 
			// ili DbClass treba implementirati interfejs (npr. IDbClass).
			_mockDb = new Mock<DbClass>();
			_service = new PonudaService(_mockDb.Object);
		}

		// =====================================================================
		//                 WHITE - BOX TESTIRANJE: PZ 4.3
		// =====================================================================
		// Metoda: ValidirajIPosaljiPonudu
		// Cilj: Pokrivanje svih 15 nezavisnih putanja (CC=15)
		// =====================================================================

		// --- TC1 - TC3: Validacija Tendera ---

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC1_TenderNull_ThrowsException()
		{
			_mockDb.Setup(db => db.DohvatiTender(It.IsAny<int>())).Returns((Tender)null);
			_service.ValidirajIPosaljiPonudu(1, 1, 1000);
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC2_TenderZatvoren_ThrowsException()
		{
			var tender = new Tender { Status = StatusTendera.Zatvoren };
			_mockDb.Setup(db => db.DohvatiTender(1)).Returns(tender);
			_service.ValidirajIPosaljiPonudu(1, 1, 1000);
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC3_RokIstekao_ThrowsException()
		{
			var tender = new Tender { Status = StatusTendera.Otvoren, RokZaPrijavu = DateTime.Now.AddDays(-1) };
			_mockDb.Setup(db => db.DohvatiTender(1)).Returns(tender);
			_service.ValidirajIPosaljiPonudu(1, 1, 1000);
		}

		// --- TC4 - TC5: Validacija Firme i Vlasništva ---

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC4_FirmaNull_ThrowsException()
		{
			var tender = CreateValidTender();
			_mockDb.Setup(db => db.DohvatiTender(1)).Returns(tender);
			_mockDb.Setup(db => db.DohvatiFirmu(It.IsAny<int>())).Returns((Firma)null);
			_service.ValidirajIPosaljiPonudu(1, 99, 1000);
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC5_SopstveniTender_ThrowsException()
		{
			var tender = CreateValidTender();
			tender.FirmaId = 10; // Tender pripada firmi 10
			_mockDb.Setup(db => db.DohvatiTender(1)).Returns(tender);
			_mockDb.Setup(db => db.DohvatiFirmu(10)).Returns(new Firma { Id = 10 });
			_service.ValidirajIPosaljiPonudu(1, 10, 1000);
		}

		// --- TC6: Iznos <= 0 ---

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void TC6_IznosNula_ThrowsException()
		{
			SetupBaseValidMocks();
			_service.ValidirajIPosaljiPonudu(1, 1, 0);
		}

		// --- TC7 - TC9: Petlja (foreach) i brojOdbijenih ---

		[TestMethod]
		public void TC7_NulaOdbijenih_LoopPasses()
		{
			SetupBaseValidMocks();
			_mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(new List<Ponuda>());
			_mockDb.Setup(db => db.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

			_service.ValidirajIPosaljiPonudu(1, 1, 5000); // Treba proći (Happy Path do te tačke)
		}
        [TestMethod]
        public void TC8_JednaOdbijena_LoopPasses()
        {
            SetupBaseValidMocks();
            // Simuliramo jednu odbijenu ponudu - petlja se izvršava jednom, ali 1 < 10
            var jednaOdbijena = new List<Ponuda> { new Ponuda { Status = StatusPonude.Odbijena } };
            _mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(jednaOdbijena);
            _mockDb.Setup(db => db.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

            _service.ValidirajIPosaljiPonudu(1, 1, 5000); // Treba proći
        }

        [TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC9_Preko10Odbijenih_ThrowsException()
		{
			SetupBaseValidMocks();
			var odbijene = Enumerable.Range(1, 11).Select(i => new Ponuda { Status = StatusPonude.Odbijena }).ToList();
			_mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(odbijene);

			_service.ValidirajIPosaljiPonudu(1, 1, 5000);
		}

		// --- TC10 - TC11: Finansijski Limiti (10% i 500%) ---

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void TC10_IznosIspodMinimuma_ThrowsException()
		{
			SetupBaseValidMocks(); // Procijenjena vrijednost u helperu je 10.000
			_mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(new List<Ponuda>());
			_service.ValidirajIPosaljiPonudu(1, 1, 500); // 500 < 1000 (10%)
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void TC11_IznosIznadMaximuma_ThrowsException()
		{
			SetupBaseValidMocks();
			_mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(new List<Ponuda>());
			_service.ValidirajIPosaljiPonudu(1, 1, 60000); // 60000 > 50000 (500%)
		}

		// --- TC12 - TC14: Kvantitativni limiti (Duplikati, Max 20, Max 50) ---

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC12_VecPoslanaPonuda_ThrowsException()
		{
			SetupBaseValidMocks();
			_mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(new List<Ponuda>());
			// Simuliramo da u listi ponuda za tender već postoji jedna od ove firme
			_mockDb.Setup(db => db.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda> { new Ponuda { FirmaId = 1 } });

			_service.ValidirajIPosaljiPonudu(1, 1, 5000);
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC13_Preko20Aktivnih_ThrowsException()
		{
			SetupBaseValidMocks();
			var aktivne = Enumerable.Range(1, 20).Select(i => new Ponuda { Status = StatusPonude.NaCekanju }).ToList();
			_mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(aktivne);
			_mockDb.Setup(db => db.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

			_service.ValidirajIPosaljiPonudu(1, 1, 5000);
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void TC14_Max50NaTenderu_ThrowsException()
		{
			SetupBaseValidMocks();
			_mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(new List<Ponuda>());
			var naTenderu = Enumerable.Range(1, 50).Select(i => new Ponuda()).ToList();
			_mockDb.Setup(db => db.DohvatiPonudePoTenderu(1)).Returns(naTenderu);

			_service.ValidirajIPosaljiPonudu(1, 1, 5000);
		}

		// --- TC15: Happy Path (Krajnja naredba) ---

		[TestMethod]
		public void TC15_UspjesnoSlanje_CallsDodajPonudu()
		{
			SetupBaseValidMocks();
			_mockDb.Setup(db => db.DohvatiPonudePoFirmi(1)).Returns(new List<Ponuda>());
			_mockDb.Setup(db => db.DohvatiPonudePoTenderu(1)).Returns(new List<Ponuda>());

			_service.ValidirajIPosaljiPonudu(1, 1, 5000);

			// Provjera da li je na kraju pozvana metoda za upis u bazu
			_mockDb.Verify(db => db.DodajPonudu(It.IsAny<Ponuda>()), Times.Once);
		}

		// --- HELPER METODE ---

		private Tender CreateValidTender()
		{
			return new Tender
			{
				Id = 1,
				Status = StatusTendera.Otvoren,
				RokZaPrijavu = DateTime.Now.AddDays(1),
				FirmaId = 999, // Pripada nekoj drugoj firmi
				ProcijenjenaVrijednost = 10000,
				Naziv = "Test Tender"
			};
		}

		private void SetupBaseValidMocks()
		{
			var tender = CreateValidTender();
			_mockDb.Setup(db => db.DohvatiTender(1)).Returns(tender);
			_mockDb.Setup(db => db.DohvatiFirmu(1)).Returns(new Firma { Id = 1 });
		}
	}
}