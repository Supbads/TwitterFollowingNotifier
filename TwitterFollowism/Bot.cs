using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitterFollowism
{
    public class Bot
    {
        private readonly DiscordConfigJson _configParsed;
        private readonly HashSet<string> _wordsCaseInsensitive;
        private DiscordSocketClient _client;

        public Bot(DiscordConfigJson configParsed, HashSet<string> words)
        {
            this._configParsed = configParsed;
            this._wordsCaseInsensitive = words;
        }

        public async Task Init()
        {
            _client = new DiscordSocketClient();
            await Setup(_client);

            _client.Disconnected += Disconnected;

            await Task.Delay(-1);
        }

        private async Task Setup(DiscordSocketClient client)
        {
            _client.MessageReceived += MessageReceivedAsync;
            _client.Log += _client_Log;

            await _client.LoginAsync(TokenType.Bot, _configParsed.Token);
            await _client.StartAsync();

            _client.Ready += async () =>
            {
                await _client.SetActivityAsync(new Game("Elon's musk", type: ActivityType.Watching, flags: ActivityProperties.None, null));
                await _client.SetStatusAsync(UserStatus.Online);
                //await _client.GetUser(128104044702072833).SendMessageAsync("we up");
            };
        }

        private async Task Disconnected(Exception e)
        {
            Console.WriteLine($"dc'd: {DateTime.Now}");
            const int setupMs = 3000;
            while(true)
            {
                try
                {
                    Console.WriteLine("disposing");
                    this._client.Dispose();
                    Console.WriteLine("disposed");

                    this._client = new DiscordSocketClient();
                    Console.WriteLine("new cli created");

                    await Setup(_client);
                    Console.WriteLine("setup client");
            
                    _client.Disconnected += Disconnected; // lmfao if this works
                    Console.WriteLine("setup dc recursively");
                    break;
                }
                catch(Exception ex)
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

        private async Task MessageReceivedAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);

            if (message.Author.Id == _client.CurrentUser.Id)
            {
                return;
            }

            #if DEBUG
            await DebugFlow(message, context);
            #else
            await OriginalFlow(message, context);
            #endif
        }

        private async Task DebugFlow(SocketUserMessage message, SocketCommandContext context)
        {
            await TestMatchMessageContent(message, context); // testing
            await TryMatchMessageContent(message, context);
        }

        private async Task OriginalFlow(SocketUserMessage message, SocketCommandContext context)
        {
            if (!message.Author.IsBot)
            {
                if (message.Content.Contains("ping", StringComparison.OrdinalIgnoreCase))
                {
                    await context.Channel.SendMessageAsync("pong");
                    return;
                }

                Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Not a bot tweet and not a ping");
                return;
            }

            if (!message.Author.Username.Contains("Eris", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Not eris message: skipping");
                return;
            }

            var elonTweet = message.Embeds.FirstOrDefault()?.Description?.Contains("New tweet by **[@elonmusk]", StringComparison.InvariantCultureIgnoreCase) ?? false;
            elonTweet |= message.Content.Contains("@elonmusk", StringComparison.InvariantCultureIgnoreCase);

            if (!elonTweet)
            {
                Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Not elon message: skipping");
                return;
            }

            await TryMatchMessageContent(message, context);
        }

        private async Task TryMatchMessageContent(SocketUserMessage message, SocketCommandContext context)
        {
            var msg = message?.Embeds?.FirstOrDefault()?.Fields.FirstOrDefault().Value;
            if (msg == null)
            {
                Console.WriteLine("Null not good");
                SerializeMessageContentAndEmbeds(message);
                return;
            }

            var end = GetEndIndex(msg, "Link to tweet");
            var elonMsgWords = msg.Substring(0,end).Split(new string[] { "\"", ".", ",", ":", "~", "!", " ", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            var matchingWords = new List<string>(elonMsgWords.Length / 4);
            foreach (var word in elonMsgWords)
            {
                if (_wordsCaseInsensitive.Contains(word))
                {
                    matchingWords.Add(word);
                }
            }

            if (matchingWords.Any())
            {
                await context.Channel.SendMessageAsync($"@everyone Matched Words: {string.Join(',', matchingWords)}");
            }
            else
            {
                Console.WriteLine("Nothing matched");
                Console.WriteLine($"words: {string.Join(' ', elonMsgWords)}");
                SerializeMessageContentAndEmbeds(message);
            }
        }

        private void SerializeMessageContentAndEmbeds(SocketUserMessage message)
        {
            Console.WriteLine($"Serialized Embeds: {JsonConvert.SerializeObject(message.Embeds, new JsonSerializerSettings { MaxDepth = 3 })}");
            Console.WriteLine($"Serialized msg: {JsonConvert.SerializeObject(message.Content, new JsonSerializerSettings { MaxDepth = 3 })}");
        }

        private async Task TestMatchMessageContent(SocketUserMessage message, SocketCommandContext context)
        {
            int index = GetStartIndex(message);
            int count = GetEndIndex(message, "Link to tweet") - index;

            var elonMsgWords = message.Content.Substring(index, count)
                .Split(new string[] { "\"", ".", ",", ":", "~", "!", " ", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var matchingWords = new List<string>(elonMsgWords.Length / 4);
            foreach (var word in elonMsgWords)
            {
                if (_wordsCaseInsensitive.Contains(word))
                {
                    matchingWords.Add(word);
                }
            }

            if (matchingWords.Any())
            {
                await context.Channel.SendMessageAsync($"@everyone Matched Words: {string.Join(',', matchingWords)}");
            }
            else
            {
                Console.WriteLine("Nothing matched");
            }
        }

        private int GetStartIndex(SocketUserMessage message)
        {
            var index = message.Content.IndexOf("@elonmusk");
            index = index != -1 ? index + "@elonmusk".Length : 0;

            return index;
        }

        private int GetEndIndex(SocketUserMessage message, string phrase)
        {
            var endIndex = message.Content.IndexOf(phrase);
            if (endIndex != -1)
            {
                return endIndex;
            }

            return message.Content.Length;
        }

        private int GetEndIndex(string message, string phrase)
        {
            var endIndex = message.IndexOf(phrase);
            if (endIndex != -1)
            {
                return endIndex;
            }

            return message.Length;
        }
    }
}
