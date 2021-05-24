using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace Crawler
{
    public class MyDictionaryComparer : IComparer<KeyValuePair<string, int>>
    {
        public int Compare(KeyValuePair<string, int> lhs, KeyValuePair<string, int> rhs)
        {
            if (lhs.Value == rhs.Value)
            {
                return lhs.Key.CompareTo(rhs.Key);
            }
            else
            {
                return rhs.Value.CompareTo(lhs.Value);
            }
        }
    }

    class Program
    {
        private int NumberOfWords = 10;
        private readonly string Url;
        private readonly string Section;
        private readonly HashSet<string> wordsToIgnore;

        Program(string Url = "https://en.wikipedia.org/wiki/Microsoft", string Section = "History")
        {
            this.Url = Url;
            this.Section = Section;
            wordsToIgnore = new HashSet<string>();
        }
        private void PrintUsage()
        {
            Console.WriteLine("Crawler.exe <Number of Top Words> <Words to ignore separated by space>...");
        }

        private bool IgnoreNode(XmlNode node)
        {
            if (node.Name == "sup" || node.Name == "a" || node.Name == "span" || node.Name == "i")
            {
                return true;
            }

            return false;
        }

        bool ParseCmdLine(string[] args)
        {
            if (args != null && args.Length != 0)
            {
                if (!int.TryParse(args[0], out NumberOfWords))
                {
                    PrintUsage();
                    return false;
                }

                for (int i = 1; i < args.Length; i++)
                {
                    wordsToIgnore.Add(args[i]);
                }
            }

            return true;
        }

        private string DownloadPage()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                    using (Stream data = client.OpenRead(Url))
                    {
                        using (StreamReader reader = new StreamReader(data))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to download Url: {0}", e.ToString());
                throw;
            }
        }

        void CrawlPage()
        {
            try
            {
                string htmlStr = DownloadPage();
                bool isSection = false;
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(htmlStr);
                Dictionary<string, int> topWords = new Dictionary<string, int>();
                foreach (XmlNode node in xmlDoc.SelectNodes("//*"))
                {
                    // Assumption section is defined by h2 Heading
                    if (node.Name == "h2")
                    {
                        if (node.InnerText.Equals(Section))
                        {
                            isSection = true;
                        }
                        else if (isSection)
                        {
                            break;
                        }
                    }

                    // Found right section. Start reading words
                    if (isSection)
                    {
                        if (IgnoreNode(node))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(node.InnerText))
                        {
                            continue;
                        }

                        bool skipNode = false;
                        foreach (XmlNode childNode in node.ChildNodes)
                        {
                            if (IgnoreNode(childNode))
                            {
                                continue;
                            }

                            if (childNode.NodeType != XmlNodeType.Text && !string.IsNullOrEmpty(childNode.InnerText) && childNode.InnerText == node.InnerText)
                            {
                                skipNode = true;
                            }
                        }

                        if (skipNode)
                        {
                            continue;
                        }

                        InsertWords(topWords, node.InnerText);
                    }
                }

                PrintTopWords(topWords);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to crawl page: {0}", e.ToString());
                throw;
            }
        }

        private void InsertWords(Dictionary<string, int> topWords, string line)
        {
            string decodedStr = HttpUtility.HtmlDecode(new string(line.Where(c => !char.IsPunctuation(c)).ToArray()).Trim());
            if (!string.IsNullOrEmpty(decodedStr))
            {
                string[] split = decodedStr.Split(' ');

                for (int i = 0; i < split.Length; i++)
                {
                    if (!wordsToIgnore.Contains(split[i]))
                    {
                        if (topWords.ContainsKey(split[i]))
                        {
                            topWords[split[i]]++;
                        }
                        else
                        {
                            topWords.Add(split[i], 1);
                        }
                    }
                }
            }
        }

        private void PrintTopWords(Dictionary<string, int> topWords)
        {
            var sortedData = topWords.OrderBy(item => item, new MyDictionaryComparer());
            int cnt = 0;
            foreach (KeyValuePair<string, int> pair in sortedData)
            {
                Console.WriteLine("{0} {1}", pair.Key, pair.Value);
                cnt++;

                if (cnt == NumberOfWords)
                {
                    break;
                }
            }
        }

        static void Main(string[] args)
        {
            Program crawlObj = new Program();

            if (!crawlObj.ParseCmdLine(args))
            {
                return;
            }

            Console.WriteLine("V1 Implementation using XML Parser:");
            crawlObj.CrawlPage();
            Console.WriteLine("\n\n\nV2 Implementation Using HtmlAgilityPack:");
            crawlObj.CrawlPageV2();
        }

        public void CrawlPageV2()
        {
            try
            {
                string htmlStr = DownloadPage();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlStr);
                bool isSection = false;
                Dictionary<string, int> topWords = new Dictionary<string, int>();

                var newDoc = new HtmlDocument();
                HtmlNodeCollection nodeCollection = new HtmlNodeCollection(newDoc.DocumentNode);

                foreach (HtmlNode node in doc.DocumentNode.Descendants())
                {
                    // Assumption section is defined by h2 Heading
                    if (node.Name == "h2")
                    {
                        if (node.InnerText.Equals(Section))
                        {
                            isSection = true;
                        }
                        else if (isSection)
                        {
                            break;
                        }
                    }

                    // Found right section. Start storing nodes
                    if (isSection)
                    {
                        nodeCollection.Add(node);
                    }
                }

                newDoc.DocumentNode.AppendChildren(nodeCollection);
                // Enumerate Text Nodes
                foreach (HtmlNode node in newDoc.DocumentNode.SelectNodes("//text()"))
                {
                    InsertWords(topWords, node.InnerText);
                }

                PrintTopWords(topWords);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to crawl page: {0}", e.ToString());
                throw;
            }
        }
    }
}
