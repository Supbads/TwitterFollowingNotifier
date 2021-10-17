using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TwitterFollowism
{
    class Program
    {
        private static DiscordBot _bot;
        static string dir = string.Empty;
        static DiscordConfigJson discordConfig;
        static TwitterApiConfig twitterApiConfig;
        static SavedRecords savedRecords;

        static async Task Main()
        {
            SetupDirectoryConfigs();

            // todo handle default e.g. should read saved entities first
            // clean @ if it starts with it
            string[] usersToTrackArr = new string[0];
            while (!usersToTrackArr.Any())
            {
                Console.WriteLine("Enter users to track. Will track existing ones by default");
                var usersToTrack = Console.ReadLine();
                if (string.IsNullOrEmpty(usersToTrack))
                {
                    usersToTrackArr = savedRecords.UserAndFriends.Keys.ToArray();
                    if (usersToTrackArr.Length == 0)
                    {
                        Console.WriteLine("Cannot read from saved records as they are empty");
                    }
                }
                else
                {
                    usersToTrackArr = usersToTrack.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(userToTrack => userToTrack.Replace("@", ""))
                        .ToArray();

                    if (usersToTrackArr.Length == 0)
                    {
                        Console.WriteLine("Please input valid users separated by ',' or whitespace");
                    }
                }
            }

            twitterApiConfig.UsersToTrack = usersToTrackArr;

            await SetupDiscordBot(discordConfig);

            var twitterBot = new TwitterApiBot(_bot, twitterApiConfig, savedRecords);

            var infPoll = twitterBot.Run();
            infPoll.GetAwaiter().GetResult();

            Console.WriteLine("Main Thread shutting down");
        }

        private static Task SetupDiscordBot(DiscordConfigJson configParsed)
        {
            _bot = new DiscordBot(configParsed);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            return _bot.Init();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _bot.Shutdown().GetAwaiter().GetResult();
        }

        private static void SetupDirectoryConfigs()
        {
            Console.WriteLine("configure from current dir ? y/n (Default n)");
            var configSetup = Console.ReadLine();
            bool goBack = true;
            if (!string.IsNullOrEmpty(configSetup) && configSetup.Contains("y", StringComparison.InvariantCulture))
            {
                goBack = false;
            }

            dir = goBack ? @"..\..\..\..\" : "";
            var discordConfigRoute = $"{dir}discordConfig.json";

            var discordCfg = File.ReadAllText(discordConfigRoute);
            discordConfig = JsonConvert.DeserializeObject<DiscordConfigJson>(discordCfg);

            var savedRecordsRoute = $"{dir}savedRecords.json";
            var savedRecordsStr = File.ReadAllText(savedRecordsRoute);
            savedRecords = JsonConvert.DeserializeObject<SavedRecords>(savedRecordsStr) ?? new SavedRecords();

            var twitterApiConfigRoute = $"{dir}twitterApiConfig.json";
            var twitterApiCfg = File.ReadAllText(twitterApiConfigRoute);
            twitterApiConfig = JsonConvert.DeserializeObject<TwitterApiConfig>(twitterApiCfg);
            twitterApiConfig.SavedRecordsRoute = savedRecordsRoute;

        }
    }
}
