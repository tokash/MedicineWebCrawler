using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using ServiceStack.Text.Json;
using RestSharp;
using RestSharp.Deserializers;
using HtmlAgilityPack;
using System.Web;

namespace MedicineWebCrawler
{
    /*TODO:
     * 1.Call the web service and retrieve the drug information according to its name (SPL)
     * 2.Retrieve the permanent url for a specific drug (using the drug setid)
     * 3.Parse the drug information
    */

    public class BasicDrugInformation
	{
        public string setid {get; set;}
        public string spl_version {get; set;}
        public DateTime published_date { get; set; }
        public string title { get; set; }
    }

    public class RootObject
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; }
    }

    class DailyMedNLMServiceClient
    {
        private readonly string WebServiceSPLListByDrugName = @"/v1/drugname/<drug_name>/spls.json";
        private readonly string DailyMedWebService = @"http://dailymed.nlm.nih.gov/dailymed/services";
        private readonly string DailyMedLookup = @"http://dailymed.nlm.nih.gov/dailymed/lookup.cfm?setid=<product setid>";

        bool _BreakDrugLoop = false;

        public List<BasicDrugInformation> RetrieveBasicDrugInformation(string iDrugName)
        {
            List<BasicDrugInformation> drugInfo = new List<BasicDrugInformation>();

            var client = new RestClient(DailyMedWebService);
            client.AddHandler("text/plain", new JsonDeserializer());

            string requestURI = WebServiceSPLListByDrugName.Replace("<drug_name>", iDrugName);
            var request = new RestRequest(requestURI, Method.GET);
            request.RequestFormat = DataFormat.Json;
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            

            try
            {
                var response = client.Execute(request);
                var records = ServiceStack.Text.JsonSerializer.DeserializeFromString<List<RootObject>>(response.Content);

                if (records != null && records.Count > 0)
                {
                    var drugData = records[0].DATA;

                    foreach (var item in drugData)
                    {
                        BasicDrugInformation drugInformation = new BasicDrugInformation();

                        drugInformation.setid = item[0].ToString(); //SETID
                        drugInformation.title = item[1].ToString(); //TITLE
                        drugInformation.spl_version = item[2].ToString(); //SPL_VERSION
                        drugInformation.published_date = DateTime.Parse(item[3].ToString()); //PUBLISHED_DATE

                        drugInfo.Add(drugInformation);
                    }
                }
            }
            catch (Exception ex)
            {

            }

            return drugInfo;
        }

        public void ParseDrugWebPage(string iDrugID, ref Drug ioDrug)
        {
            //Drug drug = new Drug();
            DrugsComMedicineParser drugComMedicineParser = new DrugsComMedicineParser("");
            string drugIngredientsRawData;

            string drugURL = DailyMedLookup.Replace("<product setid>", iDrugID);

            string tmpFilename = System.IO.Path.GetTempFileName();
            WebCommon.DownloadWebFile(drugURL, tmpFilename);

            //string drugPage = RetrieveDrugWebPage(drugURL);

            //Parsing the web page
            try
            {
                HtmlDocument proDoc = new HtmlDocument();
                
                    proDoc.Load(tmpFilename);

                    //now get the ingredients
                    HtmlNodeCollection nodes = proDoc.DocumentNode.SelectNodes("//table/@class");

                    //Locate Active ingredients
                    //need to split the contents of the table
                    //Find and Read active ingredients until reaching InActive ingredients
                    //Read InActive ingredients until reaching packaging

                if(proDoc.DocumentNode != null)
                {
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
                                            j = j + 6;
                                            string[] stopFlagsActive = { "Inactive Ingredients" };
                                            string activeIngredients = HttpUtility.HtmlDecode(GetInformation(drugInformation, ref j, stopFlagsActive)).Trim();
                                            //ioDrug.ActiveIngredients = activeIngredients;
                                            break;
                                        case "Inactive Ingredients":
                                            j += 6;
                                            string[] stopFlagsInactive = { "Packaging", "Product Characteristics" };
                                            string inactiveIngredients = HttpUtility.HtmlDecode(GetInformation(drugInformation, ref j, stopFlagsInactive)).Trim();
                                            //ioDrug.InActiveIngredients = inactiveIngredients;
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

        private string RetrieveDrugWebPage(string iURL)
        {
            HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(iURL);
            myRequest.Method = "GET";

            WebResponse myResponse = myRequest.GetResponse();
            StreamReader sr = new StreamReader(myResponse.GetResponseStream(), System.Text.Encoding.UTF8);

            string result = sr.ReadToEnd();

            sr.Close();
            myResponse.Close();

            return result;
        }

        private string GetInformation(string[] iDrugInformation, ref int ioStartIdx, string[] iStopFlags)
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

        //public List<BasicDrugInformation> RetrieveBasicDrugInformation(string iDrugName)
        //{
        //    List<BasicDrugInformation> drugInfo = null;// = new List<BasicDrugInformation>();

        //    string requestURI = WebServiceSPLListByDrugName.Replace("<drug_name>", iDrugName);
        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestURI);
        //    request.Method = "GET";
        //    request.ContentType = "application/json; charset=UTF-8";
        //    //request.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

        //    try
        //    {
        //        HttpWebResponse response = (HttpWebResponse)request.GetResponse();

        //        Stream dataStream = response.GetResponseStream();
        //        // Open the stream using a StreamReader for easy access.
        //        StreamReader reader = new StreamReader(dataStream);
        //        // Read the content.
        //        string responseFromServer = reader.ReadToEnd();

        //        //BasicDrugInformation drugInfo1 = JsonConvert.DeserializeObject<BasicDrugInformation>(responseFromServer);
        //        var obj = (JObject)JsonConvert.DeserializeObject(responseFromServer);

        //        var guids = obj["DATA"].Children()
        //                    .Cast<string>()
        //                    .ToList();

        //    }
        //    catch (Exception ex)
        //    {

        //    }

        //    return drugInfo;
        //}
    }
}
