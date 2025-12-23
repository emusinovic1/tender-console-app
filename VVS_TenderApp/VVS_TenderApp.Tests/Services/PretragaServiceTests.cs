using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;

namespace VVS_TenderApp.Tests.Services
{
    [TestClass]
    public class PretragaServiceTests
    {
        private Mock<DbClass> _mockDb;
        private PretragaService _service;

        [TestInitialize]
        public void Setup()
        {
            _mockDb = new Mock<DbClass>();
            _service = new PretragaService(_mockDb.Object);
        }

        // Helper za tender
        private Tender CreateTender(int id = 1,
                                    string naziv = "Naziv",
                                    string opis = "Opis",
                                    decimal procijenjena = 1000m,
                                    StatusTendera status = StatusTendera.Otvoren,
                                    DateTime? datumObjave = null,
                                    DateTime? rok = null,
                                    int firmaId = 1)
        {
            return new Tender
            {
                Id = id,
                Naziv = naziv,
                Opis = opis,
                ProcijenjenaVrijednost = procijenjena,
                Status = status,
                DatumObjave = datumObjave ?? new DateTime(2025, 1, 1),
                RokZaPrijavu = rok ?? new DateTime(2025, 2, 1),
                FirmaId = firmaId,
                Kriteriji = new List<Kriterij>()
            };
        }

        // ============================================================
        // MOCK TESTS
        // ============================================================

        [TestMethod]
        public void PretraziPoKljucnojRijeci_UsesDbClass()
        {
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender>());

            var result = _service.PretraziPoKljucnojRijeci("test");

            _mockDb.Verify(d => d.DohvatiSveTendere(), Times.Exactly(1));
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void NaprednaPretraga_CallsDbAndFilters()
        {
            var t1 = CreateTender(1, "A", procijenjena: 500m, status: StatusTendera.Otvoren, firmaId: 2);
            var t2 = CreateTender(2, "B", procijenjena: 1500m, status: StatusTendera.Zatvoren, firmaId: 2);
            var t3 = CreateTender(3, "C", procijenjena: 1200m, status: StatusTendera.Otvoren, firmaId: 3);

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2, t3 });

