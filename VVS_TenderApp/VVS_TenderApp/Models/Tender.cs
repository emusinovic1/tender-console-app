using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVS_TenderApp.Models
{
    public class Tender
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string Naziv { get; set; }
        public string Opis { get; set; }
        public DateTime DatumObjave { get; set; }
        public DateTime RokZaPrijavu { get; set; }
        public decimal ProcijenjenaVrijednost { get; set; }
        public StatusTendera Status { get; set; }

        public List<Kriterij> Kriteriji { get; set; } = new List<Kriterij>();

    }
}
