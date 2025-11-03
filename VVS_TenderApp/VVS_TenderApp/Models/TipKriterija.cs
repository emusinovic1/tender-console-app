using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VVS_TenderApp.Models
{
    public enum TipKriterija
    {
        Cijena,
        RokIsporuke,
        Garancija
    }

    public static class TipKriterijaExtensions
    {
        public static string ToReadableString(this TipKriterija tip)
        {
            return tip switch
            {
                TipKriterija.Cijena => "Cijena",
                TipKriterija.RokIsporuke => "Rok isporuke",
                TipKriterija.Garancija => "Garancija",
                _ => tip.ToString()
            };
        }
    }
   
}

