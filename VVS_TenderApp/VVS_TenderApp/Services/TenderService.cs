using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    public class TenderService : ITenderService
    {
        private readonly DbClass _db; 

        public TenderService(DbClass db)
        {
            _db = db;
        }

        public void ValidirajIKreirajTender(int firmaId, string naziv, string opis, DateTime rokZaPrijavu, decimal procijenjenaVrijednost, List<Kriterij> kriteriji)
        {
            var firma = _db.DohvatiFirmu(firmaId);

            if (firma == null)
                throw new Exception("Firma ne postoji");

            if (string.IsNullOrWhiteSpace(naziv))
                throw new ArgumentException("Naziv tendera je obavezan");

            if (naziv.Length < 10)
                throw new ArgumentException("Naziv mora imati minimum 10 karaktera");

            if (string.IsNullOrWhiteSpace(opis))
                throw new ArgumentException("Opis tendera je obavezan");

            if (opis.Length < 50)
                throw new ArgumentException("Opis mora imati minimum 50 karaktera");

            if (rokZaPrijavu <= DateTime.Now)
                throw new ArgumentException("Rok za prijavu mora biti u budućnosti");

            if (rokZaPrijavu < DateTime.Now.AddDays(7))
                throw new ArgumentException("Rok mora biti minimum 7 dana unaprijed");

            if (rokZaPrijavu > DateTime.Now.AddYears(1))
                throw new ArgumentException("Rok ne može biti duži od 1 godine");

            if (procijenjenaVrijednost <= 0)
                throw new ArgumentException("Procijenjena vrijednost mora biti veća od 0");

            if (procijenjenaVrijednost < 1000)
                throw new ArgumentException("Minimalna vrijednost tendera je 1000 KM");

            if (procijenjenaVrijednost > 10000000)
                throw new ArgumentException("Maksimalna vrijednost tendera je 10.000.000 KM");

            var aktivniTenderi = _db.DohvatiTenderePoFirmi(firmaId)
                .Where(t => t.Status == StatusTendera.Otvoren);
            if (aktivniTenderi.Count() >= 10)
                throw new Exception("Ne možete imati više od 10 aktivnih tendera istovremeno");

            var postojeciTenderi = _db.DohvatiTenderePoFirmi(firmaId);
            if (postojeciTenderi.Any(t => t.Naziv.ToLower() == naziv.ToLower() &&
                                          t.Status == StatusTendera.Otvoren))
                throw new Exception("Već imate aktivan tender sa istim nazivom");

            decimal ukupno = kriteriji.Sum(k => k.Tezina);

            if (Math.Abs(ukupno - 1.0m) > 0.0001m)
                throw new Exception("Zbir težina za kriterije mora biti 1,0!");

            //kreira se tender ako je sve ok proslo
            var tender = new Tender
            {
                FirmaId = firmaId,
                Naziv = naziv,
                Opis = opis,
                DatumObjave = DateTime.Now,
                RokZaPrijavu = rokZaPrijavu,
                ProcijenjenaVrijednost = procijenjenaVrijednost,
                Status = StatusTendera.Otvoren
            };

            _db.DodajTender(tender);
        }

        public void AzurirajTender(int tenderId, int firmaId, string? noviNaziv,
                                   string? noviOpis, DateTime? noviRok, decimal? novaVrijednost)
        {
            var tender = _db.DohvatiTender(tenderId);

            if (tender == null)
                throw new Exception("Tender ne postoji");

            if (tender.FirmaId != firmaId)
                throw new UnauthorizedAccessException("Ne možete ažurirati tuđi tender");

            if (tender.Status != StatusTendera.Otvoren)
                throw new Exception("Možete ažurirati samo otvorene tendere");

            var ponude = _db.DohvatiPonudePoTenderu(tenderId);
            if (ponude.Count() > 0)
                throw new Exception("Ne možete ažurirati tender koji već ima ponude"); //OVO MI JE NEKAKO LOGIČNO AL AKO MISLITE 
                                                                                       //DA TREBA MOCI OBRIŠITEE

            //SVE KAO KAD SE KREIRALO, OVO IZNAD SAMO MANJE PROVJERA
            if (string.IsNullOrWhiteSpace(noviNaziv))
                noviNaziv = tender.Naziv;
            else if (noviNaziv.Length < 10)
                throw new ArgumentException("Naziv mora imati minimum 10 karaktera");

            if (string.IsNullOrWhiteSpace(noviOpis))
                noviOpis = tender.Opis;
            else if (noviOpis.Length < 50)
                throw new ArgumentException("Opis mora imati minimum 50 karaktera");

            if (noviRok.HasValue)
            {
                if (noviRok.Value <= DateTime.Now)
                    throw new ArgumentException("Rok mora biti u budućnosti");

                if (noviRok.Value < DateTime.Now.AddDays(7))
                    throw new ArgumentException("Rok mora biti minimum 7 dana unaprijed");

                tender.RokZaPrijavu = noviRok.Value;
            }
            if(novaVrijednost.HasValue)
            {
                if (novaVrijednost < 1000 || novaVrijednost > 10000000)
                    throw new ArgumentException("Vrijednost mora biti između 1.000 i 10.000.000 KM");
                tender.ProcijenjenaVrijednost = novaVrijednost.Value;
            }
           
            tender.Naziv = noviNaziv;
            tender.Opis = noviOpis;

            _db.AzurirajTender(tender);
        }


        public void ZatvoriTender(int tenderId, int firmaId)
        {
            var tender = _db.DohvatiTender(tenderId);

            if (tender == null)
                throw new Exception("Tender ne postoji");

            if (tender.FirmaId != firmaId)
                throw new UnauthorizedAccessException("Ne možete zatvoriti tuđi tender");

            if (tender.Status != StatusTendera.Otvoren)
                throw new Exception("Samo otvoreni tender može biti zatvoren");

            tender.Status = StatusTendera.Zatvoren;
            _db.AzurirajTender(tender);

            Console.WriteLine($"Tender '{tender.Naziv}' je zatvoren");
        }

        public void DodijeliTender(int tenderId, int ponudaId, int firmaId)
        {
            var tender = _db.DohvatiTender(tenderId);
            var ponuda = _db.DohvatiPonudu(ponudaId);

            if (tender == null)
                throw new Exception("Tender ne postoji");

            if (ponuda == null)
                throw new Exception("Ponuda ne postoji");

            if (tender.FirmaId != firmaId)
                throw new UnauthorizedAccessException("Ne možete dodijeliti tuđi tender");

            if (tender.Status != StatusTendera.Zatvoren)
                throw new Exception("Tender mora biti zatvoren prije dodjele");

            if (ponuda.TenderId != tenderId)
                throw new Exception("Ponuda ne pripada ovom tenderu");

            tender.Status = StatusTendera.Zavrsen;
            _db.AzurirajTender(tender);

            
            ponuda.Status = StatusPonude.Prihvacena;
            _db.AzurirajPonudu(ponuda);

            //ostale odbijene
            var ostalePounde = _db.DohvatiPonudePoTenderu(tenderId)
                .Where(p => p.Id != ponudaId);
            foreach (var p in ostalePounde)
            {
                p.Status = StatusPonude.Odbijena;
                _db.AzurirajPonudu(p);
            }

            var pobjednickaFirma = _db.DohvatiFirmu(ponuda.FirmaId);
            Console.WriteLine($"Tender '{tender.Naziv}' dodijeljen firmi {pobjednickaFirma.Naziv}");
            //Console.WriteLine("Tender '{tender.Naziv}' dodijeljen ponuđaču (Ponuda ID: {ponudaId})");
        }

        public void OtkaziTender(int tenderId, int firmaId, string razlog)
        {
            var tender = _db.DohvatiTender(tenderId);

            if (tender == null)
                throw new Exception("Tender ne postoji");

            if (tender.FirmaId != firmaId)
                throw new UnauthorizedAccessException("Ne možete otkazati tuđi tender");

            if (tender.Status == StatusTendera.Zavrsen)
                throw new Exception("Ne možete otkazati već dodijeljen tender");

            if (string.IsNullOrWhiteSpace(razlog))
                throw new ArgumentException("Razlog otkazivanja je obavezan");

            tender.Status = StatusTendera.Otkazan;
            _db.AzurirajTender(tender);

            var ponude = _db.DohvatiPonudePoTenderu(tenderId);
            foreach (var ponuda in ponude)
            {
                ponuda.Status = StatusPonude.Odbijena;
                _db.AzurirajPonudu(ponuda);
            }

            Console.WriteLine("Tender '{tender.Naziv}' otkazan. Razlog: {razlog}");
        }

        public void ObrisiTender(int tenderId)
        {
            var tender = _db.DohvatiTender(tenderId);

            if (tender == null)
                throw new Exception("Tender ne postoji");

            var ponude = _db.DohvatiPonudePoTenderu(tenderId);
            foreach (var ponuda in ponude)
            {
                _db.ObrisiPonudu(ponuda.Id);
            }

            _db.ObrisiTender(tenderId);
        }

        public List<Tender> DohvatiMojeTendere(int firmaId)
        {
            return _db.DohvatiTenderePoFirmi(firmaId);
        }

        public List<Tender> DohvatiTudjeTendere(int firmaId)
        {
            return _db.DohvatiAktivneTendere()
                .Where(t => t.FirmaId != firmaId)
                .ToList();
        }

        public List<Tender> DohvatiSveTendere()
        {
            return _db.DohvatiSveTendere();
        }

        public Tender DohvatiTender(int tenderId)
        {
            var tender = _db.DohvatiTender(tenderId);
            if (tender == null)
                throw new Exception("Tender ne postoji");

            return tender;
        }

    }
}
