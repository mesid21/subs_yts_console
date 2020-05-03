using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using HtmlAgilityPack;
using System.IO.Compression;

namespace subs_yts_console
{
    class Program
    {
        private static string tempfilepath = string.Empty;
        private static string fullmoviepath = string.Empty;
        private static readonly string omdbapikey = "replace with open movie db api key";

        static void Main(string[] args)
        {
            //var arg = @"M:\Toy Story 4 (2019) [BluRay] [720p] [YTS.LT]\Toy.Story.4.2019.720p.BluRay.x264-[YTS.LT].mp4";
            try
            {
                Task t = MainAsync(args[0]);//args[0]
                t.Wait();
            }
            catch (Exception)
            {
                Console.WriteLine("error:unable to get subtitle");
                Console.ReadLine();
            }
        }

        static async Task MainAsync(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return;

            var year = string.Empty;
            var filename = Path.GetFileName(argument);
            fullmoviepath = argument;

            //file-type filter
            var ext = Path.GetExtension(filename).ToLower();
            if (ext == ".mp4" || ext == ".avi" || ext == ".mkv")
            {
                Regex r = new Regex(@"\d{4}");
                Match match = r.Match(filename);
                if (match.Success)
                    year = match.Value;

                var name = filename.Substring(0, match.Index).Replace('.', ' ');

                Movie movie = new Movie()
                {
                    Name = name,
                    Year = year
                };

                Console.WriteLine($"movie name:{movie.Name}");
                Console.WriteLine($"year:{movie.Year}");

                if (string.IsNullOrEmpty(await GetSubtitle(movie)))
                {
                    Console.WriteLine("unable to get subtitle");
                }
                
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("not a supported media file");
                Console.ReadLine();
            }
        }

        public static async Task<string> GetSubtitle(Movie movie)
        {
            //get movie info from open-movie-db-api            
            var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync($"http://www.omdbapi.com/?&apikey={omdbapikey}={movie.Name}&y={movie.Year}&r=xml");
            string result = await response.Content.ReadAsStringAsync();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(result);
            string id = string.Empty;
            foreach (XmlNode node in doc.DocumentElement)
            {
                id = node.Attributes[17].Value;
            }            

            //get subtitle list page
            client = new HttpClient();
            response = await client.GetAsync($"https://yts-subs.com/movie-imdb/{id}");
            result = await response.Content.ReadAsStringAsync();

            //filter data and find English subtitle
            var sublistpage = new HtmlDocument();
            sublistpage.LoadHtml(result);
            HtmlNodeCollection nodes = sublistpage.DocumentNode.SelectNodes("//table[@class='table other-subs']/tbody/tr");
            string link = string.Empty;
            foreach (var node in nodes)
            {   
                var cell = node.SelectNodes("./td");
                if(cell[1].InnerText.Trim().Equals("English"))
                {
                    //get download page link
                    link = cell[4].SelectSingleNode("./a").Attributes["href"].Value;
                }    
            }

            if(string.IsNullOrWhiteSpace(link))
                return string.Empty;

            //get download page 
            client = new HttpClient();
            response = await client.GetAsync($"https://yts-subs.com{link}");
            result = await response.Content.ReadAsStringAsync();

            var downloadpage = new HtmlDocument();
            downloadpage.LoadHtml(result);
            var divs = downloadpage.DocumentNode.SelectNodes("//div[@class='col-xs-12']");
            var downloadlink = divs[1].SelectSingleNode("./a").Attributes["href"].Value;
            
            if (string.IsNullOrWhiteSpace(downloadlink))
                return string.Empty;

            Uri uri = new Uri(downloadlink);
            tempfilepath = Path.Combine(Path.GetTempPath(), Path.GetFileName(uri.LocalPath));            
                       

            using (var wc = new WebClient())
            {
                wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
                wc.DownloadFileAsync(uri, tempfilepath);
            }

            client.Dispose();           

            return downloadlink;
        }

        private static void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            Console.WriteLine("subtitle downloaded");
            (sender as WebClient).Dispose();

            //extract and move srt file to movie dir
            ZipFile.ExtractToDirectory(tempfilepath, Path.GetDirectoryName(fullmoviepath));
            Console.WriteLine("done");
            
        }
    }

    public class Movie
    {
        public string Name { get; set; }
        public string Year { get; set; }   
    }

}
