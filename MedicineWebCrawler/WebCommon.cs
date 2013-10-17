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
        public static void DownloadWebFile(string i_URI, string o_LocalFilepath)
        {
            WebClient Client = new WebClient ();
            Client.DownloadFile(i_URI, o_LocalFilepath);
        }

        public static List<string> GetLinksFromHtml(string i_URI, string i_Pattern, string extension)
        {
            List<string> neededLinks = new List<string>();
            HtmlDocument doc = new HtmlDocument();
            string tempFilepath = System.IO.Path.GetTempPath() + @"temp.htm";// +Path.GetFileNameWithoutExtension(i_URI);

            if (!File.Exists(tempFilepath))
            {
                DownloadWebFile(i_URI, tempFilepath);
            }

            doc.Load(tempFilepath);

            HtmlNodeCollection links = doc.DocumentNode.SelectNodes("//a[@href]");
            foreach (HtmlNode node in links)
            {
                if (node.OuterHtml.Contains(i_Pattern))
                {
                    if (Path.GetExtension(node.Attributes["href"].Value) == extension)
                    {
                        neededLinks.Add(i_URI + node.Attributes["href"].Value);    
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
