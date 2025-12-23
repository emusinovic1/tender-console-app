using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;

namespace VVS_TenderApp.Tests;

[TestClass]
public class RangiranjeServiceWhiteBoxTests
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

    private Ponuda Ponuda(int id, int tenderId, decimal iznos,
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

    // ???????????????????????????????????????????????????????????????
    // DODATNE HELPER METODE ZA WHITE BOX TESTIRANJE
    // ???????????????????????????????????????????????????????????????

    // Helper za kreiranje tendera BEZ kriterija (za Test 1)
    private Tender CreateTenderBezKriterija()
    {
        return new Tender
        {
            Id = 10,
            Naziv = "Tender za nabavku",
            Kriteriji = new List<Kriterij>() // prazna lista
        };
    }

    // Helper za kreiranje tendera sa INVALID kriterijima (za Test 3)
    private Tender CreateTenderSaInvalidnimKriterijima()
    {
        return new Tender
        {
            Id = 10,
            Naziv = "Tender za nabavku",
            Kriteriji = new List<Kriterij>
                {
                    null, // null kriterij
                    new Kriterij { Tip = TipKriterija.Cijena, Tezina = 0m },    // Tezina = 0
                    new Kriterij { Tip = TipKriterija.Cijena, Tezina = -10m },  // Tezina < 0
                    new Kriterij { Tip = TipKriterija.Cijena, Tezina = 100m }   // validna
                }
        };
    }

    // Helper za kreiranje tendera sa SAMO jednim tipom kriterija
    private Tender CreateTenderSaKriterijem(TipKriterija tip, decimal tezina)
    {
        return new Tender
        {
            Id = 10,
            Naziv = "Tender za nabavku",
            Kriteriji = new List<Kriterij>
                {
                    new Kriterij { Tip = tip, Tezina = tezina }
                }
        };
    }

    // Helper za setup Mock-a (kombinuje DohvatiTender i DohvatiPonudePoTenderu)
    private void SetupMock(Tender tender, List<Ponuda> ponude)
    {
        _mockDb.Setup(db => db.DohvatiTender(It.IsAny<int>())).Returns(tender);
        _mockDb.Setup(db => db.DohvatiPonudePoTenderu(It.IsAny<int>())).Returns(ponude);
    }

    // ???????????????????????????????????????????????????????????????
    // WHITE BOX TESTOVI
    // ???????????????????????????????????????????????????????????????

    // TEST 1: Nema ponuda - rani return
    [TestMethod]
    public void Test1_NemaPonuda_RaniReturn()
    {
        // Arrange
        var tender = CreateTenderSaKriterijima();
        var ponude = new List<Ponuda>(); // prazna lista
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(0, rezultat.Count);

        // Pokriva: Branch odluka 1 (TRUE - !ponude.Any())
    }

    // TEST 2: Nema kriterija - rani return
    [TestMethod]
    public void Test2_NemaKriterija_RaniReturn()
    {
       
        var tender = CreateTenderBezKriterija();
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1000m, 30, 12)
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(0, rezultat.Count);

        
    }

    // TEST 3: Invalidne ponude - null, negativan iznos, negativan rok
    [TestMethod]
    public void Test3_InvalidnePonude_Continue()
    {
        // Arrange
        var tender = CreateTenderSaKriterijem(TipKriterija.Cijena, 100m);
        var ponude = new List<Ponuda>
            {
                null,
                Ponuda(2, 10, 0m, 30, 12),     // Iznos = 0
                Ponuda(3, 10, -100m, 30, 12),  // Iznos < 0
                Ponuda(4, 10, 1000m, -5, 12),  // RokIsporuke < 0
                Ponuda(5, 10, 2000m, 30, 12)   // validna
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(1, rezultat.Count);
        Assert.AreEqual(5, rezultat[0].ponuda.Id);

        
    }

    // TEST 4: Invalidni kriteriji - null, negativna težina
    [TestMethod]
    public void Test4_InvalidniKriteriji_Continue()
    {
        // Arrange
        var tender = CreateTenderSaInvalidnimKriterijima();
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1000m, 30, 12)
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(1, rezultat.Count);
        Assert.IsTrue(rezultat[0].skor > 0); // samo validni kriterij primijenjen

       
    }

    // TEST 5: Poredenje ponuda - jeftinija vs skuplja
    [TestMethod]
    public void Test5_UsporedBaPonuda_ObjeGrane()
    {
        // Arrange
        var tender = CreateTenderSaKriterijem(TipKriterija.Cijena, 100m);
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1500m, 30, 12), // srednja
                Ponuda(2, 10, 1000m, 30, 12), // jeftinija
                Ponuda(3, 10, 2000m, 30, 12)  // skuplja
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(3, rezultat.Count);
        Assert.AreEqual(2, rezultat[0].ponuda.Id); // jeftinija prva

       
    }

    // TEST 6: Kriterij CIJENA
    [TestMethod]
    public void Test6_KriterijCijena_Proracun()
    {
        // Arrange
        var tender = CreateTenderSaKriterijem(TipKriterija.Cijena, 100m);
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1000m, 30, 12),
                Ponuda(2, 10, 2000m, 30, 12)
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(2, rezultat.Count);
        Assert.AreEqual(1, rezultat[0].ponuda.Id); // jeftinija ima viši skor
        Assert.IsTrue(rezultat[0].skor > rezultat[1].skor);

        
    }

    // TEST 7: Kriterij ROK ISPORUKE
    [TestMethod]
    public void Test7_KriterijRokIsporuke_Proracun()
    {
        // Arrange
        var tender = CreateTenderSaKriterijem(TipKriterija.RokIsporuke, 100m);
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1000m, 10, 12), // kra?i rok
                Ponuda(2, 10, 1000m, 30, 12)
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(2, rezultat.Count);
        Assert.AreEqual(1, rezultat[0].ponuda.Id); // kra?i rok je bolji
        Assert.IsTrue(rezultat[0].skor > rezultat[1].skor);

        
    }

    // TEST 8: Kriterij GARANCIJA
    [TestMethod]
    public void Test8_KriterijGarancija_Proracun()
    {
        // Arrange
        var tender = CreateTenderSaKriterijem(TipKriterija.Garancija, 100m);
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1000m, 30, 24), // duža garancija
                Ponuda(2, 10, 1000m, 30, 12)
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(2, rezultat.Count);
        Assert.AreEqual(1, rezultat[0].ponuda.Id); // duža garancija je bolja
        Assert.IsTrue(rezultat[0].skor > rezultat[1].skor);

    }

    // TEST 9: Svi kriteriji zajedno
    [TestMethod]
    public void Test9_SviKriteriji_KompleksnaPutanja()
    {
        // Arrange
        var tender = CreateTenderSaKriterijima(); // koristi postoje?u metodu
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1000m, 10, 24), // najbolja kombinacija
                Ponuda(2, 10, 2000m, 30, 12), // najgora kombinacija
                Ponuda(3, 10, 1500m, 20, 18)  // srednja
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(3, rezultat.Count);
        Assert.AreEqual(1, rezultat[0].ponuda.Id); // najbolja kombinacija

        
    }

    // TEST 10: Sortiranje - SWAP se dešava
    [TestMethod]
    public void Test10_Sortiranje_SwapGrana()
    {
        // Arrange
        var tender = CreateTenderSaKriterijem(TipKriterija.Cijena, 100m);
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 5000m, 30, 12), // najgora
                Ponuda(2, 10, 3000m, 30, 12),
                Ponuda(3, 10, 1000m, 30, 12), // najbolja
                Ponuda(4, 10, 4000m, 30, 12),
                Ponuda(5, 10, 2000m, 30, 12)
            };
        SetupMock(tender, ponude);

        // Act
        var rezultat = _service.RangirajPonude(10);

        // Assert
        Assert.AreEqual(5, rezultat.Count);
        Assert.AreEqual(3, rezultat[0].ponuda.Id); // najjeftinija nakon sortiranja
        Assert.AreEqual(5, rezultat[1].ponuda.Id);
        Assert.AreEqual(2, rezultat[2].ponuda.Id);
        Assert.AreEqual(4, rezultat[3].ponuda.Id);
        Assert.AreEqual(1, rezultat[4].ponuda.Id); // najskuplja

        // Provjera da je sortiranje ispravno
        for (int i = 0; i < rezultat.Count - 1; i++)
        {
            Assert.IsTrue(rezultat[i].skor >= rezultat[i + 1].skor);
        }

        
    }

    // TEST 11: Sortiranje - BEZ SWAP (ve? sortirano)
    [TestMethod]
    public void Test11_Sortiranje_BezSwap()
    {
        // Arrange
        var tender = CreateTenderSaKriterijem(TipKriterija.Cijena, 100m);
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1000m, 30, 12), //  najbolja
                Ponuda(2, 10, 2000m, 30, 12),
                Ponuda(3, 10, 3000m, 30, 12)  //  najgora
            };
        SetupMock(tender, ponude);

        
        var rezultat = _service.RangirajPonude(10);

        Assert.AreEqual(3, rezultat.Count);
        Assert.AreEqual(1, rezultat[0].ponuda.Id); 
        Assert.AreEqual(2, rezultat[1].ponuda.Id);
        Assert.AreEqual(3, rezultat[2].ponuda.Id);

        
    }

    // TEST 12: LOOP - 1 element (ne ulazi u sortiranje)
    [TestMethod]
    public void Test12_LoopPonude_JednaIteracija()
    {
        var tender = CreateTenderSaKriterijem(TipKriterija.Cijena, 100m);
        var ponude = new List<Ponuda>
            {
                Ponuda(1, 10, 1000m, 30, 12)
            };
        SetupMock(tender, ponude);

       
        var rezultat = _service.RangirajPonude(10);

       
        Assert.AreEqual(1, rezultat.Count);
        Assert.AreEqual(1, rezultat[0].ponuda.Id);

     
    }
    [TestMethod]
    public void Test13_NullPonuda_Continue()
    {
        var tender = CreateTenderSaKriterijem(TipKriterija.Cijena, 100m);
        var ponude = new List<Ponuda>
    {
        null,  
        Ponuda(2, 10, 1000m, 30, 12)
    };
        SetupMock(tender, ponude);

        var rezultat = _service.RangirajPonude(10);

        Assert.AreEqual(1, rezultat.Count);
        Assert.AreEqual(2, rezultat[0].ponuda.Id);
    }
    [TestMethod]
    public void Test14_NegativnaGarancija_Continue()
    {
        var tender = CreateTenderSaKriterijem(TipKriterija.Garancija, 100m);
        var ponude = new List<Ponuda>
    {
        Ponuda(1, 10, 1000m, 30, 0),   
        Ponuda(2, 10, 1000m, 30, 12)
    };
        SetupMock(tender, ponude);

        var rezultat = _service.RangirajPonude(10);

        Assert.AreEqual(1, rezultat.Count);
        Assert.AreEqual(2, rezultat[0].ponuda.Id);
    }
}

