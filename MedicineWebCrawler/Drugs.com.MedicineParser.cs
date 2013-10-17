using System;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HtmlAgilityPack;

/*
 * Read page
 * look for name, description, side effects on home page
 * look for Active\InActive ingredients on profeesional page
*/

namespace MedicineWebCrawler
{
    public class DrugsComMedicineParser
    {
        string _StartParsePath;
        bool _BreakDrugLoop = false;

        public DrugsComMedicineParser(string i_StartParsePath)
        {
            _StartParsePath = i_StartParsePath;
        }

        public List<Drug> Parse(int i_StartIdx, int i_Count)
        {
            List<Drug> drugs = new List<Drug>();
            string[] filesToParse;

            if (Directory.Exists(_StartParsePath))
            {
                filesToParse = Directory.GetFiles(_StartParsePath);

                for (int i = i_StartIdx; i > 0 && i < i_Count + i_StartIdx && i < filesToParse.Length; i++)
                {
                    //string currFile = TextFilesCommon.ReadTextFileToString(filesToParse[i]);
                    drugs.Add(ParseSingleDrug(filesToParse[i]));
                }

            }

            return drugs;
        }

        public Drug ParseSingleDrug(string iDrugFile)
        {
            Drug drug = new Drug();
            HtmlDocument doc = new HtmlDocument();
            doc.Load(iDrugFile);

            try
            {
                //Get the drug name
                drug.Name = doc.DocumentNode.SelectSingleNode("//h1").InnerText;

                //Get the drug description
                drug.Description = doc.DocumentNode.SelectSingleNode("//p/@itemprop").InnerText;

                //now for the active\inactive ingredients, read the page from the "pro" directory
                HandleDrugIngredientsInformation(iDrugFile, ref drug);
                

                //Get side effects
                GetDrugSideEffects(ref drug, doc);
            }
            catch (Exception ex)
            {

            }

            return drug;
        }

        private void HandleDrugIngredientsInformation(string iDrugFile, ref Drug drug)
        {
            GetDrugIngredientsInformation(iDrugFile, ref drug);

            //if couldnt get drug ingredients from local site, search in the US national library of medicine
            //TODO: replace this with function
            if (drug.ActiveIngredients == "Active ingredients not found" ||
                drug.InActiveIngredients == "InActive ingredients not found")
            {
                DailyMedNLMServiceClient dailyMedService = new DailyMedNLMServiceClient();
                List<BasicDrugInformation> drugs = dailyMedService.RetrieveBasicDrugInformation(drug.Name);
                if (drugs.Count > 0)
                {
                    dailyMedService.ParseDrugWebPage(drugs[0].setid, ref drug);
                }
            }


        }

        private static void GetDrugSideEffects(ref Drug drug, HtmlDocument doc)
        {
            string htmlDoc = doc.DocumentNode.InnerHtml;
            string[] htmlParts = htmlDoc.Split('\n');
            List<String> sideEffects = new List<string>();
            List<String> htmlDocList = new List<string>();
            List<int> sideEffectsLocations = new List<int>();

            HtmlNodeCollection text = doc.DocumentNode.SelectNodes("//text()");
            foreach (HtmlNode node in text)
            {
                htmlDocList.Add(node.InnerText);
            }

            for (int i = 0; i < htmlDocList.Count; i++)
            {
                //Get emergency medical help if you have any of these signs of an allergic reaction, side effects
                if (htmlDocList[i].Contains("Get emergency medical help if you have any of these signs of an allergic reaction"))
                {
                    sideEffectsLocations.Add(i);
                }
            }

            if (sideEffectsLocations.Count > 0)
            {
                string sideEffectsInformation = string.Empty;
                int k = sideEffectsLocations[0];
                while (!htmlDocList[k].Contains("This is not a complete list of side effects and others may occur.") && k < htmlDocList.Count)
                {
                    if (htmlDocList[k] != string.Empty && htmlDocList[k] != "\n")
                    {
                        sideEffects.Add(htmlDocList[k]);
                        sideEffectsInformation += htmlDocList[k] + "\n";
                    }
                    k++;
                }

                drug.SideEffects = sideEffectsInformation;
            }
        }

