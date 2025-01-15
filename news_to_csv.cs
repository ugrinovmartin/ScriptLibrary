using HtmlAgilityPack;
using System.Globalization;

namespace News_Scraper
{
    public class ForexFactoryScraper
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public async Task<List<ForexEvent>> GetForexFactoryEvents(string url)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            requestMessage.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            requestMessage.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");

            var response = await HttpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                return [];
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
                return new List<ForexEvent>();
            }

            var rows = eventsTable.SelectNodes(".//tr[contains(@class, 'calendar__row')]");
            if (rows == null)
            {
                return [];
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

                    string impact = impactNode != null ? impactNode.GetAttributeValue("class", "") : "";

                    var forexEvent = new ForexEvent
                    {
                        Time = time,
                        Currency = currencyNode?.InnerText.Trim(),
                        Event = eventDescription,
                        Impact = impact
                    };

                    events.Add(forexEvent);
                }
                catch
                {
                    continue;
                }
            }

            return events;
        }

        private string GetImpactValue(string impactClass)
        {
            return impactClass switch
            {
                string s when s.Contains("icon--ff-impact-red") => "3",
                string s when s.Contains("icon--ff-impact-ora") => "2",
                string s when s.Contains("icon--ff-impact-yel") => "1",
                _ => "0"
            };
        }

        public async Task<List<ForexEvent>> FetchNews(DateTime startDate, DateTime endDate, string currency = null)
        {
            var allEvents = new List<ForexEvent>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                try
                {
                    var url = $"https://www.forexfactory.com/calendar?day={date:MMM dd.yyyy}".ToLower();
                    var events = await GetForexFactoryEvents(url);

                    if (!string.IsNullOrWhiteSpace(currency))
                    {
                        events = events.Where(e => e.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase)).ToList();
                    }

                    foreach (var evt in events)
                    {
                        evt.Date = date;
                    }

                    allEvents.AddRange(events);
                    await Task.Delay(500);
                    Console.WriteLine($"Fetched events for {date:yyyy-MM-dd}");
                }
                catch
                {
                    continue;
                }
            }

            return allEvents;
        }

        private static int ConvertTimeToMinutes(string time, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(time) || time.Contains("All Day")) return 0;

            time = time.Trim().ToUpper();

            // Remove any spaces between numbers and AM/PM
            time = time.Replace(" PM", "PM").Replace(" AM", "AM");

            string[] formats = {
                "h:mmtt",  // for "4:59PM" 
                "h:mm tt", // for "4:59 PM" 
                "H:mm",    // 24-hour 
                "HH:mm"    // 24-hour zero
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(time, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    var fullDateTime = date.Date.Add(parsedTime.TimeOfDay);

                    // Convert from UTC+2 to UTC
                    DateTime utcTime = fullDateTime.AddHours(-2);

                    // Convert UTC to Eastern Time
                    TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    DateTime easternTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, easternZone);

                    return easternTime.Hour * 60 + easternTime.Minute;
                }
            }

            return 0;
        }

        public async Task ExportToCSV(DateTime startDate, DateTime endDate, string currency, string outputPath)
        {
            var events = await FetchNews(startDate, endDate, currency);

            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("date,time,event,impact");

            foreach (var evt in events)
            {
                int dateInt = int.Parse(evt.Date.ToString("yyyyMMdd"));
                int timeMinutes = ConvertTimeToMinutes(evt.Time, evt.Date);
                int isBankHoliday = evt.Event.Contains("Bank Holiday", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                string impact = isBankHoliday == 1 ? "0" : GetImpactValue(evt.Impact);

                writer.WriteLine($"{dateInt},{timeMinutes},{isBankHoliday},{impact}");
            }
        }
    }

    public class ForexEvent
    {
        public DateTime Date { get; set; }
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

            var startDate = new DateTime(2024, 12, 24);
            var endDate = new DateTime(2024, 12, 31);

            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string outputPath = Path.Combine(currentDirectory, "forex_events.csv");

            await scraper.ExportToCSV(startDate, endDate, "USD", outputPath);
        }
    }
}