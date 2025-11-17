using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVS_TenderApp.Models
{
    public class Firma
    {
        public int Id { get; set; }
        public string Naziv { get; set; }
        public string PIB { get; set; } //poresko identifikacioni broj
        public string Adresa { get; set; }
        public string Email { get; set; }
        public string Telefon { get; set; }
        public List<int> Ocjene { get; set; } = new List<int>();
        public double ProsjecnaOcjena =>
            Ocjene.Count == 0 ? 0 : Ocjene.Average();

    }
}
