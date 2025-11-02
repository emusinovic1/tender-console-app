using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVS_TenderApp.Models;

namespace VVS_TenderApp.Services
{
    internal interface IAuthService
    {
        public Korisnik Prijava(string email, string lozinka);
        public void RegistracijaFirme(string nazivFirme, string pib, string adresa,string emailFirme, string telefon,
                                      string imeKorisnika, string emailKorisnika, string lozinka);
        public void PromijeniLozinku(int korisnikId, string staraLozinka, string novaLozinka);
        public bool EmailPostoji(string email);

        public bool ValidanEmailFormat(string email);

    }
}
