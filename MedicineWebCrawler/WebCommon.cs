using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using System.IO;

namespace MedicineWebCrawler
{
    class WebCommon
    {
        public static void DownloadWebFile(string iURI, string oLocalFilepath)
        {
            WebClient Client = new WebClient ();
            Client.DownloadFile(iURI, oLocalFilepath);
        }

        public static List<string> GetLinksFromHtml(string iURI, string iPattern, string iIxtension)
        {
            List<string> neededLinks = new List<string>();
            HtmlDocument doc = new HtmlDocument();
            string tempFilepath = System.IO.Path.GetTempPath() + @"temp.htm";// +Path.GetFileNameWithoutExtension(i_URI);

            if (!File.Exists(tempFilepath))
            {
                DownloadWebFile(iURI, tempFilepath);
            }

            doc.Load(tempFilepath);

            HtmlNodeCollection links = doc.DocumentNode.SelectNodes("//a[@href]");
            foreach (HtmlNode node in links)
            {
                if (node.OuterHtml.Contains(iPattern))
                {
                    if (Path.GetExtension(node.Attributes["href"].Value) == iIxtension)
                    {
                        neededLinks.Add(iURI + node.Attributes["href"].Value);    
                    }
                    
                }
            }

            if (File.Exists(tempFilepath))
            {
                File.Delete(tempFilepath);
            }

            return neededLinks;
        }
    }
}
