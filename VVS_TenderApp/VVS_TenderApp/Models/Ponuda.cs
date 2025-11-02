using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVS_TenderApp.Models
{
    public class Ponuda
    {
        public int Id { get; set; }
        public int TenderId { get; set; }
        public int FirmaId { get; set; }
        public decimal Iznos { get; set; }

        public DateTime DatumSlanja { get; set; }
        public StatusPonude Status {  get; set; }

        //public StatusPonude Status { get; set; }
    }
}
