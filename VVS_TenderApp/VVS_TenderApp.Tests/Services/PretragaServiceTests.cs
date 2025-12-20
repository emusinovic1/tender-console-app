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

        // Helper za kreiranje tendera (važno: Naziv i Opis ne smiju biti null jer se radi ToLower()).
        private Tender CreateTender(int id, string naziv, string opis)
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

        // =====================================================================
        //                  WHITE - BOX TESTIRANJE: PZ 2
        // =====================================================================



        // =====================================================================
        //                  Metoda: PretraziPoKljucnojRijeci                                        
        //                  Radila: Emina Zubetljak
        // =====================================================================

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
                CreateTender(1, "Tender A", "Opis A"),
                CreateTender(2, "Tender B", "Opis B")
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
                CreateTender(1, "Hello", "World")
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
                CreateTender(1, "xx abc yy", "nista")
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
            var t1 = CreateTender(1, "xx abc yy", "nista");
            var t2 = CreateTender(2, "abc - tender", "nista");

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
            var tContains = CreateTender(1, "xx abc yy", "nista");
            var tStarts = CreateTender(2, "abc - tender", "nista");
            var tEquals = CreateTender(3, "abc", "nista");

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
            var t1 = CreateTender(1, "nista", "neki abc tekst");
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
            var t3 = CreateTender(1, "nista", OpisWithRepeats("abc", 3));
            var t4 = CreateTender(2, "nista", OpisWithRepeats("abc", 4));

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
            var t4 = CreateTender(1, "nista", OpisWithRepeats("abc", 4));
            var t5 = CreateTender(2, "nista", OpisWithRepeats("abc", 5));

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
            var t = CreateTender(1, "xx abc yy", "abc nesto abc jos");

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
            var tLow = CreateTender(1, "nista", "abc");
            var tHigh = CreateTender(2, "abc", "nista");

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
            var tA = CreateTender(1, "abc - tender", "nista");
            var tB = CreateTender(2, "xx abc yy", "abc abc"); // 2 pojavljivanja u opisu

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
            var t = CreateTender(1, "AbC Tender", "Opis");
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
