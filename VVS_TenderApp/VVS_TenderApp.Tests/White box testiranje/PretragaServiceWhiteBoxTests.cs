using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;

namespace VVS_TenderApp.Tests.White_box_testiranje
{
    [TestClass]
    public class PretragaServiceWhiteBoxTests
    {
        private Mock<DbClass> _mockDb;
        private PretragaService _service;

        [TestInitialize]
        public void Setup()
        {
            _mockDb = new Mock<DbClass>();
            _service = new PretragaService(_mockDb.Object);
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

        // =====================================================================
        //                  WHITE - BOX TESTIRANJE: PZ 1
        // =====================================================================


        // =====================================================================
        //                  Metoda: NaprednaPretraga                                        
        //                  Radila: Emina Mušinović
        // =====================================================================
        
        private Tender CreateTender(int id, string naziv, decimal vrijednost,
                                     StatusTendera status, DateTime datum)
        {
            return new Tender
            {
                Id = id,
                Naziv = naziv,
                ProcijenjenaVrijednost = vrijednost,
                Status = status,
                DatumObjave = datum
            };
        }
        // TC1: Putanja 1 - Prazna lista tendera (foreach FALSE)
        [TestMethod]
        public void TC01_EmptyTenderList_ReturnsEmpty()
        {
            // Arrange
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(new List<Tender>());

            // Act
            var result = _service.NaprednaPretraga(null, null, null, null, null);

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        // TC2: Putanja 2 - Bez parametara, svi tenderi prolaze
        [TestMethod]
        public void TC02_NoFilters_ReturnsAllTenders()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 1000m, StatusTendera.Otvoren, DateTime.Now),
                CreateTender(2, "Tender B", 2000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, null, null, null, null);

            // Assert
            Assert.AreEqual(2, result.Count);
        }

        // TC3: Putanja 3 - Keyword ne sadrži, odbacuje tender
        [TestMethod]
        public void TC03_KeywordNotFound_ExcludesTender()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Nabavka opreme", 5000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga("most", null, null, null, null);

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        // TC4: Putanja 4 - Keyword sadrži, while petlja 1 iteracija
        [TestMethod]
        public void TC04_KeywordFoundOnce_AddsScore()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Izgradnja mosta", 5000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga("most", null, null, null, null);

            // Assert
            Assert.AreEqual(1, result.Count);
        }

        // TC5: Putanja 5 - Keyword više puta, while petlja više iteracija
        [TestMethod]
        public void TC05_KeywordFoundMultipleTimes_HigherScore()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "most most most", 5000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga("most", null, null, null, null);

            // Assert
            Assert.AreEqual(1, result.Count);
        }

        // TC6: Putanja 6 - minVrijednost filter odbacuje tender
        [TestMethod]
        public void TC06_BelowMinValue_ExcludesTender()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 1000m, StatusTendera.Otvoren, DateTime.Now)
            };  
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, 5000m, null, null, null);

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        // TC7: Putanja 7 - minVrijednost filter prihvata tender
        [TestMethod]
        public void TC07_AboveMinValue_IncludesTender()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 5000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, 3000m, null, null, null);

            // Assert
            Assert.AreEqual(1, result.Count);
        }

        // TC8: Putanja 8 - maxVrijednost filter odbacuje tender
        [TestMethod]
        public void TC08_AboveMaxValue_ExcludesTender()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 10000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, null, 5000m, null, null);

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        // TC9: Putanja 9 - maxVrijednost filter prihvata tender
        [TestMethod]
        public void TC09_BelowMaxValue_IncludesTender()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 3000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, null, 5000m, null, null);

            // Assert
            Assert.AreEqual(1, result.Count);
        }

        // TC10: Putanja 10 - Status filter odbacuje tender
        [TestMethod]
        public void TC10_WrongStatus_ExcludesTender()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 1000m, StatusTendera.Zatvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, null, null, StatusTendera.Otvoren, null);

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        // TC11: Putanja 11 - Status filter prihvata tender
        [TestMethod]
        public void TC11_CorrectStatus_IncludesTender()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 1000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, null, null, StatusTendera.Otvoren, null);

            // Assert
            Assert.AreEqual(1, result.Count);
        }

        // TC12: Putanja 12 - Datum filter odbacuje tender
        [TestMethod]
        public void TC12_BeforeDate_ExcludesTender()
        {
            // Arrange
            var datum = new DateTime(2023, 1, 1);
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 1000m, StatusTendera.Otvoren, datum)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, null, null, null, new DateTime(2024, 1, 1));

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        // TC13: Putanja 13 - Datum filter prihvata tender
        [TestMethod]
        public void TC13_AfterDate_IncludesTender()
        {
            // Arrange
            var datum = new DateTime(2024, 6, 1);
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Tender A", 1000m, StatusTendera.Otvoren, datum)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga(null, null, null, null, new DateTime(2024, 1, 1));

            // Assert
            Assert.AreEqual(1, result.Count);
        }

        // TC14: Putanja 14 - Bubble Sort sa 1 elementom (ne sortira)
        [TestMethod]
        public void TC14_BubbleSort_OneElement()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "most", 1000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga("most", null, null, null, null);

            // Assert
            Assert.AreEqual(1, result.Count);
        }

        // TC15: Putanja 15 - Bubble Sort swap TRUE (vrši zamjenu)
        [TestMethod]
        public void TC15_BubbleSort_SwapOccurs()
        {
            // Arrange
            var tenderi = new List<Tender>
    {
            CreateTender(1, "izgradnja most", 1000m, StatusTendera.Otvoren, DateTime.Now),      //skor 5
            CreateTender(2, "most most most popravka", 2000m, StatusTendera.Otvoren, DateTime.Now)  // skor 15, 3 put most
    };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga("most", null, null, null, null);

            // Assert
            Assert.AreEqual(2, result.Count);
            //Tender sa većim skorom (id=2, score=15) treba biti prvi
            Assert.AreEqual(2, result[0].Id);
            Assert.AreEqual(1, result[1].Id);
        }

        // TC16: Putanja 16 - Bubble Sort swap FALSE (već sortirano)
        [TestMethod]
        public void TC16_BubbleSort_NoSwapNeeded()
        {
            // Arrange
            var tenderi = new List<Tender>
            {
                CreateTender(1, "most most", 1000m, StatusTendera.Otvoren, DateTime.Now),
                CreateTender(2, "most", 2000m, StatusTendera.Otvoren, DateTime.Now)
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga("most", null, null, null, null);

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].Id); // Redoslijed ostaje isti
        }

        // TC17: Putanja 17 - Kompleksna kombinacija svih filtera
        [TestMethod]
        public void TC17_AllFilters_ComplexPath()
        {
            // Arrange
            var datum = new DateTime(2024, 1, 1);
            var tenderi = new List<Tender>
            {
                CreateTender(1, "Izgradnja mosta", 5000m, StatusTendera.Otvoren, datum),
                CreateTender(2, "Nabavka most", 3000m, StatusTendera.Zatvoren, datum),
                CreateTender(3, "Popravka mosta", 7000m, StatusTendera.Otvoren, datum.AddDays(-10))
            };
            _mockDb.Setup(d => d.DohvatiSveTendere()).Returns(tenderi);

            // Act
            var result = _service.NaprednaPretraga("most", 4000m, 8000m,
                                                    StatusTendera.Otvoren, datum.AddDays(-5));

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Id);
        }
    }
}
