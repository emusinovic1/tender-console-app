using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    internal interface IPonudaService
    {
        public void ValidirajIPosaljiPonudu(int tenderId, int firmaId, decimal iznos);
        public void AzurirajPonudu(int ponudaId, int firmaId, decimal? noviIznos);
        public void PovuciPonudu(int ponudaId, int firmaId);
        public List<Ponuda> DohvatiMojePonude(int firmaId);
        public Ponuda DohvatiPonudu(int ponudaId);
        public List<Ponuda> RangirajPonude(int tenderId);
    }
}
