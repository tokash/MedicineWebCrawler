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

        private static readonly string connStringInitial = "Server=TOKASHYOS-PC\\SQLEXPRESS;Integrated security=SSPI;database=master";
        private static readonly string connString = "Server=TOKASHYOS-PC\\SQLEXPRESS;Integrated security=SSPI;database=drugs";

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
        //private static readonly string SideEffectsTableSchema = "CREATE TABLE SideEffects (SideEffectID int IDENTITY(1,1), Name varchar(max) NOT NULL, Seriousness varchar(255), PRIMARY KEY (SideEffectID))";
        private static readonly string SideEffectsTableSchema = "CREATE TABLE SideEffects (SideEffectID int IDENTITY(1,1), Name varchar(max) NOT NULL, PRIMARY KEY (SideEffectID))";
        private static readonly string ActiveIngredientsTableSchema = "CREATE TABLE ActiveIngredients (ActiveIngredientID int IDENTITY(1,1), Name varchar(max) NOT NULL, PRIMARY KEY (ActiveIngredientID))";
        private static readonly string InactiveIngredientsTableSchema = "CREATE TABLE InactiveIngredients (InactiveIngredientID int IDENTITY(1,1), Name varchar(max) NOT NULL, PRIMARY KEY (InactiveIngredientID))";
        private static readonly string Drugs_ActiveIngredientsTableSchema = "CREATE TABLE Drugs_ActiveIngredients (DrugID int FOREIGN KEY REFERENCES Drugs(DrugID), ActiveIngredientID int FOREIGN KEY REFERENCES ActiveIngredients(ActiveIngredientID))";
        private static readonly string Drugs_InactiveIngredientsTableSchema = "CREATE TABLE Drugs_InactiveIngredients (DrugID int FOREIGN KEY REFERENCES Drugs(DrugID), InactiveIngredientID int FOREIGN KEY REFERENCES InactiveIngredients(InactiveIngredientID))";
        private static readonly string Drugs_SideEffectsTableSchema = "CREATE TABLE Drugs_SideEffects (DrugID int FOREIGN KEY REFERENCES Drugs(DrugID), SideEffectID int FOREIGN KEY REFERENCES SideEffects(SideEffectID))";

        private static readonly string[] DrugsTableColumns = {"Name"};
        //private static readonly string[] SideEffectsTableColumns = { "Name", "Seriousness" };
        private static readonly string[] SideEffectsTableColumns = { "Name"};
        private static readonly string[] ActiveIngredientsTableColumns = { "Name"};
        private static readonly string[] InactiveIngredientsTableColumns = { "Name" };
        private static readonly string[] Drugs_SideEffectsTableColumns = { "DrugID", "SideEffectID" };
        private static readonly string[] Drugs_ActiveIngredientsTableColumns = { "DrugID", "ActiveIngredientID" };
        private static readonly string[] Drugs_InactiveIngredientsTableColumns = { "DrugID", "InactiveIngredientID" };

        private static readonly string[] StringsToIgnore = { 
                                                              "Call your doctor at once if you have"
                                                           };

        private static readonly List<String> InvalidInputs = new List<string>(new string[] { "\n", "", "&nbsp;" });
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
                    if (currentDrug != null)
                    {
                        if (currentDrug.ActiveIngredients != null)
                        {
                            drugs.Add(currentDrug);
                            AddDrugToDatabase(currentDrug);  
                        }
                    }
                }
            }

            return drugs;
        }

        public Drug ParseSingleDrug(string iDrugFile)
        {
            Drug drug = null;
            HtmlDocument doc = new HtmlDocument();
            doc.Load(iDrugFile);

            try
            {
                if (doc.DocumentNode.InnerText != "")
                {
                    drug = new Drug();

                    //Get the drug name
                    drug.Name = doc.DocumentNode.SelectSingleNode("//h1").InnerText;

                    //Get the drug description
                    drug.Description = doc.DocumentNode.SelectSingleNode("//p/@itemprop").InnerText;

                    //now for the active\inactive ingredients, read the page from the "pro" directory
                    HandleDrugIngredientsInformation(iDrugFile, ref drug);

                    //Get side effects
                    GetDrugSideEffects(ref drug, doc);
                }
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
            if (ioDrug.ActiveIngredients.Count == 0 ||
                ioDrug.InActiveIngredients.Count == 0)
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
                if (htmlDocList[i].Contains("Get emergency medical help if you have any of these"))
                {
                    sideEffectsLocations.Add(i);
                }
            }

            if (sideEffectsLocations.Count > 0)
            {
                string sideEffectsInformation = string.Empty;
                int k = sideEffectsLocations[0];
                while ((!htmlDocList[k].Contains("This is not a complete list of side effects and others may occur.") && !htmlDocList[k].Contains("What should I discuss with my health care provider before using ")) && k < htmlDocList.Count)
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

            string[] wordsToRemove = { "or", "and"};//"or ", " or"};

            //sanitize
            //split on ;
            //remove conjunction words
            //add to freshly sanitized and split list

            List<string> SanitizedRawSideEffects = new List<string>();
            foreach (string SideEffect in iRawSideEffects)
            {
                string sanitizedString = SanitizeString(SideEffect, "", StringsToIgnore.ToList<String>());

                if (sanitizedString != String.Empty)
                {
                    SanitizedRawSideEffects.Add(sanitizedString);
                }
            }

            List<string> SanitizedSideEffects = new List<string>();
            foreach (string Line in SanitizedRawSideEffects)
            {
                string[] parts = Line.Split(';');

                List<string> sanitizedParts = new List<string>();

                foreach (string item in parts)
                {
                    string s = RemoveUnnecessaryWordsFromString(item, wordsToRemove);
                    s = s.Trim();
                    s = RemoveUnnecessaryWordsFromString(s, wordsToRemove);
                    if (s != string.Empty)
                    {
                        sanitizedParts.Add(s); 
                    }
                }

                SanitizedSideEffects.AddRange(sanitizedParts);
            }

            foreach (string SideEffect in SanitizedSideEffects)
            {
                if (regExpSerious.IsMatch(SideEffect, 0) && isSeriousVisited == false)
                {
                    sideEffects.Add(new SideEffect() { Name = SideEffect, Level = Seriousness.Serious });

                    isSeriousVisited = true;
                }
                else if (regExpLessSerious.IsMatch(SideEffect, 0) && isLessSeriousVisited == false)
                {
                    sideEffectSeriousness = 1;

                    isLessSeriousVisited = true;
                }
                else if (regExpCommon.IsMatch(SideEffect, 0))
                {
                    sideEffectSeriousness = 2;
                }
                else
                {
                    if (SideEffect != string.Empty)
                    {
                        switch (sideEffectSeriousness)
                        {
                            case 0:
                                sideEffects.Add(new SideEffect() { Name = SideEffect, Level = Seriousness.Serious });
                                break;
                            case 1:
                                sideEffects.Add(new SideEffect() { Name = SideEffect, Level = Seriousness.LessSerious });
                                break;
                            case 2:
                                sideEffects.Add(new SideEffect() { Name = SideEffect, Level = Seriousness.Common });
                                break;
                        } 
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
                string pattern = word + " +";
                if (Regex.IsMatch(value, pattern))// || word == value)
                {
                    value = value.Replace(word, "");
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
                        currValue = value.Replace(word, "");
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
                                            string[] stopFlagsActive = { "Inactive Ingredients", "Packaging" };
                                            ioDrug.ActiveIngredients = GetInformation(drugInformation, ref j, stopFlagsActive);//HttpUtility.HtmlDecode(GetInformation(drugInformation, ref j, stopFlagsActive)).Trim();

                                            break;
                                        case "Inactive Ingredients":
                                            j += 7;
                                            string[] stopFlagsInactive = { "Packaging", "Product Characteristics" };
                                            ioDrug.InActiveIngredients = GetInformation(drugInformation, ref j, stopFlagsInactive);//HttpUtility.HtmlDecode(GetInformation(drugInformation, ref j, stopFlagsInactive)).Trim();
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

            }
            catch (Exception ex)
            {

            }
        }

        public List<string> GetInformation(string[] iDrugInformation, ref int ioStartIdx, string[] iStopFlags)
        {
            string info = string.Empty;
            int i = ioStartIdx;
            string currLine = iDrugInformation[i];
            List<String> informationParts = new List<string>();


            try
            {
                while (VerifyStopFlag(currLine, iStopFlags))
                {
                    if (IsValidInput(currLine, InvalidInputs))
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

            int k = 0;
            List<string> ingredientsNames = new List<string>();
            for (int j = 0; j < informationParts.Count; j++)
            {
                if (Regex.IsMatch(informationParts[j], @"^[^0-9].+$"))
                {
                    if (ingredientsNames.FindAll(delegate(string s) { return s.Contains(informationParts[j]); }).Count == 0)
                    {
                        ingredientsNames.Add(informationParts[j]);
                        k++;

                        //string sanitizedString = SanitizeString(informationParts[j], "", StringsToIgnore.ToList<String>());

                        //if (sanitizedString != String.Empty)
                        //{
                        //    ingredientsNames.Add(informationParts[j]);
                        //    k++; 
                        //} 
                    }
                }
            }

            return ingredientsNames;
        }

        private string SanitizeString(string iOriginalString, string iCharsToRemove, List<string> iStringsToIgnore)
        {
            string newString = iOriginalString.Trim();

            //remove leading\trailing whitespaces
            newString = TrimPunctuation(newString);

            foreach (string StringToIgnore in iStringsToIgnore)
	        {
		        if (newString.Contains(StringToIgnore))
	            {
		            newString = string.Empty;
                    break;
	            }
	        }

            return newString;
        }

        static string TrimPunctuation(string value)
        {
	        // Count start punctuation.
	        int removeFromStart = 0;
	        for (int i = 0; i < value.Length; i++)
	        {
	            if (char.IsPunctuation(value[i]))
	            {
		            removeFromStart++;
	            }
	            else
	            {
		            break;
	            }
	        }

	        // Count end punctuation.
	        int removeFromEnd = 0;
	        for (int i = value.Length - 1; i >= 0; i--)
	        {
	            if (char.IsPunctuation(value[i]))
	            {
		            removeFromEnd++;
	            }
	            else
	            {
		            break;
	            }
	        }
	        // No characters were punctuation.
	        if (removeFromStart == 0 &&
	            removeFromEnd == 0)
	        {
	            return value;
	        }
	        // All characters were punctuation.
	        if (removeFromStart == value.Length &&
	            removeFromEnd == value.Length)
	        {
	            return "";
	        }
	        // Substring.
	        return value.Substring(removeFromStart,
	            value.Length - removeFromEnd - removeFromStart);
            }

        private bool IsValidInput(string iInput, List<String> iInvalidInputs)
        {
            bool isValidInput = true;

            foreach (string invalidInput in iInvalidInputs)
            {
                if (iInput == invalidInput)
                {
                    isValidInput = false;
                    break;
                }
            }

            return isValidInput;
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
                AddSideEffectsToDB(iDrug);

                //Add record\s to active ingredients
                AddActiveIngredientsToDB(iDrug);

                //Add record\s to inactive ingredients
                AddInactiveIngredientsToDB(iDrug);

                //Add record\s to drugs_side effects
                AddDrugs_SideEffectsToDB(iDrug);

                //Add record\s to drugs_active ingredients
                AddDrugs_ActiveIngredientsToDB(iDrug);

                //Add record\s to drugs_inactive ingredients
                AddDrugs_InactiveIngredientsToDB(iDrug);
            }
            
        }

        private static void AddDrugs_InactiveIngredientsToDB(Drug iDrug)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            if (iDrug.InActiveIngredients != null)
            {
                foreach (string InactiveIngredient in iDrug.InActiveIngredients)
                {
                    try
                    {
                        string drugsQuery = string.Format("Select DrugID from {0} where {1} = {2};", "Drugs", "Name", "'" + iDrug.Name.Replace("'", "") + "'");
                        string inactiveIngredientsQuery = string.Format("Select InactiveIngredientID from {0} where {1} = {2};", "InactiveIngredients", "Name", "'" + InactiveIngredient.Replace("'", "") + "'");

                        int drugID = -1;
                        int inactiveIngredientID = -1;

                        DataTable drugsResults = SQLServerCommon.SQLServerCommon.ExecuteQuery(drugsQuery, connString);
                        if (drugsResults.Rows.Count == 1)
                        {
                            drugID = int.Parse((drugsResults.Rows[0]["DrugID"]).ToString());
                        }
                        DataTable inactiveIngredientsResults = SQLServerCommon.SQLServerCommon.ExecuteQuery(inactiveIngredientsQuery, connString);
                        if (inactiveIngredientsResults.Rows.Count == 1)
                        {
                            inactiveIngredientID = int.Parse((inactiveIngredientsResults.Rows[0]["InactiveIngredientID"]).ToString());
                        }

                        parameters.Add(String.Format("@{0}", Drugs_InactiveIngredientsTableColumns[0]), drugID.ToString());
                        parameters.Add(String.Format("@{0}", Drugs_InactiveIngredientsTableColumns[1]), inactiveIngredientID.ToString());

                        string query = String.Format("Select 1 from {0} where {1} = {2} AND {3} = {4};", "Drugs_InactiveIngredients", "DrugID", drugID.ToString(), "InactiveIngredientID", inactiveIngredientID.ToString());

                        DataTable dt = SQLServerCommon.SQLServerCommon.ExecuteQuery(query, connString);
                        if (dt.Rows.Count == 0)
                        {
                            SQLServerCommon.SQLServerCommon.Insert("Drugs_InactiveIngredients", connString, Drugs_InactiveIngredientsTableColumns, parameters);
                        }

                        parameters.Clear();
                    }
                    catch (Exception ex)
                    {

                        throw;
                    }
                }
            }
        }

        private static void AddDrugs_ActiveIngredientsToDB(Drug iDrug)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            if (iDrug.ActiveIngredients != null)
            {
                foreach (string ActiveIngredient in iDrug.ActiveIngredients)
                {
                    try
                    {
                        string drugsQuery = string.Format("Select DrugID from {0} where {1} = {2};", "Drugs", "Name", "'" + iDrug.Name.Replace("'", "") + "'");
                        string activeIngredientsQuery = string.Format("Select ActiveIngredientID from {0} where {1} = {2};", "ActiveIngredients", "Name", "'" + ActiveIngredient.Replace("'", "") + "'");

                        int drugID = -1;
                        int activeIngredientID = -1;

                        DataTable drugsResults = SQLServerCommon.SQLServerCommon.ExecuteQuery(drugsQuery, connString);
                        if (drugsResults.Rows.Count == 1)
                        {
                            drugID = int.Parse((drugsResults.Rows[0]["DrugID"]).ToString());
                        }
                        DataTable activeIngredientsResults = SQLServerCommon.SQLServerCommon.ExecuteQuery(activeIngredientsQuery, connString);
                        if (activeIngredientsResults.Rows.Count == 1)
                        {
                            activeIngredientID = int.Parse((activeIngredientsResults.Rows[0]["ActiveIngredientID"]).ToString());
                        }

                        parameters.Add(String.Format("@{0}", Drugs_ActiveIngredientsTableColumns[0]), drugID.ToString());
                        parameters.Add(String.Format("@{0}", Drugs_ActiveIngredientsTableColumns[1]), activeIngredientID.ToString());

                        string query = String.Format("Select 1 from {0} where {1} = {2} AND {3} = {4};", "Drugs_ActiveIngredients", "DrugID", drugID.ToString(), "ActiveIngredientID", activeIngredientID.ToString());

                        DataTable dt = SQLServerCommon.SQLServerCommon.ExecuteQuery(query, connString);
                        if (dt.Rows.Count == 0)
                        {
                            SQLServerCommon.SQLServerCommon.Insert("Drugs_ActiveIngredients", connString, Drugs_ActiveIngredientsTableColumns, parameters);
                        }

                        parameters.Clear();
                    }
                    catch (Exception ex)
                    {

                        throw;
                    }
                }
            }
        }

        private static void AddDrugs_SideEffectsToDB(Drug iDrug)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            if (iDrug.SideEffectsList != null)
            {
                foreach (SideEffect SideEffect in iDrug.SideEffectsList)
                {
                    try
                    {
                        string drugsQuery = string.Format("Select DrugID from {0} where {1} = {2};", "Drugs", "Name", "'" + iDrug.Name.Replace("'", "") + "'");
                        string sideEffectsQuery = string.Format("Select SideEffectID from {0} where {1} = {2};", "SideEffects", "Name", "'" + SideEffect.Name.Replace("'", "") + "'"); ;

                        int drugID = -1;
                        int sideEffectID = -1;

                        DataTable drugsResults = SQLServerCommon.SQLServerCommon.ExecuteQuery(drugsQuery, connString);
                        if (drugsResults.Rows.Count == 1)
                        {
                            drugID = int.Parse((drugsResults.Rows[0]["DrugID"]).ToString());
                        }
                        DataTable sideEffectsResults = SQLServerCommon.SQLServerCommon.ExecuteQuery(sideEffectsQuery, connString);
                        if (sideEffectsResults.Rows.Count == 1)
                        {
                            sideEffectID = int.Parse((sideEffectsResults.Rows[0]["SideEffectID"]).ToString());
                        }

                        parameters.Add(String.Format("@{0}", Drugs_SideEffectsTableColumns[0]), drugID.ToString());
                        parameters.Add(String.Format("@{0}", Drugs_SideEffectsTableColumns[1]), sideEffectID.ToString());

                        string query = String.Format("Select 1 from {0} where {1} = {2} AND {3} = {4};", "Drugs_SideEffects", "DrugID", drugID.ToString(), "SideEffectID", sideEffectID.ToString());

                        DataTable dt = SQLServerCommon.SQLServerCommon.ExecuteQuery(query, connString);
                        if (dt.Rows.Count == 0)
                        {
                            SQLServerCommon.SQLServerCommon.Insert("Drugs_SideEffects", connString, Drugs_SideEffectsTableColumns, parameters);
                        }

                        parameters.Clear();
                    }
                    catch (Exception ex)
                    {

                        throw;
                    }
                }
            }
        }

        private static void AddInactiveIngredientsToDB(Drug iDrug)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            if (iDrug.InActiveIngredients != null)
            {
                foreach (string InactiveIngredient in iDrug.InActiveIngredients)
                {
                    try
                    {
                        parameters.Add(String.Format("@{0}", InactiveIngredientsTableColumns[0]), InactiveIngredient.Replace("'", ""));

                        string query = String.Format("Select 1 from {0} where {1} = {2};", "InactiveIngredients", "Name", "'" + InactiveIngredient.Replace("'", "") + "'");

                        DataTable dt = SQLServerCommon.SQLServerCommon.ExecuteQuery(query, connString);
                        if (dt.Rows.Count == 0)
                        {
                            SQLServerCommon.SQLServerCommon.Insert("InactiveIngredients", connString, InactiveIngredientsTableColumns, parameters);
                        }

                        parameters.Clear();
                    }
                    catch (Exception ex)
                    {

                        throw;
                    }
                }
            }
        }

        private static void AddActiveIngredientsToDB(Drug iDrug)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            if (iDrug.ActiveIngredients!= null)
            {
                foreach (string ActiveIngredient in iDrug.ActiveIngredients)
                {
                    try
                    {
                        parameters.Add(String.Format("@{0}", ActiveIngredientsTableColumns[0]), ActiveIngredient.Replace("'", ""));

                        string query = String.Format("Select 1 from {0} where {1} = {2};", "ActiveIngredients", "Name", "'" + ActiveIngredient.Replace("'", "") + "'");

                        DataTable dt = SQLServerCommon.SQLServerCommon.ExecuteQuery(query, connString);
                        if (dt.Rows.Count == 0)
                        {
                            SQLServerCommon.SQLServerCommon.Insert("ActiveIngredients", connString, ActiveIngredientsTableColumns, parameters);
                        }

                        parameters.Clear();
                    }
                    catch (Exception ex)
                    {

                        throw;
                    }
                }
            }
        }

        private static void AddSideEffectsToDB(Drug iDrug)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            if (iDrug.SideEffectsList != null)
            {
                foreach (SideEffect currSideEffect in iDrug.SideEffectsList)
                {
                    try
                    {
                        parameters.Add(String.Format("@{0}", SideEffectsTableColumns[0]), currSideEffect.Name.Replace("'", ""));
                        //parameters.Add(String.Format("@{0}", SideEffectsTableColumns[1]), currSideEffect.Level.ToString());

                        string query = String.Format("Select 1 from {0} where {1} = {2};", "SideEffects", "Name", "'" + currSideEffect.Name.Replace("'", "") + "'");

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
