using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ForexFactoryScraper
{
    public class ForexFactoryScraper
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public async Task<List<ForexEvent>> GetForexFactoryEvents(string url)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            // Add headers to mimic a browser request
            requestMessage.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            requestMessage.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");

            var response = await HttpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                return new List<ForexEvent>();
            }

            var html = await response.Content.ReadAsStringAsync();
            var events = ParseHtmlToForexEvents(html);
            return events;
        }

        private List<ForexEvent> ParseHtmlToForexEvents(string html)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var eventsTable = htmlDocument.DocumentNode.SelectSingleNode("//table[contains(@class, 'calendar__table')]");
            if (eventsTable == null)
            {
                Console.WriteLine("Unable to find the events table.");
                return new List<ForexEvent>();
            }

            var rows = eventsTable.SelectNodes(".//tr[contains(@class, 'calendar__row')]");
            if (rows == null)
            {
                Console.WriteLine("Unable to find event rows.");
                return new List<ForexEvent>();
            }

            var events = new List<ForexEvent>();
            string lastTime = null;

            foreach (var row in rows)
            {
                try
                {
                    var timeNode = row.SelectSingleNode(".//td[contains(@class, 'calendar__time')]");
                    var currencyNode = row.SelectSingleNode(".//td[contains(@class, 'calendar__currency')]");
                    var eventNode = row.SelectSingleNode(".//td[contains(@class, 'calendar__event')]");
                    var impactNode = row.SelectSingleNode(".//td[contains(@class, 'calendar__impact')]//span");

                    string eventDescription = eventNode?.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(eventDescription)) continue;

                    string time = timeNode?.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(time))
                    {
                        time = lastTime;
                    }
                    else
                    {
                        lastTime = time;
                    }

                    string impact = impactNode != null ? GetImpactDescription(impactNode.GetAttributeValue("class", "")) : "Unknown Impact";

                    var forexEvent = new ForexEvent
                    {
                        Time = time,
                        Currency = currencyNode?.InnerText.Trim(),
                        Event = eventDescription,
                        Impact = impact
                    };

                    events.Add(forexEvent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing row: {ex.Message}");
                }
            }

            return events;
        }

        private string GetImpactDescription(string impactClass)
        {
            return impactClass switch
            {
                string s when s.Contains("icon--ff-impact-red") => "High Impact Expected",
                string s when s.Contains("icon--ff-impact-ora") => "Medium Impact Expected",
                string s when s.Contains("icon--ff-impact-yel") => "Low Impact Expected",
                string s when s.Contains("icon--ff-impact-grey") => "Non-Economic",
                _ => "Unknown Impact"
            };
        }

        public async Task<List<ForexEvent>> FetchNews(string currency = null)
        {
            var url = "https://www.forexfactory.com/calendar?day=may31.2024";
            var events = await GetForexFactoryEvents(url);

            if (!string.IsNullOrWhiteSpace(currency))
            {
                events = events.Where(e => e.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return events;
        }
    }

    public class ForexEvent
    {
        public string Time { get; set; }
        public string Currency { get; set; }
        public string Event { get; set; }
        public string Impact { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var scraper = new ForexFactoryScraper();
            var events = await scraper.FetchNews("USD");

            foreach (var forexEvent in events)
            {
                Console.WriteLine($"Time: {forexEvent.Time}, Currency: {forexEvent.Currency}, Event: {forexEvent.Event}, Impact: {forexEvent.Impact}");
            }
        }
    }
}
