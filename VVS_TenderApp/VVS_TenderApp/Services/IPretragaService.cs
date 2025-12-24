using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    internal interface IPretragaService
    {
        List<Tender> NaprednaPretraga(string kljucnaRijec, decimal? minVrijednost, decimal? maxVrijednost, StatusTendera? status,
                                     DateTime? datumOd);

        List<Tender> PretraziPoKljucnojRijeci(string kljucnaRijec);

        List<Tender> PretraziPoVrijednosti(decimal minVrijednost, decimal maxVrijednost);

        List<Tender> PretraziPoStatusu(StatusTendera status);

        List<Tender> PretraziPoDatumu(DateTime datumOd, DateTime datumDo);

        List<Tender> SortirajPoNazivu(List<Tender> tenderi, bool uzlazno = true);

        List<Tender> SortirajPoVrijednosti(List<Tender> tenderi, bool uzlazno = true);

        List<Tender> SortirajPoRoku(List<Tender> tenderi, bool uzlazno = true);

        List<Tender> SortirajPoDatumu(List<Tender> tenderi, bool uzlazno = true);

    }
}
