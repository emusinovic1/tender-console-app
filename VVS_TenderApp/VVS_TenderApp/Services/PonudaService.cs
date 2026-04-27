using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    public class PonudaService : IPonudaService
    {
        private readonly DbClass _db;

        
        private const int MaxRejectedOffers = 10;
        private const int MaxActiveOffersPerCompany = 20;
        private const int MaxOffersPerTender = 50;
        private const decimal MinPriceFactor = 0.1m;
        private const decimal MaxPriceFactor = 5m;

        public PonudaService(DbClass db)
        {
            _db = db;
        }

        public void ValidirajIPosaljiPonudu(int tenderId, int firmaId, decimal iznos)
        {
            
            var trenutnoVrijeme = DateTime.Now;

            
            var tender = _db.DohvatiTender(tenderId);
            if (tender == null) throw new Exception("Tender ne postoji");

            var firma = _db.DohvatiFirmu(firmaId);
            if (firma == null) throw new Exception("Firma ne postoji");

            
            ValidirajStatusIRok(tender, trenutnoVrijeme);

            if (tender.FirmaId == firmaId)
                throw new Exception("Ne možete se prijaviti na sopstveni tender");

            
            ValidirajIznos(iznos, tender.ProcijenjenaVrijednost);
            ProvjeriLimiteIFirmaStatus(tenderId, firmaId);

            
            var novaPonuda = new Ponuda
            {
                TenderId = tenderId,
                FirmaId = firmaId,
                Iznos = iznos,
                DatumSlanja = trenutnoVrijeme,
                Status = StatusPonude.NaCekanju
            };

            _db.DodajPonudu(novaPonuda);

            Console.WriteLine($"Ponuda uspješno poslata!");
            Console.WriteLine($"Tender: {tender.Naziv}");
            Console.WriteLine($"Vaš iznos: {iznos:N2} KM");
        }

        
        private void ValidirajStatusIRok(Tender tender, DateTime vrijeme)
        {
            if (tender.Status != StatusTendera.Otvoren)
                throw new Exception("Tender nije otvoren za prijave");

            if (tender.RokZaPrijavu < vrijeme)
                throw new Exception("Rok za prijavu je istekao");
        }

        private void ValidirajIznos(decimal iznos, decimal procijenjenaVrijednost)
        {
            if (iznos <= 0)
                throw new ArgumentException("Iznos ponude mora biti veći od 0");

            decimal minimumIznos = procijenjenaVrijednost * MinPriceFactor;
            decimal maximumIznos = procijenjenaVrijednost * MaxPriceFactor;

            if (iznos < minimumIznos)
                throw new ArgumentException($"Iznos ponude je prenizak. Minimum: {minimumIznos:N2} KM");

            if (iznos > maximumIznos)
                throw new ArgumentException($"Iznos ponude je previsok. Maximum: {maximumIznos:N2} KM");
        }

        private void ProvjeriLimiteIFirmaStatus(int tenderId, int firmaId)
        {
            int brojOdbijenih = _db.DohvatiPonudePoFirmi(firmaId).Count(p => p.Status == StatusPonude.Odbijena);
            if (brojOdbijenih > MaxRejectedOffers)
                throw new Exception("Firma ima previše odbijenih ponuda u istoriji.");

            if (_db.DohvatiPonudePoTenderu(tenderId).Any(p => p.FirmaId == firmaId))
                throw new Exception("Već ste poslali ponudu za ovaj tender");

            int aktivne = _db.DohvatiPonudePoFirmi(firmaId).Count(p => p.Status == StatusPonude.NaCekanju);
            if (aktivne >= MaxActiveOffersPerCompany)
                throw new Exception($"Ne možete imati više od {MaxActiveOffersPerCompany} aktivnih ponuda istovremeno");

            if (_db.DohvatiPonudePoTenderu(tenderId).Count >= MaxOffersPerTender)
                throw new Exception($"Ovaj tender je dostigao maksimalan broj ponuda ({MaxOffersPerTender})");
        }

        public void AzurirajPonudu(int ponudaId, int firmaId, decimal? noviIznos)
        {
            var ponuda = _db.DohvatiPonudu(ponudaId);
            if (ponuda == null) throw new Exception("Ponuda ne postoji");

            if (ponuda.FirmaId != firmaId)
                throw new UnauthorizedAccessException("Ne možete ažurirati tuđu ponudu");

            if (ponuda.Status != StatusPonude.NaCekanju)
                throw new Exception("Možete ažurirati samo ponude koje su na čekanju");

            var tender = _db.DohvatiTender(ponuda.TenderId);
            ValidirajStatusIRok(tender, DateTime.Now);

            if (noviIznos.HasValue)
            {
                ValidirajIznos(noviIznos.Value, tender.ProcijenjenaVrijednost);
                ponuda.Iznos = noviIznos.Value;
            }

            _db.AzurirajPonudu(ponuda);
            Console.WriteLine($"Ponuda uspješno ažurirana!");
        }

        public void PovuciPonudu(int ponudaId, int firmaId)
        {
            var ponuda = _db.DohvatiPonudu(ponudaId);
            if (ponuda == null) throw new Exception("Ponuda ne postoji");

            if (ponuda.FirmaId != firmaId)
                throw new UnauthorizedAccessException("Ne možete povući tuđu ponudu");

            if (ponuda.Status != StatusPonude.NaCekanju)
                throw new Exception("Možete povući samo ponude koje su na čekanju");

            var tender = _db.DohvatiTender(ponuda.TenderId);
            if (tender.Status != StatusTendera.Otvoren)
                throw new Exception("Tender je već zatvoren, ne možete povući ponudu");

            _db.ObrisiPonudu(ponudaId);
            Console.WriteLine($"Ponuda uspješno povučena!");
        }

        public List<Ponuda> DohvatiMojePonude(int firmaId)
        {
            return _db.DohvatiPonudePoFirmi(firmaId)
                .OrderByDescending(p => p.DatumSlanja)
                .ToList();
        }

        public Ponuda DohvatiPonudu(int ponudaId)
        {
            var ponuda = _db.DohvatiPonudu(ponudaId);
            if (ponuda == null) throw new Exception("Ponuda ne postoji");
            return ponuda;
        }

        public List<Ponuda> RangirajPonude(int tenderId)
        {
            return _db.DohvatiPonudePoTenderu(tenderId)
                .Where(p => p.Status == StatusPonude.NaCekanju)
                .OrderBy(p => p.Iznos)
                .ToList();
        }

        public void OcijeniFirmu(int firmaId, int ocjena)
        {
            if (ocjena < 1 || ocjena > 5)
                throw new ArgumentException("Ocjena mora biti između 1 i 5.");

            var firma = _db.DohvatiFirmu(firmaId);
            if (firma == null) throw new Exception("Firma ne postoji.");

            _db.SnimiOcjenu(firmaId, ocjena);
        }
    }
}