        private void GetDrugIngredientsInformation(string iDrugFile, ref Drug drug)
        {
            try
            {
                HtmlDocument proDoc = new HtmlDocument();
                string pathDrugProInfo = Path.GetDirectoryName(iDrugFile);
                pathDrugProInfo = Path.GetDirectoryName(pathDrugProInfo);
                pathDrugProInfo += "\\pro\\";
                pathDrugProInfo = Path.Combine(pathDrugProInfo, Path.GetFileName(iDrugFile));
                string drugIngredientsRawData;

                if (File.Exists(pathDrugProInfo))
                {
                    proDoc.Load(pathDrugProInfo);

                    //now get the ingredients
                    HtmlNodeCollection nodes = proDoc.DocumentNode.SelectNodes("//table/@class");

                    //Locate Active ingredients
                    //need to split the contents of the table
                    //Find and Read active ingredients until reaching InActive ingredients
                    //Read InActive ingredients until reaching packaging
                    int i = 0;
                    while (i < nodes.Count && _BreakDrugLoop == false)
                    {
                        if (nodes[i].InnerText.Contains("Active Ingredient"))
                        {
                            drugIngredientsRawData = nodes[i].InnerText;
                            string[] drugInformation = drugIngredientsRawData.Split('\n');

                            for (int j = 0; j < drugInformation.Length; j++)
                            {
                                if (drugInformation[j] != string.Empty && drugInformation[j] != "\n")
                                {
                                    switch (drugInformation[j])
                                    {
                                        case "Active Ingredient/Active Moiety":
                                            j = j + 8;
                                            string[] stopFlagsActive = { "Inactive Ingredients" };
                                            string activeIngredients = HttpUtility.HtmlDecode(GetInformation(drugInformation, ref j, stopFlagsActive)).Trim();
                                            drug.ActiveIngredients = activeIngredients;
                                            break;
                                        case "Inactive Ingredients":
                                            j += 7;
                                            string[] stopFlagsInactive = { "Packaging", "Product Characteristics" };
                                            string inactiveIngredients = HttpUtility.HtmlDecode(GetInformation(drugInformation, ref j, stopFlagsInactive)).Trim();
                                            drug.InActiveIngredients = inactiveIngredients;
                                            _BreakDrugLoop = true;
                                            break;
                                        //case "Inactive Ingredients":
                                        default:
                                            break;
                                    }
                                }
                            }

                            break;
                        }

                        i++;
                    }

                    _BreakDrugLoop = false;
                }
                else //no active\inactive data for drug
                {
                    drug.ActiveIngredients = "Active ingredients not found";
                    drug.InActiveIngredients = "InActive ingredients not found";
                }

            }
            catch (Exception ex)
            {

            }
        }

        public string GetInformation(string[] i_DrugInformation, ref int i_StartIdx, string[] i_StopFlags)
        {
            string info = string.Empty;
            int i = i_StartIdx;
            string currLine = i_DrugInformation[i];
            List<String> informationParts = new List<string>();


            try
            {
                while (VerifyStopFlag(currLine, i_StopFlags))
                {
                    if (currLine != string.Empty && currLine != "\n" && currLine != "&nbsp;")
                    {
                        informationParts.Add(currLine.Replace("&nbsp;", ""));
                    }

                    if (i < i_DrugInformation.Length)
                    {
                        i++;
                        currLine = i_DrugInformation[i];
                    }
                    else
                    {
                        break;
                    }
                }

            }
            catch (Exception ex)
            {

            }

            foreach (string item in informationParts)
            {
                char[] cArr = item.ToCharArray();
                if (!char.IsNumber(cArr[0]))
                {
                    if (item.Contains("\n"))
                    {
                        info += item;
                    }
                    else
                    {
                        info += item + "\n";
                    }    
                }
                else
                {
                    info += string.Format("Dosage: {0}\n", item);
                }
            }

            return info;
        }

        private bool VerifyStopFlag(string data, string[] i_StopFlags)
        {
            bool isVerified = true;

            foreach (string flag in i_StopFlags)
            {
                if (data.Contains(flag))
                {
                    isVerified = false;
                }
            }

            return isVerified;
        }

        //private string GetInformation(string[] i_DrugInformation, ref int i_StartIdx, string i_StopFlag, string i_Separator)
        //{
        //    string info = string.Empty;
        //    int i = i_StartIdx;
        //    string currLine = i_DrugInformation[i];
            

        //    try
        //    {
        //        while (!currLine.Contains(i_StopFlag))
        //        {
        //            if (currLine != string.Empty && currLine != "\n")
        //            {
        //                info += currLine + i_Separator;// +"\n"; 
        //            }
                    
        //            if (i < i_DrugInformation.Length)
        //            {
        //                i++;
        //                currLine = i_DrugInformation[i];
        //            }
        //            else
        //            {
        //                break;
        //            }
        //        }

        //    }
        //    catch (Exception ex)
        //    {

        //    }

        //    return info;
        //}

    }
}
