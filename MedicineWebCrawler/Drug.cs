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
        public List<SideEffect> SideEffectsList { get; set; }
    }

    public class SideEffects
    {
        public List<string> Serious = new List<string>();
        public List<string> LessSerious = new List<string>();
        public List<string> Common = new List<string>();
    }

    public class SideEffect
    {
        public string Name { get; set; }
        public Seriousness Level { get; set; }
    }

    public enum Seriousness
    {
        Serious,
        LessSerious,
        Common
    };
}
