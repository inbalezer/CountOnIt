using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CountOnIt.Shared.Models.present.toShow
{
    public class UserStreakData
    {
        public bool AllWeeksValid { get; set; }
        public int WeeksWithThreeOrMoreTransactions { get; set; }
    }
}
