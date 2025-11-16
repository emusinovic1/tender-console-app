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
        public PonudaService(DbClass db)
        {
            _db = db;
        }
        public void ValidirajIPosaljiPonudu(int tenderId, int firmaId, decimal iznos)
        {
            var tender = _db.DohvatiTender(tenderId);
            if (tender == null)
                throw new Exception("Tender ne postoji");

            if (tender.Status != StatusTendera.Otvoren)
                throw new Exception("Tender nije otvoren za prijave");

            if (tender.RokZaPrijavu < DateTime.Now)
                throw new Exception("Rok za prijavu je istekao");

            var firma = _db.DohvatiFirmu(firmaId);
            if (firma == null)
                throw new Exception("Firma ne postoji");

            if (tender.FirmaId == firmaId)
                throw new Exception("Ne možete se prijaviti na sopstveni tender");


            if (iznos <= 0)
                throw new ArgumentException("Iznos ponude mora biti veći od 0");

            decimal minimumIznos = tender.ProcijenjenaVrijednost * 0.1m;
            if (iznos < minimumIznos)
                throw new ArgumentException($"Iznos ponude je prenizak. Minimum: {minimumIznos:N2} KM");

            //iznos previsok (više od 500% procijenjene vrijednosti)
            decimal maximumIznos = tender.ProcijenjenaVrijednost * 5m;
            if (iznos > maximumIznos)
                throw new ArgumentException($"Iznos ponude je previsok. Maximum: {maximumIznos:N2} KM");

            var postojecaPonuda = _db.DohvatiPonudePoTenderu(tenderId)
                .FirstOrDefault(p => p.FirmaId == firmaId);
            if (postojecaPonuda != null)
                throw new Exception("Već ste poslali ponudu za ovaj tender");

            var aktivnePonude = _db.DohvatiPonudePoFirmi(firmaId)
                .Where(p => p.Status == StatusPonude.NaCekanju);
            if (aktivnePonude.Count() >= 20)
                throw new Exception("Ne možete imati više od 20 aktivnih ponuda istovremeno");

            //mozda nam i ne treba, skontamo
            var ponudeNaTender = _db.DohvatiPonudePoTenderu(tenderId);
            if (ponudeNaTender.Count >= 50)
                throw new Exception("Ovaj tender je dostigao maksimalan broj ponuda (50)");

            
            var ponuda = new Ponuda
            {
                TenderId = tenderId,
                FirmaId = firmaId,
                Iznos = iznos,
                DatumSlanja = DateTime.Now,
                Status = StatusPonude.NaCekanju
            };

            _db.DodajPonudu(ponuda);

            Console.WriteLine($"Ponuda uspješno poslata!");
            Console.WriteLine($"Tender: {tender.Naziv}");
            Console.WriteLine($"Vaš iznos: {iznos:N2} KM");
        }

        public void AzurirajPonudu(int ponudaId, int firmaId, decimal? noviIznos)
        {
            var ponuda = _db.DohvatiPonudu(ponudaId);

            if (ponuda == null)
                throw new Exception("Ponuda ne postoji");

            if (ponuda.FirmaId != firmaId)
                throw new UnauthorizedAccessException("Ne možete ažurirati tuđu ponudu");

            if (ponuda.Status != StatusPonude.NaCekanju)
                throw new Exception("Možete ažurirati samo ponude koje su na čekanju");

            var tender = _db.DohvatiTender(ponuda.TenderId);

            if (tender.Status != StatusTendera.Otvoren)
                throw new Exception("Tender nije više otvoren");

            if (tender.RokZaPrijavu < DateTime.Now)
                throw new Exception("Rok za prijavu je istekao");

            if (noviIznos <= 0)
                throw new ArgumentException("Iznos mora biti veći od 0");

            decimal minimumIznos = tender.ProcijenjenaVrijednost * 0.1m;
            if (noviIznos < minimumIznos)
                throw new ArgumentException($"Iznos je prenizak. Minimum: {minimumIznos:N2} KM");

            decimal maximumIznos = tender.ProcijenjenaVrijednost * 5m;
            if (noviIznos > maximumIznos)
                throw new ArgumentException($"Iznos je previsok. Maximum: {maximumIznos:N2} KM");

            if(noviIznos.HasValue)
                ponuda.Iznos = noviIznos.Value;

            _db.AzurirajPonudu(ponuda);

            Console.WriteLine($"Ponuda uspješno ažurirana!");
        }


        public void PovuciPonudu(int ponudaId, int firmaId)
        {
            var ponuda = _db.DohvatiPonudu(ponudaId);

            if (ponuda == null)
                throw new Exception("Ponuda ne postoji");

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
            if (ponuda == null)
                throw new Exception("Ponuda ne postoji");

            return ponuda;
        }

        public List<Ponuda> RangirajPonude(int tenderId)
        {
            var ponude = _db.DohvatiPonudePoTenderu(tenderId)
                .Where(p => p.Status == StatusPonude.NaCekanju)
                .OrderBy(p => p.Iznos)  // Od najjeftinije
                .ToList();

            return ponude;
        }
    }
}
