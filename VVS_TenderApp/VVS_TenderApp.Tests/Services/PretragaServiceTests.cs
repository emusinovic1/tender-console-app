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

        [Ignore]
        [ExcludeFromCodeCoverage]
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

        [Ignore]
        [ExcludeFromCodeCoverage]
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

            
            Assert.AreEqual(1, result[0].Id, "Tender sa 'Tender' na pocetku naziva treba biti prvi");

           
            Console.WriteLine("✅ Bubble sort: možda nije najbrži, ali je naš!");
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



        // =====================================================================
        //                  WHITE - BOX TESTIRANJE: PZ 2
        // =====================================================================



        // =====================================================================
        //                  Metoda: PretraziPoKljucnojRijeci                                        
        //                  Radila: Emina Zubetljak
        // =====================================================================

        // Helper za kreiranje tendera (važno: Naziv i Opis ne smiju biti null jer se radi ToLower()).
        private Tender MojCreateTender(int id, string naziv, string opis)
        {
            return new Tender
            {
                Id = id,
                Naziv = naziv,
                Opis = opis
            };
        }

        // Helper za generisanje opisa sa N ponavljanja ključne riječi, razdvojeno razmakom.
        private string OpisWithRepeats(string keyword, int count)
        {
            if (count <= 0) return "nema";
            return string.Join(" ", Enumerable.Repeat(keyword, count));
        }

        // ------------------ TC1/TC2: rani izlaz (null/empty/whitespace) ------------------

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void PretraziPoKljucnojRijeci_NullOrWhitespace_ReturnsAllTenders(string kljucnaRijec)
        {
            // Arrange
            var svi = new List<Tender>
            {
                MojCreateTender(1, "Tender A", "Opis A"),
                MojCreateTender(2, "Tender B", "Opis B")
            };

            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(svi);

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci(kljucnaRijec);

            // Assert
            Assert.IsNotNull(rezultat);
            Assert.AreEqual(2, rezultat.Count);
            Assert.AreEqual(1, rezultat[0].Id);
            Assert.AreEqual(2, rezultat[1].Id);

            _mockDb.Verify(d => d.DohvatiSveTendere(), Times.Once);
        }

        // ------------------ TC3: 0 tendera -> 0 rezultata ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_NoTenders_ReturnsEmpty()
        {
            // Arrange
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender>());

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.IsNotNull(rezultat);
            Assert.AreEqual(0, rezultat.Count);

            _mockDb.Verify(d => d.DohvatiSveTendere(), Times.Once);
        }

        // ------------------ TC4: nema poklapanja -> prazno ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_NoMatch_ReturnsEmpty()
        {
            // Arrange
            var svi = new List<Tender>
            {
                MojCreateTender(1, "Hello", "World")
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(svi);

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(0, rezultat.Count);
        }

        // ------------------ TC5: naziv sadrži (nije startswith, nije equals) ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_NameContainsOnly_IncludesTender()
        {
            // Arrange
            var svi = new List<Tender>
            {
                MojCreateTender(1, "xx abc yy", "nista")
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(svi);

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(1, rezultat.Count);
            Assert.AreEqual(1, rezultat[0].Id);
        }

        // ------------------ TC6: naziv startswith ima veći score od "contains only" -> sort ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_StartsWith_ShouldComeBefore_ContainsOnly()
        {
            // Arrange
            // contains-only score = 10
            // startswith score = 15
            var t1 = MojCreateTender(1, "xx abc yy", "nista");
            var t2 = MojCreateTender(2, "abc - tender", "nista");

            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { t1, t2 });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(2, rezultat.Count);
            Assert.AreEqual(2, rezultat[0].Id, "Tender koji počinje ključnom riječju treba biti prvi (veći score).");
            Assert.AreEqual(1, rezultat[1].Id);
        }

        // ------------------ TC7: naziv == ključna ima najveći score -> sort ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_NameEquals_ShouldHaveHighestScore()
        {
            // Arrange
            // equals score = 25 (10 + 5 + 10)
            // startswith score = 15
            // contains-only score = 10
            var tContains = MojCreateTender(1, "xx abc yy", "nista");
            var tStarts = MojCreateTender(2, "abc - tender", "nista");
            var tEquals = MojCreateTender(3, "abc", "nista");

            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { tContains, tStarts, tEquals });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(3, rezultat.Count);
            Assert.AreEqual(3, rezultat[0].Id, "Naziv jednak ključnoj riječi mora biti prvi (najveći score).");
            Assert.AreEqual(2, rezultat[1].Id);
            Assert.AreEqual(1, rezultat[2].Id);
        }

        // ------------------ TC8: opis sadrži keyword 1 put -> while 1 iteracija ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_DescriptionOneOccurrence_IncludesTender()
        {
            // Arrange
            // opis score = 3 + 1 = 4
            var t1 = MojCreateTender(1, "nista", "neki abc tekst");
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { t1 });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(1, rezultat.Count);
            Assert.AreEqual(1, rezultat[0].Id);
        }

        // ------------------ TC9/TC10: opis 3 puta (bez break) vs 4+ puta (break) -> redoslijed ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_DescriptionThreeVsFourOccurrences_FourShouldComeFirst()
        {
            // Arrange
            // 3 occurrences -> score = 3 + 3 = 6
            // 4 occurrences -> score = 3 + 4 = 7 (break granom se limitira na 4)
            var t3 = MojCreateTender(1, "nista", OpisWithRepeats("abc", 3));
            var t4 = MojCreateTender(2, "nista", OpisWithRepeats("abc", 4));

            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { t3, t4 });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(2, rezultat.Count);
            Assert.AreEqual(2, rezultat[0].Id, "Tender sa 4 pojavljivanja treba biti ispred 3 pojavljivanja.");
            Assert.AreEqual(1, rezultat[1].Id);
        }

        // ------------------ TC10 (posebno): 4 vs 5 pojavljivanja -> score se cap-uje na 4, pa je rezultat stabilan ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_DescriptionFiveOccurrences_IsCappedToFour_StableOrder()
        {
            // Arrange
            // Ako metoda ispravno prekida nakon >3, max pojavljivanja = 4,
            // pa 4 i 5 pojavljivanja daju isti score (3+4=7) -> nema swap-a (stabilan redoslijed).
            var t4 = MojCreateTender(1, "nista", OpisWithRepeats("abc", 4));
            var t5 = MojCreateTender(2, "nista", OpisWithRepeats("abc", 5));

            // Namjerno: t4 je prije t5.
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { t4, t5 });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(2, rezultat.Count);
            Assert.AreEqual(1, rezultat[0].Id, "Ako je score cap-ovan na 4, redoslijed treba ostati stabilan (bez swap-a).");
            Assert.AreEqual(2, rezultat[1].Id);
        }

        // ------------------ TC11: i naziv i opis daju bodove (kombinovan score) ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_NameAndDescriptionBothMatch_IncludesTender()
        {
            // Arrange
            // naziv contains-only: 10
            // opis 2 puta: (3+2)=5
            // total = 15
            var t = MojCreateTender(1, "xx abc yy", "abc nesto abc jos");

            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { t });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(1, rezultat.Count);
            Assert.AreEqual(1, rezultat[0].Id);
        }

        // ------------------ TC12: bubble sort swap grana (scorovi[j] < scorovi[j+1]) = True ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_BubbleSort_SwapOccurs_HigherScoreFirst()
        {
            // Arrange
            // tLow: opis 1 put -> 4
            // tHigh: naziv == keyword -> 25
            var tLow = MojCreateTender(1, "nista", "abc");
            var tHigh = MojCreateTender(2, "abc", "nista");

            // Namjerno: manji score prvo, da swap mora da se desi.
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { tLow, tHigh });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(2, rezultat.Count);
            Assert.AreEqual(2, rezultat[0].Id, "Bubble sort treba dovesti veći score na vrh (swap grana).");
            Assert.AreEqual(1, rezultat[1].Id);
        }

        // ------------------ TC13: bubble sort no-swap na jednakim score-ovima (stabilnost) ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_BubbleSort_EqualScores_NoSwap_StableOrder()
        {
            // Arrange
            // tA: startswith -> 15
            // tB: naziv contains-only (10) + opis 2 puta (5) = 15
            // Score isti -> if (scorovi[j] < scorovi[j+1]) je false -> nema swap-a -> stabilan redoslijed.
            var tA = MojCreateTender(1, "abc - tender", "nista");
            var tB = MojCreateTender(2, "xx abc yy", "abc abc"); // 2 pojavljivanja u opisu

            // Namjerno: tA prije tB. Ako je stabilno i nema swap-a na jednakim score-ovima, redoslijed ostaje.
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { tA, tB });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("abc");

            // Assert
            Assert.AreEqual(2, rezultat.Count);
            Assert.AreEqual(1, rezultat[0].Id, "Na jednakim score-ovima metoda ne treba raditi swap (stabilan redoslijed).");
            Assert.AreEqual(2, rezultat[1].Id);
        }

        // ------------------ Dodatno: provjera case-insensitive ponašanja ------------------

        [TestMethod]
        public void PretraziPoKljucnojRijeci_IsCaseInsensitive()
        {
            // Arrange
            var t = MojCreateTender(1, "AbC Tender", "Opis");
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender> { t });

            // Act
            var rezultat = _service.PretraziPoKljucnojRijeci("aBc");

            // Assert
            Assert.AreEqual(1, rezultat.Count);
            Assert.AreEqual(1, rezultat[0].Id);
        }

        // =====================================================================


    }
}
