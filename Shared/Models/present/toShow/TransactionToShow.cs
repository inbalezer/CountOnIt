using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CountOnIt.Shared.Models.present.toShow
{
    public class TransactionToShow
    {
        public int id { get; set; }
        public bool transType { get; set; }
        public double transValue { get; set; }
        public string valueType { get; set; }
        public DateTime transDate { get; set; }
        public bool fixedMonthly { get; set; }
        public int tagID { get; set; }
        public string transTitle { get; set; }
    }
}
