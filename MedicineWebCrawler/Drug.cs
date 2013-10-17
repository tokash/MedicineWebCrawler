using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MedicineWebCrawler
{
    public class Drug
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ActiveIngredients { get; set; }
        public string InActiveIngredients { get; set; }
        public string SideEffects { get; set; }
    }
}