            var result = _service.NaprednaPretraga(null, 1000m, 1300m, StatusTendera.Otvoren,
                new DateTime(2025, 1, 1), new DateTime(2025, 12, 1), 3);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(3, result[0].Id);
        }

        // ============================================================
        // DATA DRIVEN – DATAROW
        // ============================================================

        [DataTestMethod]
        [DataRow("test")]
        [DataRow("road")]
        [DataRow("mostar")]
        public void PretraziPoKljucnojRijeci_DataRow(string keyword)
        {
            var t1 = CreateTender(1, naziv: keyword + " plus", opis: "opis");
            var t2 = CreateTender(2, naziv: "other", opis: "nebitno");

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2 });

            var result = _service.PretraziPoKljucnojRijeci(keyword);

            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual(1, result[0].Id);
        }

        // ============================================================
        // DYNAMIC DATA – CSV
        // ============================================================

        public static IEnumerable<object[]> CsvKeywords()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Keywords.csv");
            if (!File.Exists(path))
                yield break;

            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    yield return new object[] { line.Trim() };
            }
        }

       
        [DataTestMethod]
        [DynamicData(nameof(CsvKeywords), DynamicDataSourceType.Method)]
        public void PretraziPoKljucnojRijeci_Csv_Works(string keyword)
        {
            var t1 = CreateTender(1, naziv: keyword + " project");
            var t2 = CreateTender(2, naziv: "nothing");

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2 });

            var result = _service.PretraziPoKljucnojRijeci(keyword);

            Assert.AreEqual(1, result[0].Id);
        }

        // ============================================================
        // NAPREDNA PRETRAGA
        // ============================================================

        [TestMethod]
        public void NaprednaPretraga_NoFilters_ReturnsAll()
        {
            var t1 = CreateTender(1);
            var t2 = CreateTender(2);

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2 });

            var result = _service.NaprednaPretraga(null, null, null, null, null, null, null);

            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void NaprednaPretraga_FilterByDate()
        {
            var t1 = CreateTender(1, datumObjave: new DateTime(2025, 1, 1));
            var t2 = CreateTender(2, datumObjave: new DateTime(2025, 3, 1));

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2 });

            var result = _service.NaprednaPretraga(null, null, null, null,
                new DateTime(2025, 1, 1), new DateTime(2025, 2, 1), null);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Id);
        }

        // ============================================================
        // POJEDINAČNE METODE: Vrijednost, Status, Datum
        // ============================================================

        [TestMethod]
        public void PretraziPoVrijednosti_Works()
        {
            var t1 = CreateTender(1, procijenjena: 300m);
            var t2 = CreateTender(2, procijenjena: 150m);
            var t3 = CreateTender(3, procijenjena: 600m);

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2, t3 });

            var result = _service.PretraziPoVrijednosti(100m, 600m);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(150m, result[0].ProcijenjenaVrijednost);
        }

        [TestMethod]
        public void PretraziPoStatusu_Works()
        {
            var t1 = CreateTender(1, status: StatusTendera.Otvoren, datumObjave: new DateTime(2025, 2, 1));
            var t2 = CreateTender(2, status: StatusTendera.Otvoren, datumObjave: new DateTime(2025, 1, 1));
            var t3 = CreateTender(3, status: StatusTendera.Zatvoren);

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2, t3 });

            var result = _service.PretraziPoStatusu(StatusTendera.Otvoren);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].Id); // newest first
        }

        [TestMethod]
        public void PretraziPoDatumu_Works()
        {
            var t1 = CreateTender(1, datumObjave: new DateTime(2025, 1, 1));
            var t2 = CreateTender(2, datumObjave: new DateTime(2025, 1, 20));
            var t3 = CreateTender(3, datumObjave: new DateTime(2025, 3, 1));

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2, t3 });

            var result = _service.PretraziPoDatumu(new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(2, result[0].Id);
        }

        // ============================================================
        // SORTIRANJE
        // ============================================================

        [TestMethod]
        public void SortirajPoNazivu_Works()
        {
            var list = new List<Tender>
            {
                CreateTender(1, "C"),
                CreateTender(2, "A"),
                CreateTender(3, "B")
            };

            var asc = _service.SortirajPoNazivu(list, true);
            var desc = _service.SortirajPoNazivu(list, false);

            Assert.AreEqual("A", asc[0].Naziv);
            Assert.AreEqual("C", desc[0].Naziv);
        }

        [TestMethod]
        public void SortirajPoVrijednosti_Works()
        {
            var list = new List<Tender>
            {
                CreateTender(1, procijenjena: 300m),
                CreateTender(2, procijenjena: 100m)
            };

            var asc = _service.SortirajPoVrijednosti(list, true);
            var desc = _service.SortirajPoVrijednosti(list, false);

            Assert.AreEqual(100m, asc[0].ProcijenjenaVrijednost);
            Assert.AreEqual(300m, desc[0].ProcijenjenaVrijednost);
        }

        [TestMethod]
        public void SortirajPoRoku_Works()
        {
            var list = new List<Tender>
            {
                CreateTender(1, rok: new DateTime(2025, 5, 1)),
                CreateTender(2, rok: new DateTime(2025, 1, 1))
            };

            var asc = _service.SortirajPoRoku(list, true);
            var desc = _service.SortirajPoRoku(list, false);

            Assert.AreEqual(new DateTime(2025, 1, 1), asc[0].RokZaPrijavu);
            Assert.AreEqual(new DateTime(2025, 5, 1), desc[0].RokZaPrijavu);
        }

        [TestMethod]
        public void SortirajPoDatumu_Works()
        {
            var list = new List<Tender>
            {
                CreateTender(1, datumObjave: new DateTime(2025, 3, 1)),
                CreateTender(2, datumObjave: new DateTime(2025, 2, 1))
            };

            var asc = _service.SortirajPoDatumu(list, true);
            var desc = _service.SortirajPoDatumu(list, false);

            Assert.AreEqual(new DateTime(2025, 2, 1), asc[0].DatumObjave);
            Assert.AreEqual(new DateTime(2025, 3, 1), desc[0].DatumObjave);
        }

       
        [TestMethod]
        public void PretraziPoKljucnojRijeci_BubbleSort_StillFunctionsAsExpected()
        {


            var t1 = CreateTender(1, naziv: "Najbolji Tender Ever", opis: "opis");
            var t2 = CreateTender(2, naziv: "Tender", opis: "opis opis opis"); // vise puta u opisu
            var t3 = CreateTender(3, naziv: "Obican Tender", opis: "samo opis");

            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender> { t3, t1, t2 }); 

           
            var result = _service.PretraziPoKljucnojRijeci("tender");

           
            Assert.AreEqual(3, result.Count, "Trebalo bi naći sve tendere");


            Assert.AreEqual(2, result[0].Id, "Tender sa ključnom riječi kao cijelim nazivom treba biti prvi");
        }

        [TestMethod]
        public void PretraziPoKljucnojRijeci_ScoringSystem_KonacnoTestiran()
        {
            
            var exactMatch = CreateTender(1, naziv: "asfalt", opis: "neki opis");

            // Scenario 2: Naziv starts with keyword = visok score  
            var startsWithMatch = CreateTender(2, naziv: "asfalt plus", opis: "opis");

            // Scenario 3: Keyword samo u opisu = niži score
            var opisOnly = CreateTender(3, naziv: "nesto", opis: "asfalt asfalt asfalt");

            // Scenario 4: Keyword u nazivu i opisu = kombinovani score
            var both = CreateTender(4, naziv: "projekat asfalt", opis: "asfalt");

            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender> { opisOnly, both, startsWithMatch, exactMatch });

            // Act
            var result = _service.PretraziPoKljucnojRijeci("asfalt");

            // Assert - provjeri ranking order (highest score first)
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(1, result[0].Id, "Exact match treba biti #1");
            Assert.AreEqual(2, result[1].Id, "Starts with treba biti #2");

            
        }

        [TestMethod]
        public void PretraziPoKljucnojRijeci_MultipleOccurrences_ScoreBoost()
        {
           
            var t1 = CreateTender(1, naziv: "x", opis: "put put put put put"); // 5x = max 3 score
            var t2 = CreateTender(2, naziv: "x", opis: "put"); // 1x = 1 score

            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender> { t2, t1 });

            var result = _service.PretraziPoKljucnojRijeci("put");

            Assert.AreEqual(1, result[0].Id, "Tender sa više pojavljivanja treba biti raniji");
  
        }

        [TestMethod]
        public void PretraziPoKljucnojRijeci_PrazanString_VracaSveTendere()
        {
            var t1 = CreateTender(1);
            var t2 = CreateTender(2);

            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender> { t1, t2 });

            var result1 = _service.PretraziPoKljucnojRijeci("");
            var result2 = _service.PretraziPoKljucnojRijeci("   ");
            var result3 = _service.PretraziPoKljucnojRijeci(null);

            Assert.AreEqual(2, result1.Count);
            Assert.AreEqual(2, result2.Count);
            Assert.AreEqual(2, result3.Count);
        }

        [TestMethod]
        public void PretraziPoKljucnojRijeci_NijePronadjen_VracaPraznuListu()
        {
            var t1 = CreateTender(1, naziv: "Nesto", opis: "Sasvim drugo");

            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender> { t1 });

            var result = _service.PretraziPoKljucnojRijeci("nepostojeci");

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void PretraziPoKljucnojRijeci_SamoUOpisu_NemaBonusScoreZaNaziv()
        {
            var t1 = CreateTender(1, naziv: "XYZ", opis: "most most most");

            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender> { t1 });

            var result = _service.PretraziPoKljucnojRijeci("most");

            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public void PretraziPoKljucnojRijeci_BubbleSort_PraznaLista()
        {
            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender>());

            var result = _service.PretraziPoKljucnojRijeci("bilo sta");

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void PretraziPoKljucnojRijeci_ExactMatch_Plus_StartsWithBonus()
        {
            var exactAndStarts = CreateTender(1, naziv: "put", opis: "put put");

            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender> { exactAndStarts });

            var result = _service.PretraziPoKljucnojRijeci("put");

            Assert.AreEqual(1, result.Count);
        }
      

        [TestMethod]
        public void NaprednaPretraga_FilterByKljucnaRijec_Naziv()
        {
            // Test: kljucna rijec u NAZIVU
            var t1 = CreateTender(1, naziv: "Asfaltiranje ceste", opis: "neki opis");
            var t2 = CreateTender(2, naziv: "Nesto drugo", opis: "nesto");

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2 });

            var result = _service.NaprednaPretraga("asfalt", null, null, null, null, null, null);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Id);
        }

        [TestMethod]
        public void NaprednaPretraga_FilterByKljucnaRijec_Opis()
        {
            // Test: kljucna rijec u OPISU
            var t1 = CreateTender(1, naziv: "Projekat A", opis: "radovi na asfaltu");
            var t2 = CreateTender(2, naziv: "Projekat B", opis: "nesto sasvim drugo");

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2 });

            var result = _service.NaprednaPretraga("asfalt", null, null, null, null, null, null);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Id);
        }

        [TestMethod]
        public void NaprednaPretraga_FilterByKljucnaRijec_NijeUNazivuNitiOpisu()
        {
            // Test: NI u nazivu NI u opisu = odgovara FALSE
            var t1 = CreateTender(1, naziv: "Projekat", opis: "Opis");

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1 });

            var result = _service.NaprednaPretraga("nepostojeci", null, null, null, null, null, null);

            Assert.AreEqual(0, result.Count, "Ne bi trebalo naći ništa");
        }

        [TestMethod]
        public void NaprednaPretraga_CombinedFilters_AllMatch()
        {
            // Test za COMBINATION svih filtera koji PASS-aju
            var t1 = CreateTender(1,
                naziv: "Tender sa asfalt",
                opis: "opis",
                procijenjena: 1500m,
                status: StatusTendera.Otvoren,
                datumObjave: new DateTime(2025, 6, 15),
                firmaId: 5);

            var t2 = CreateTender(2, procijenjena: 500m); // ne zadovoljava min

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2 });

            var result = _service.NaprednaPretraga(
                "asfalt",           // ✓ u nazivu
                1000m,              // ✓ min: 1500 >= 1000
                2000m,              // ✓ max: 1500 <= 2000
                StatusTendera.Otvoren, // ✓
                new DateTime(2025, 6, 1),  // ✓ datum >= 6/1
                new DateTime(2025, 6, 30), // ✓ datum <= 6/30
                5);                 // ✓ firmaId

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Id);
        }

        [TestMethod]
        public void NaprednaPretraga_MinVrijednost_FailsBoundary()
        {
            // tender.ProcijenjenaVrijednost < minVrijednost
            var t1 = CreateTender(1, procijenjena: 999m);

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1 });

            var result = _service.NaprednaPretraga(null, 1000m, null, null, null, null, null);

            Assert.AreEqual(0, result.Count, "999 < 1000, ne bi trebalo proći");
        }

        [TestMethod]
        public void NaprednaPretraga_MaxVrijednost_FailsBoundary()
        {
            // tender.ProcijenjenaVrijednost > maxVrijednost
            var t1 = CreateTender(1, procijenjena: 2001m);

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1 });

            var result = _service.NaprednaPretraga(null, null, 2000m, null, null, null, null);

            Assert.AreEqual(0, result.Count, "2001 > 2000, ne bi trebalo proći");
        }

        [TestMethod]
        public void NaprednaPretraga_Status_Fails()
        {
            // tender.Status != status.Value
            var t1 = CreateTender(1, status: StatusTendera.Zatvoren);

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1 });

            var result = _service.NaprednaPretraga(null, null, null, StatusTendera.Otvoren, null, null, null);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void NaprednaPretraga_DatumOd_Fails()
        {
            // Pokriva tender.DatumObjave < datumOd
            var t1 = CreateTender(1, datumObjave: new DateTime(2024, 12, 31));

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1 });

            var result = _service.NaprednaPretraga(null, null, null, null,
                new DateTime(2025, 1, 1), null, null);

            Assert.AreEqual(0, result.Count, "31.12.2024 < 1.1.2025");
        }

        [TestMethod]
        public void NaprednaPretraga_DatumDo_Fails()
        {
            // Pokriva tender.DatumObjave > datumDo
            var t1 = CreateTender(1, datumObjave: new DateTime(2025, 2, 1));

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1 });

            var result = _service.NaprednaPretraga(null, null, null, null,
                null, new DateTime(2025, 1, 31), null);

            Assert.AreEqual(0, result.Count, "1.2.2025 > 31.1.2025");
        }

        [TestMethod]
        public void NaprednaPretraga_FirmaId_Fails()
        {
            // Pokriva tender.FirmaId != firmaId.Value
            var t1 = CreateTender(1, firmaId: 5);

            _mockDb.Setup(x => x.DohvatiSveTendere()).Returns(new List<Tender> { t1 });

            var result = _service.NaprednaPretraga(null, null, null, null, null, null, 99);

            Assert.AreEqual(0, result.Count, "FirmaId 5 != 99");
        }

        // ============================================================
        // CSV TEST FIX (ako već imaš Keywords.csv)
        // ============================================================

        [DataTestMethod]
        [DataRow("asfalt")]
        [DataRow("cesta")]
        [DataRow("most")]
        [DataRow("put")]
        [DataRow("gradnja")]
        public void PretraziPoKljucnojRijeci_MultipleKeywords_Works(string keyword)
        {
            // Zamjena za CSV test - radi istu stvar bez fajla
            var t1 = CreateTender(1, naziv: keyword + " projekat");
            var t2 = CreateTender(2, naziv: "nesto drugo");

            _mockDb.Setup(x => x.DohvatiSveTendere())
                   .Returns(new List<Tender> { t1, t2 });

            var result = _service.PretraziPoKljucnojRijeci(keyword);

            Assert.IsTrue(result.Count > 0, $"Trebalo bi naći tender sa '{keyword}'");
            Assert.AreEqual(1, result[0].Id);
        }



    }
}
