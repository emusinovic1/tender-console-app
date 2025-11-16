using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    internal interface ITenderService
    {
        public void ValidirajIKreirajTender(int firmaId, string naziv, string opis, DateTime rokZaPrijavu,
            decimal procijenjenaVrijednost, List<Kriterij> kriteriji);
        public void AzurirajTender(int tenderId, int firmaId, string? noviNaziv,
                                   string? noviOpis, DateTime? noviRok, decimal? novaVrijednost);
        public void ZatvoriTender(int tenderId, int firmaId);
        public void DodijeliTender(int tenderId, int ponudaId, int firmaId);

        public void OtkaziTender(int tenderId, int firmaId, string razlog);
        public void ObrisiTender(int tenderId);
        public List<Tender> DohvatiMojeTendere(int firmaId);
        public List<Tender> DohvatiTudjeTendere(int firmaId);
        public List<Tender> DohvatiSveTendere();
        public Tender DohvatiTender(int tenderId);

        Ponuda AutomatskiDodijeliTender(int tenderId, int firmaId);
    }
}

