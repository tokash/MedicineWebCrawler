using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MedicineWebCrawler
{
    public class TextFilesCommon
    {
        public static List<String> ReadTextFileToList(string iPath)
        {
            List<String> list = new List<string>();

            //read whole file to buffer
            using (FileStream fs = File.Open(iPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BufferedStream bs = new BufferedStream(fs))
            using (StreamReader sr = new StreamReader(bs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    list.Add(line + "\n");
                }
            }

            return list;
        }

        public static string ReadTextFileToString(string iPath)
        {
            string line = string.Empty;

            //read whole file to buffer
            using (FileStream fs = File.Open(iPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs))
            {
                line = sr.ReadToEnd();
                
            }

            return line;
        }

        public static void WriteTextFile(List<String> iLines, string oOutputFilePath)
        {
            using (StreamWriter writer = new StreamWriter(oOutputFilePath, true))
            {
                foreach (string item in iLines)
                {
                    writer.WriteLine(item);
                }
            }
        }

        public static void WriteCSVFile(string oCSVFilepath, List<String> iLines, string iColumns)
        {
            using (StreamWriter csvRequiredCommitDetails = new StreamWriter(oCSVFilepath, true))
            {
                csvRequiredCommitDetails.WriteLine(iColumns);

                foreach (string line in iLines)
                {
                    csvRequiredCommitDetails.WriteLine(line);
                }
            }
        }
    }
}
