using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abot.Crawler;
using Abot.Poco;
using System.Net;
using System.IO;


namespace MedicineWebCrawler
{
    class Crawler
    {
        string _drive = "d:\\";
        PoliteWebCrawler _crawler = null;
        Uri _uri;

        public Crawler(Uri uri)
        {
            _crawler = new PoliteWebCrawler();
            _crawler.PageCrawlStartingAsync += crawler_ProcessPageCrawlStarting;
            _crawler.PageCrawlCompletedAsync += crawler_ProcessPageCrawlCompleted;
            _crawler.PageCrawlDisallowedAsync += crawler_PageCrawlDisallowed;
            _crawler.PageLinksCrawlDisallowedAsync += crawler_PageLinksCrawlDisallowed;

            _uri = uri;
        }

        public void Crawl()
        {
            _crawler.Crawl(_uri);
        }

        void crawler_ProcessPageCrawlStarting(object sender, PageCrawlStartingArgs e)
        {
                PageToCrawl pageToCrawl = e.PageToCrawl;
                Console.WriteLine("About to crawl link {0} which was found on page {1}", pageToCrawl.Uri.AbsoluteUri, pageToCrawl.ParentUri.AbsoluteUri);
        }

        void crawler_ProcessPageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
        {
            CrawledPage crawledPage = e.CrawledPage;

            //get website from uri
            string uri = crawledPage.Uri.ToString();
            uri = Path.GetDirectoryName(uri);
            string[] pathParts = uri.Split('\\');

            string dir = pathParts[1]; //dir name will be at the 2nd place in the string array (URI = http:\\[site name\[directory]\file)
            
            //build directory path
            string path = _drive;
            for (int i = 1; i < pathParts.Length; i++)
			{
                path += pathParts[i] + "\\";
			}

            //filename should containg the word "drug" - otherwise it is not needed
            //should categorize alphabetically
            string filename = Path.GetFileName(crawledPage.Uri.ToString());

            if (Path.GetExtension(filename) != string.Empty)// && filename.Contains("drug"))
            {
                int idx = _uri.AbsoluteUri.IndexOf("alpha_");
                string s = (_uri.AbsoluteUri[idx + "alpha_".Length].ToString()).ToUpper();
                path += s + "\\";

                if (Directory.Exists(path))
                {
                    //create file in path
                    if (!File.Exists(Path.Combine(path, filename)))
                    {
                        try
                        {
                            File.WriteAllText(Path.Combine(path, filename), crawledPage.RawContent);
                        }
                        catch (Exception)
                        {

                        }
                    }
                }
                else
                {
                    //create path
                    DirectoryInfo dirInfo = Directory.CreateDirectory(path);


                    if (Directory.Exists(path))
                    {
                        //create file in path
                        try
                        {
                            File.WriteAllText(Path.Combine(path, filename), crawledPage.RawContent);
                        }
                        catch (Exception)
                        {
                        }
                    }
                } 
            }

            if (crawledPage.WebException != null || crawledPage.HttpWebResponse.StatusCode != HttpStatusCode.OK)
                    Console.WriteLine("Crawl of page failed {0}", crawledPage.Uri.AbsoluteUri);
            else
                    Console.WriteLine("Crawl of page succeeded {0}", crawledPage.Uri.AbsoluteUri);

            if (string.IsNullOrEmpty(crawledPage.RawContent))
                    Console.WriteLine("Page had no content {0}", crawledPage.Uri.AbsoluteUri);
        }

        void crawler_PageLinksCrawlDisallowed(object sender, PageLinksCrawlDisallowedArgs e)
        {
                CrawledPage crawledPage = e.CrawledPage;
                Console.WriteLine("Did not crawl the links on page {0} due to {1}", crawledPage.Uri.AbsoluteUri, e.DisallowedReason);
        }

        void crawler_PageCrawlDisallowed(object sender, PageCrawlDisallowedArgs e)
        {
                PageToCrawl pageToCrawl = e.PageToCrawl;
                Console.WriteLine("Did not crawl page {0} due to {1}", pageToCrawl.Uri.AbsoluteUri, e.DisallowedReason);
        }
    }
}
