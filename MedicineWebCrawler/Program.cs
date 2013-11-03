using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

/*
 *Download information from site - done (downloaded www.drugs.com)
 *Read drugs and do the following:
 *1.For name, description -> go through these directories: cdi, mtm, cons, ppa, drp
 *2.for Side effects and Active\InActive Ingredients go through the "pro" directory, if the medicine page doesn't exist in "pro",
 *  i cannot retrieve its
 *  Active\InActive Ingredients, it's side effects can be found on the "home" page taken from one of the directories above
*/


namespace MedicineWebCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            DailyMedNLMServiceClient dmsc = new DailyMedNLMServiceClient();
            List<BasicDrugInformation> drugInfo = null;
            Stopwatch watch = new Stopwatch();

            //drugInfo = dmsc.RetrieveBasicDrugInformation("4-way");
            //dmsc.ParseDrugWebPage(drugInfo[0].setid);

            //MedicineWebCrawler.Crawler crawler = new Crawler(new Uri("http://www.drugs.com/alpha"));
            //MedicineWebCrawler.Crawler crawler = new Crawler(new Uri("http://www.rxlist.com/drugs/alpha_c.htm"));
            List<Drug> drugs = null;

            watch.Start();
            ////crawler.Crawl();
            DrugsComMedicineParser drugsParser = new DrugsComMedicineParser(@"d:\www.drugs.com\mtm", false);

            drugs = drugsParser.Parse(1, 500);
            watch.Stop();

            Console.WriteLine(String.Format("Time elapsed since program start: {0}", watch.Elapsed));

            //using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"drugs.txt"))
            //{
            //    foreach (Drug drug in drugs)
            //    {
            //        file.WriteLine(String.Format("Name: {0}\n", drug.Name));
            //        //file.WriteLine("\n");
            //        file.WriteLine(String.Format("Description: {0}\n", drug.Description));
            //        //file.WriteLine("\n");
            //        file.WriteLine(String.Format("Active ingredients: \n{0}\n", drug.ActiveIngredients));
            //        //file.WriteLine("\n");
            //        file.WriteLine(String.Format("Inactive ingredients: \n{0}\n", drug.InActiveIngredients));
            //        //file.WriteLine("\n");
            //        file.WriteLine(String.Format("Side effects: \n{0}\n", drug.SideEffects));
            //        file.WriteLine("----------------------------------------------------------------------------\n\n");
            //    }
            //}

            //foreach (Drug drug in drugs)
            //{
            //    Console.WriteLine(String.Format("Name: {0}\n", drug.Name));
            //    Console.WriteLine(String.Format("Description: {0}\n", drug.Description));
            //    Console.WriteLine(String.Format("Active ingredients: \n{0}\n", drug.ActiveIngredients));
            //    Console.WriteLine(String.Format("Inactive ingredients: \n{0}\n", drug.InActiveIngredients));
            //    Console.WriteLine(String.Format("Side effects: \n{0}\n", drug.SideEffects));
            //}

            //Console.ReadLine();
        }

         
        
    }
}
