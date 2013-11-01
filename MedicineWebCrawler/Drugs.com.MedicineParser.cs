using System;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HtmlAgilityPack;
using SQLServerCommon;
using System.Text.RegularExpressions;
using System.Data;

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
        private bool _IsSearchOnline; //determines if the program will search for drugs online, default = true

        private static readonly string connStringInitial = "Server=TOKASHYO-PC\\SQLEXPRESS;Integrated security=SSPI;database=master";
        private static readonly string connString = "Server=TOKASHYO-PC\\SQLEXPRESS;Integrated security=SSPI;database=drugs";

        private static readonly string sqlCommandCreateDB = "CREATE DATABASE Drugs ON PRIMARY " +
                "(NAME = Drugs, " +
                "FILENAME = 'D:\\Drugs.mdf', " +
                "SIZE = 2MB, MAXSIZE = 10MB, FILEGROWTH = 10%) " +
                "LOG ON (NAME = Drugs_LOG, " +
                "FILENAME = 'D:\\Drugs.ldf', " +
                "SIZE = 1MB, " +
                "MAXSIZE = 100MB, " +
                "FILEGROWTH = 10%)";

        private static readonly string drugsTableSchema = "CREATE TABLE Drugs (DrugID int IDENTITY(1,1), Name varchar(max) NOT NULL, PRIMARY KEY (DrugID))";
        private static readonly string SideEffectsTableSchema = "CREATE TABLE SideEffects (SideEffectID int IDENTITY(1,1), Name varchar(max) NOT NULL, Seriousness varchar(255), PRIMARY KEY (SideEffectID))";
        private static readonly string ActiveIngredientsTableSchema = "CREATE TABLE ActiveIngredients (ActiveIngredientID int IDENTITY(1,1), Name varchar(max) NOT NULL, PRIMARY KEY (ActiveIngredientID))";
        private static readonly string InactiveIngredientsTableSchema = "CREATE TABLE InactiveIngredients (InactiveIngredientID int IDENTITY(1,1), Name varchar(max) NOT NULL, PRIMARY KEY (InactiveIngredientID))";
        private static readonly string Drugs_ActiveIngredientsTableSchema = "CREATE TABLE Drugs_ActiveIngredients (DrugID int FOREIGN KEY REFERENCES Drugs(DrugID), ActiveIngredientID int FOREIGN KEY REFERENCES ActiveIngredients(ActiveIngredientID))";
        private static readonly string Drugs_InactiveIngredientsTableSchema = "CREATE TABLE Drugs_InactiveIngredients (DrugID int FOREIGN KEY REFERENCES Drugs(DrugID), InactiveIngredientID int FOREIGN KEY REFERENCES InactiveIngredients(InactiveIngredientID))";
        private static readonly string Drugs_SideEffectsTableSchema = "CREATE TABLE Drugs_SideEffects (DrugID int FOREIGN KEY REFERENCES Drugs(DrugID), SideEffectID int FOREIGN KEY REFERENCES SideEffects(SideEffectID))";

        private static readonly string[] DrugsTableColumns = {"Name"};
        private static readonly string[] SideEffectsTableColumns = { "Name", "Seriousness" };

        public DrugsComMedicineParser(string iStartParsePath, bool iSearchOnline = true)
        {
            _StartParsePath = iStartParsePath;
            _IsSearchOnline = iSearchOnline;
        }

        public List<Drug> Parse(int iStartIdx, int iCount)
        {
            List<Drug> drugs = new List<Drug>();
            string[] filesToParse;

            if (Directory.Exists(_StartParsePath))
            {
                filesToParse = Directory.GetFiles(_StartParsePath);

                for (int i = iStartIdx; i > 0 && i < iCount + iStartIdx && i < filesToParse.Length; i++)
                {
                    //string currFile = TextFilesCommon.ReadTextFileToString(filesToParse[i]);
                    Drug currentDrug = ParseSingleDrug(filesToParse[i]);
                    drugs.Add(currentDrug);
                    AddDrugToDatabase(currentDrug);
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

        private void HandleDrugIngredientsInformation(string iDrugFile, ref Drug ioDrug)
        {
            GetDrugIngredientsInformation(iDrugFile, ref ioDrug);

            //if couldnt get drug ingredients from local site, search in the US national library of medicine
            //TODO: replace this with function
            if (ioDrug.ActiveIngredients == "Active ingredients not found" ||
                ioDrug.InActiveIngredients == "InActive ingredients not found")
            {

                if (_IsSearchOnline == true)
                {
                    DailyMedNLMServiceClient dailyMedService = new DailyMedNLMServiceClient();
                    List<BasicDrugInformation> drugs = dailyMedService.RetrieveBasicDrugInformation(ioDrug.Name);
                    if (drugs.Count > 0)
                    {
                        dailyMedService.ParseDrugWebPage(drugs[0].setid, ref ioDrug);
                    } 
                }
            }


        }

        private void GetDrugSideEffects(ref Drug ioDrug, HtmlDocument iDoc)
        {
            string htmlDoc = iDoc.DocumentNode.InnerHtml;
            string[] htmlParts = htmlDoc.Split('\n');
            List<String> sideEffects = new List<string>();
            List<String> htmlDocList = new List<string>();
            List<int> sideEffectsLocations = new List<int>();

            HtmlNodeCollection text = iDoc.DocumentNode.SelectNodes("//text()");
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
                while ((!htmlDocList[k].Contains("This is not a complete list of side effects and others may occur.") || !htmlDocList[k].Contains("What should I discuss with my health care provider before using ")) && k < htmlDocList.Count)
                {
                    if (htmlDocList[k] != string.Empty && htmlDocList[k] != "\n")
                    {
                        sideEffects.Add(htmlDocList[k]);
                        sideEffectsInformation += htmlDocList[k] + "\n";
                    }
                    k++;
                }

                sideEffects.RemoveAt(0);
                
                //sideEffectsInformation;
            }

            ioDrug.SideEffectsList = ParseSideEffects(sideEffects);
        }

        private List<SideEffect> ParseSideEffects(List<string> iRawSideEffects)
        {
            List<SideEffect> sideEffects = new List<SideEffect>();

            bool isSeriousVisited = false;
            bool isLessSeriousVisited = false;
            int sideEffectSeriousness = 0;

            Regex regExpSerious = new Regex("serious", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            Regex regExpLessSerious = new Regex("less serious", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            Regex regExpCommon = new Regex("common", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            string[] wordsToRemove = { };//"or ", " or"};

            foreach (string SideEffect in iRawSideEffects)
            {
                if (regExpSerious.IsMatch(SideEffect, 0) && isSeriousVisited == false)//SideEffect.Contains("serious"))
                {
                    List<string> values = ParseValuesFromString(SideEffect);
                    List<SideEffect> tempSideEffects = new List<MedicineWebCrawler.SideEffect>();
                    values = RemoveUnnecessaryWordsFromList(values, wordsToRemove);

                    foreach (string value in values)
                    {
                        tempSideEffects.Add(new SideEffect() { Name = value, Level = Seriousness.Serious});
                    }

                    sideEffects.AddRange(tempSideEffects);

                    isSeriousVisited = true;
                }
                else if (regExpLessSerious.IsMatch(SideEffect, 0) && isLessSeriousVisited == false)
                {
                    sideEffectSeriousness = 1;
                    List<string> values = ParseValuesFromString(SideEffect);
                    List<SideEffect> tempSideEffects = new List<MedicineWebCrawler.SideEffect>();
                    values = RemoveUnnecessaryWordsFromList(values, wordsToRemove);

                    foreach (string value in values)
                    {
                        tempSideEffects.Add(new SideEffect() { Name = value, Level = Seriousness.LessSerious });
                    }

                    sideEffects.AddRange(tempSideEffects);

                    isLessSeriousVisited = true;
                }
                else if (regExpCommon.IsMatch(SideEffect, 0))
                {
                    sideEffectSeriousness = 2;
                    List<string> values = ParseValuesFromString(SideEffect);
                    List<SideEffect> tempSideEffects = new List<MedicineWebCrawler.SideEffect>();
                    values = RemoveUnnecessaryWordsFromList(values, wordsToRemove);

                    foreach (string value in values)
                    {
                        tempSideEffects.Add(new SideEffect() { Name = value, Level = Seriousness.Common });
                    }

                    sideEffects.AddRange(tempSideEffects);
                }
                else
                {
                    string value = SideEffect;
                    int location = value.IndexOf(";");
                    if (location >= 0)
                    {
                        value = value.Remove(location, value.Length - location);
                    }

                    switch (sideEffectSeriousness)
                    {
                        case 0:
                            //sideEffects.Serious.AddRange(ParseValuesFromString(SideEffect));
                            sideEffects.Add(new SideEffect(){Name = value, Level = Seriousness.Serious});
                            break;
                        case 1:
                            //sideEffects.LessSerious.AddRange(ParseValuesFromString(SideEffect));
                            value = RemoveUnnecessaryWordsFromString(value, wordsToRemove);
                            sideEffects.Add(new SideEffect() { Name = value, Level = Seriousness.LessSerious });
                            break;
                        case 2:
                            //sideEffects.Common.AddRange(ParseValuesFromString(SideEffect));
                            value = RemoveUnnecessaryWordsFromString(value, wordsToRemove);
                            sideEffects.Add(new SideEffect() { Name = value, Level = Seriousness.Common });
                            break;
                    }
                }
            }

            return sideEffects;
        }

        //private SideEffects ParseSideEffects(List <string> iRawSideEffects)
        //{
        //    SideEffects sideEffects = new SideEffects();
        //    int sideEffectSeriousness = 0; //0 (default) - serious, 1 - less serious, 2 - common
        //    bool isSeriousVisited = false;
        //    bool isLessSeriousVisited = false;
           
        //    Regex regExpSerious = new Regex("serious", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        //    Regex regExpLessSerious = new Regex("less serious", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        //    Regex regExpCommon = new Regex("common", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        //    string[] wordsToRemove = {};//"or ", " or"};

        //    foreach (string SideEffect in iRawSideEffects)
        //    {
        //        if (regExpSerious.IsMatch(SideEffect, 0) && isSeriousVisited == false)//SideEffect.Contains("serious"))
        //        {
        //            List<string> values = ParseValuesFromString(SideEffect);
        //            values = RemoveUnnecessaryWordsFromList(values, wordsToRemove);
        //            sideEffects.Serious.AddRange(values);

        //            isSeriousVisited = true;
        //        }
        //        else if (regExpLessSerious.IsMatch(SideEffect, 0) && isLessSeriousVisited == false)
        //        {
        //            sideEffectSeriousness = 1;
        //            List<string> values = ParseValuesFromString(SideEffect);
        //            values = RemoveUnnecessaryWordsFromList(values, wordsToRemove);
        //            sideEffects.LessSerious.AddRange(values);
        //            isLessSeriousVisited = true;
        //        }
        //        else if (regExpCommon.IsMatch(SideEffect, 0))
        //        {
        //            sideEffectSeriousness = 2;
        //            List<string> values = ParseValuesFromString(SideEffect);
        //            values = RemoveUnnecessaryWordsFromList(values, wordsToRemove);
        //            sideEffects.Common.AddRange(values);
        //        }
        //        else
        //        {
        //            string value = SideEffect;
        //            int location = value.IndexOf(";");
        //            if (location >= 0)
        //            {
        //                value = value.Remove(location, value.Length - location);
        //            }

        //            switch (sideEffectSeriousness)
        //            {
        //                case 0:
        //                    //sideEffects.Serious.AddRange(ParseValuesFromString(SideEffect));
        //                    sideEffects.Serious.Add(value);
        //                    break;
        //                case 1:
        //                    //sideEffects.LessSerious.AddRange(ParseValuesFromString(SideEffect));
        //                    value = RemoveUnnecessaryWordsFromString(value, wordsToRemove);
        //                    sideEffects.LessSerious.Add(value);
        //                    break;
        //                case 2:
        //                    //sideEffects.Common.AddRange(ParseValuesFromString(SideEffect));
        //                    value = RemoveUnnecessaryWordsFromString(value, wordsToRemove);
        //                    sideEffects.Common.Add(value);
        //                    break;
        //            }
        //        }
        //    }

        //    return sideEffects;
        //}

        private string RemoveUnnecessaryWordsFromString(string iValue, string[] iRemoveTheseWords)
        {
            string value = iValue;

            foreach (string word in iRemoveTheseWords)
            {
                if (value.Contains(word))
                {
                    int location = value.IndexOf(word);
                    value = value.Remove(location, word.Length);
                }
            }

            return value;
        }

        private List<string> RemoveUnnecessaryWordsFromList(List<string> iValues, string[] iRemoveTheseWords)
        {
            foreach (string value in iValues)
            {
                string currValue = value;

                foreach (string word in iRemoveTheseWords)
                {
                    if (value.Contains(word))
                    {
                        int location = value.IndexOf(word);
                        currValue = value.Remove(location, word.Length + 1);
                        iValues.Remove(value);
                        iValues.Add(currValue);
                    }
                }
            }

            return iValues;
        }

        private List<String> ParseValuesFromString(string iRawString)
        {
            List<string> values = new List<string>();

            string rawString = iRawString;
            int count = rawString.IndexOf("such as", 0);

            if (count > 0)
            {
                rawString = rawString.Remove(0, count + "such as".Length + 1); //removing to and including "such as:" == 8 chars 
            }

            string[] rawSplit = rawString.Split(',');

            if (rawSplit.Length > 1)
            {
                int i = 0;
                while (i < rawSplit.Length)
                {
                    values.Add(rawSplit[i]);
                    i++;
                }
            }

            return values;
        }

        private void GetDrugIngredientsInformation(string iDrugFile, ref Drug ioDrug)
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
                                            ioDrug.ActiveIngredients = activeIngredients;
                                            break;
                                        case "Inactive Ingredients":
                                            j += 7;
                                            string[] stopFlagsInactive = { "Packaging", "Product Characteristics" };
                                            string inactiveIngredients = HttpUtility.HtmlDecode(GetInformation(drugInformation, ref j, stopFlagsInactive)).Trim();
                                            ioDrug.InActiveIngredients = inactiveIngredients;
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
                    ioDrug.ActiveIngredients = "Active ingredients not found";
                    ioDrug.InActiveIngredients = "InActive ingredients not found";
                }

            }
            catch (Exception ex)
            {

            }
        }

        public string GetInformation(string[] iDrugInformation, ref int ioStartIdx, string[] iStopFlags)
        {
            string info = string.Empty;
            int i = ioStartIdx;
            string currLine = iDrugInformation[i];
            List<String> informationParts = new List<string>();


            try
            {
                while (VerifyStopFlag(currLine, iStopFlags))
                {
                    if (currLine != string.Empty && currLine != "\n" && currLine != "&nbsp;")
                    {
                        informationParts.Add(currLine.Replace("&nbsp;", ""));
                    }

                    if (i < iDrugInformation.Length)
                    {
                        i++;
                        currLine = iDrugInformation[i];
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

        private bool VerifyStopFlag(string iData, string[] iStopFlags)
        {
            bool isVerified = true;

            foreach (string flag in iStopFlags)
            {
                if (iData.Contains(flag))
                {
                    isVerified = false;
                }
            }

            return isVerified;
        }

        private void AddDrugToDatabase(Drug iDrug)
        {
            if (!SQLServerCommon.SQLServerCommon.IsDatabaseExists(connStringInitial, "Drugs"))
            {
                CreateEmptyDB();
            }
            
            if(SQLServerCommon.SQLServerCommon.IsDatabaseExists(connStringInitial, "Drugs"))
            {
                //Add record to drugs
                AddDrugToDB(iDrug);

                //Add record\s to side effects
                Dictionary<string, string> parameters = new Dictionary<string, string>();

                if (iDrug.SideEffectsList != null)
                {
                    foreach (SideEffect currSideEffect in iDrug.SideEffectsList)
                    {
                        try
                        {
                            parameters.Add(String.Format("@{0}", SideEffectsTableColumns[0]), currSideEffect.Name);
                            parameters.Add(String.Format("@{0}", SideEffectsTableColumns[1]), currSideEffect.Level.ToString());

                            string query = String.Format("Select 1 from {0} where {1} = {2};", "SideEffects", "Name", "'" + currSideEffect.Name + "'");

                            DataTable dt = SQLServerCommon.SQLServerCommon.ExecuteQuery(query, connString);
                            if (dt.Rows.Count == 0)
                            {
                                SQLServerCommon.SQLServerCommon.Insert("SideEffects", connString, SideEffectsTableColumns, parameters);
                            }

                            parameters.Clear();
                        }
                        catch (Exception ex)
                        {

                            throw;
                        }
                    } 
                }

                //Add record\s to active ingredients
                //Add record\s to inactive ingredients
                //Add record\s to drugs_side effects
                //Add record\s to drugs_active ingredients
                //Add record\s to drugs_inactive ingredients
                //SQLServerCommon.SQLServerCommon.ExecuteNonQuery( "" , connString);
            }
            
        }

        private static void AddDrugToDB(Drug iDrug)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            foreach (string column in DrugsTableColumns)
            {
                parameters.Add(String.Format("@{0}", column), iDrug.Name);
            }

            try
            {
                DataTable dt = SQLServerCommon.SQLServerCommon.ExecuteQuery(String.Format("select 1 from {0} where {1} = {2};", "Drugs", "Name", "'" + iDrug.Name.Replace("'", "") + "'"), connString);
                if (dt.Rows.Count == 0)
                {
                    SQLServerCommon.SQLServerCommon.Insert("Drugs", connString, DrugsTableColumns, parameters);
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        private void CreateEmptyDB()
        {
            try
            {
                //Create DB
                if (!SQLServerCommon.SQLServerCommon.IsDatabaseExists(connStringInitial, "Drugs"))
                {
                    SQLServerCommon.SQLServerCommon.ExecuteNonQuery(sqlCommandCreateDB, connStringInitial);

                    //Create tables upon DB creation
                    SQLServerCommon.SQLServerCommon.ExecuteNonQuery(drugsTableSchema, connString);
                    SQLServerCommon.SQLServerCommon.ExecuteNonQuery(SideEffectsTableSchema, connString);
                    SQLServerCommon.SQLServerCommon.ExecuteNonQuery(ActiveIngredientsTableSchema, connString);
                    SQLServerCommon.SQLServerCommon.ExecuteNonQuery(InactiveIngredientsTableSchema, connString);
                    SQLServerCommon.SQLServerCommon.ExecuteNonQuery(Drugs_ActiveIngredientsTableSchema, connString);
                    SQLServerCommon.SQLServerCommon.ExecuteNonQuery(Drugs_InactiveIngredientsTableSchema, connString);
                    SQLServerCommon.SQLServerCommon.ExecuteNonQuery(Drugs_SideEffectsTableSchema, connString);
                }
            }
            catch (Exception)
            {
                throw;
            }
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
