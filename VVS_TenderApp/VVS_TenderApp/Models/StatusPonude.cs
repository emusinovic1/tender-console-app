using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VVS_TenderApp.Models
{
    public enum StatusPonude
    {
        NaCekanju, Prihvacena, Odbijena
    }

    public static class StatusPonudeExtension
    {
        public static string ToReadableString(this StatusPonude statusPonude)
        {
            return statusPonude switch
            {
                StatusPonude.NaCekanju => "Na cekanju",
                _ => statusPonude.ToString()
            };
        }
    }

}
