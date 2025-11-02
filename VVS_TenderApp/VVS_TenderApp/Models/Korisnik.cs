using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVS_TenderApp.Models
{
    public class Korisnik
    {
        public int Id { get; set; }
        public string Ime { get; set; }
        public string Prezime { get; set; }
        public string Email { get; set; }
        public string LozinkaHash { get; set; }
        public Uloga Uloga { get; set; }
        public int? FirmaId { get; set; }
    }
}
