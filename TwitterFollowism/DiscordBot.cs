using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TwitterFollowism
{
    public class DiscordBot
    {
        private readonly DiscordConfigJson _configParsed;
        private DiscordSocketClient _client;

        public DiscordBot(DiscordConfigJson configParsed)
        {
            this._configParsed = configParsed;
        }

        public async Task Init()
        {
            _client = new DiscordSocketClient();

            await Setup();

            _client.Disconnected += Disconnected;
        }

        private async Task Setup()
        {
            _client.MessageReceived += MessageReceivedAsync;
            _client.Log += _client_Log;

            _client.Connected += Connected;

            await _client.LoginAsync(TokenType.Bot, _configParsed.Token);
            await _client.StartAsync();

            _client.Ready += async () =>
            {

                await _client.SetActivityAsync(new Game("into the void", type: ActivityType.Watching, flags: ActivityProperties.None, null));
                await _client.SetStatusAsync(UserStatus.Online);
            };
        }

        private async Task MessageReceivedAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);
            if (!message.Author.IsBot)
            {
                if (message.Content.Contains("ping", StringComparison.OrdinalIgnoreCase))
                {
                    await context.Channel.SendMessageAsync("pong");
                    return;
                }

                return;
            }
        }

        private async Task Connected()
        {
            Console.WriteLine($"Bot: ${_client.CurrentUser.Username}");
        }

        private async Task Disconnected(Exception e)
        {
            Console.WriteLine($"dc'd: {DateTime.Now}");
            const int setupMs = 3000;
            while (true)
            {
                try
                {
                    Console.WriteLine("disposing");
                    this._client.Dispose();
                    Console.WriteLine("disposed");

                    this._client = new DiscordSocketClient();
                    Console.WriteLine("new cli created");

                    await Setup();
                    Console.WriteLine("setup client");

                    _client.Disconnected += Disconnected;
                    Console.WriteLine("setup dc recursively");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not reconnect on dc @ {DateTime.Now}. Try setting it up again in {setupMs} ms.");
                }

                await Task.Delay(setupMs);
            }
        }

        public async Task Shutdown()
        {
            await _client.SetStatusAsync(UserStatus.Offline);
            _client.Dispose();
        }

        private Task _client_Log(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        public async Task Notify(string message)
        {
            Console.WriteLine(message);

            var sendChannelsMessagesTasks = new List<Task<RestUserMessage>>();
            foreach (var guild in this._client.Guilds)
            {
                if (guild.Id == 897168415691780118)
                {
                    sendChannelsMessagesTasks.Add(guild.GetTextChannel(897641608386863124).SendMessageAsync($"<@&897180966597033984> Testing potential scrapes {message}"));
                }

                sendChannelsMessagesTasks.Add(guild.DefaultChannel.SendMessageAsync(message));
            }

            await Task.WhenAll(sendChannelsMessagesTasks);
        }
    }
}
