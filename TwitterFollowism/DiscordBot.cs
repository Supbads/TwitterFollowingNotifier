using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TwitterFollowism
{
    public class DiscordBot
    {
        private readonly DiscordConfigJson _configParsed;
        private DiscordSocketClient _client;
        private TwitterApiBot _twitterApiBot;

        const string RemoveUserCommand = ".remove user";
        const string AddUserCommand = ".add user";
        const string StopUserCommand = ".stop user";
        const string ContinueUserCommand = ".continue user";
            

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

        public void ConfigureTwitterApi(TwitterApiBot bot)
        {
            this._twitterApiBot = bot;
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
                await _client.SetActivityAsync(new Game("use .help to see a list of all the commands", type: ActivityType.Watching, flags: ActivityProperties.None, null));
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
                else if (message.Content.Equals(".help", StringComparison.InvariantCultureIgnoreCase))
                {
                    await context.Channel.SendMessageAsync(
                        $"{AddUserCommand} <username> - Adds a user to the track list" + "\n" +
                        $"{RemoveUserCommand} <username> - Removes a user from the track list, deletes their following snapshot stops tracking them" + "\n" +
                        $"{StopUserCommand} <username> - Stops tracking a user's followings for the current session but does not remove their following snapshot" + "\n" +
                        $"{ContinueUserCommand} <username> - Continues to track a configured user if they have been stopped using the stop user command" + "\n" +
                        ".list - Display the list of currently tracked accounts");
                }
                else if (message.Content.StartsWith(AddUserCommand))
                {
                    var user = message.Content.Substring(AddUserCommand.Length).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Last();
                    user = ReplaceExistingStartingAtSigns(user);

                    var status = await this._twitterApiBot.AddUser(user);
                    switch (status)
                    {
                        case Models.Enums.AddUserCode.Success:
                            await context.Channel.SendMessageAsync($"User {user} added");
                            break;
                        case Models.Enums.AddUserCode.AlreadyAdded:
                            await context.Channel.SendMessageAsync($"User {user} has already been added");
                            break;
                        case Models.Enums.AddUserCode.DoesNotExist:
                            await context.Channel.SendMessageAsync($"Unable to add user {user} as they do not exist. Check for typos");

                            break;
                        default:
                            break;
                    }
                }
                else if (message.Content.StartsWith(RemoveUserCommand))
                {
                    var user = message.Content.Substring(RemoveUserCommand.Length).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Last();
                    user = ReplaceExistingStartingAtSigns(user);

                    var status = this._twitterApiBot.RemoveUser(user);
                    switch (status)
                    {
                        case Models.Enums.RemoveUserCode.Success:
                            await context.Channel.SendMessageAsync($"User {user} removed");
                            break;
                        case Models.Enums.RemoveUserCode.WasNotConfigured:
                            await context.Channel.SendMessageAsync($"Unable to remove {user} as they are not configured. Check for typos");
                            break;
                        default:
                            break;
                    }
                }
                else if (message.Content.StartsWith(StopUserCommand))
                {
                    var user = message.Content.Substring(StopUserCommand.Length).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Last();
                    user = ReplaceExistingStartingAtSigns(user);
                    
                    var status = this._twitterApiBot.StopTrackingUser(user);
                    switch (status)
                    {
                        case Models.Enums.RemoveUserCode.Success:
                            await context.Channel.SendMessageAsync($"User {user} removed");
                            break;
                        case Models.Enums.RemoveUserCode.WasNotConfigured:
                            await context.Channel.SendMessageAsync($"Unable to stop tracking {user} as they are not configured for this run. Check for typos");
                            var trackedUsersStr = string.Join(", ", this._twitterApiBot.GetCurrentlyTrackedUsers());
                            await context.Channel.SendMessageAsync($"Current tracked users: {trackedUsersStr}");
                            break;
                        default:
                            break;
                    }
                }
                else if(message.Content.StartsWith(ContinueUserCommand))
                {
                    var user = message.Content.Substring(ContinueUserCommand.Length).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Last();
                    user = ReplaceExistingStartingAtSigns(user);

                    var status = this._twitterApiBot.ContinueTrackingUser(user);
                    switch (status)
                    {
                        case Models.Enums.AddUserCode.Success:
                            await context.Channel.SendMessageAsync($"Continue tracking {user}");
                            break;
                        case Models.Enums.AddUserCode.AlreadyAdded:
                            await context.Channel.SendMessageAsync($"User {user} is already being tracked");
                            break;
                        case Models.Enums.AddUserCode.NotConfigured:
                            await context.Channel.SendMessageAsync($"User {user} was never configured. Use .add if you wish to add them");
                            break;
                        default:
                            break;
                    }
                }
                else if (message.Content.StartsWith(".list"))
                {
                    var trackedUsersStr = string.Join(", ", this._twitterApiBot.GetCurrentlyTrackedUsers());
                    await context.Channel.SendMessageAsync($"Currently tracked users: {trackedUsersStr}");
                }

                return;
            }
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

        private async Task Connected()
        {
            Console.WriteLine($"Bot: ${_client.CurrentUser.Username}");
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

        private static string ReplaceExistingStartingAtSigns(string user)
        {
            user = Regex.Replace(user, "^@+", "");
            return user;
        }
    }
}
