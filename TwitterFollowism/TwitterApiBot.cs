using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static TwitterFollowism.Models.Enums;

namespace TwitterFollowism
{
    public class TwitterApiBot
    {
        private readonly HttpClient _client;
        private readonly TwitterApiConfig _config;
        private readonly DiscordBot _discordBot;
        private readonly SavedRecords _savedRecords;

        private object lockObj = new object();

        private const string _userRequestUrl = @"https://api.twitter.com/2/users?ids={0}";
        private const string _userFriendsReqStr = @"https://api.twitter.com/1.1/friends/ids.json?screen_name={0}";
        private const string TwitterAccountLink = @"https://twitter.com/{0}";
        private const string GetUserByUsername = @"https://api.twitter.com/2/users/by/username/{0}";

        private const int DelayMs = 1000000; // 15 mins

        public TwitterApiBot(DiscordBot discordBot,
            TwitterApiConfig config,
            SavedRecords savedEntities)
        {
            this._client = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(1000)
            };

            this._client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.BearerToken}");

            this._config = config;
            this._discordBot = discordBot;
            this._savedRecords = savedEntities;
        }

        public string[] GetCurrentlyTrackedUsers()
        {
            return this._config.UsersToTrack.ToArray();
        }

        public async Task Run()
        {
            this._discordBot.ConfigureTwitterApi(this);
            if (this._config.UsersToTrack.Contains("skip"))
            {
                this._config.UsersToTrack.Clear();
            }

            await InitUsers();

            while (true)
            {
                var usersFriends = await Task.WhenAll(this._config.UsersToTrack.Select(x => GetUserFriends(x)).ToArray());

                // parallel foreach ?
                foreach (var userWithFriends in usersFriends)
                {
                    var user = userWithFriends.user;
                    var newUserFriends = userWithFriends.friends;

                    var oldUserFriends = this._savedRecords.UserAndFriends[user];

                    var newFriends = newUserFriends.Except(oldUserFriends).ToArray();
                    var removedFriends = oldUserFriends.Except(newUserFriends).ToArray();

                    var friendsChanges = newFriends.Union(removedFriends).ToArray();

                    if (!friendsChanges.Any())
                    {
                        continue;
                    }

                    await SendDiscordMessages(user, newFriends, removedFriends, friendsChanges);
                    this._savedRecords.UserAndFriends[user] = newUserFriends;
                    PersistSavedRecordsBlocking();
                }

                await Task.Delay(DelayMs);
            }
        }

        private async Task InitUsers()
        {
            // todo validate the configured users exist

            var usersToInit = this._config.UsersToTrack
                .Where(user => !_savedRecords.IsInitialSetup.ContainsKey(user) || _savedRecords.IsInitialSetup[user])
                .ToArray();

            if (usersToInit.Any())
                Console.WriteLine($"Reinitializing users: {string.Join(',', usersToInit)}");

            var initUsersTasks = await Task.WhenAll(usersToInit.Select(x => GetUserFriends(x)).ToArray());

            bool updatedUserFriends = false;
            foreach (var initUser in initUsersTasks)
            {
                var user = initUser.user;
                if (!this._savedRecords.IsInitialSetup.ContainsKey(user))
                {
                    this._savedRecords.IsInitialSetup.Add(user, false);
                }
                else
                {
                    this._savedRecords.IsInitialSetup[user] = false;
                }

                if (!this._savedRecords.UserAndFriends.ContainsKey(user))
                {
                    this._savedRecords.UserAndFriends.Add(user, new HashSet<long>());
                }

                this._savedRecords.UserAndFriends[user] = initUser.friends;
                updatedUserFriends = true;
            }

            if (updatedUserFriends)
            {
                PersistSavedRecordsBlocking();
            }
        }

        public AddUserCode ContinueTrackingUser(string user)
        {
            if (this._config.UsersToTrack.Contains(user))
            {
                return AddUserCode.AlreadyAdded;
            }

            if (!this._savedRecords.UserAndFriends.ContainsKey(user))
            {
                return AddUserCode.NotConfigured;
            }

            this._config.UsersToTrack.Add(user);
            return AddUserCode.Success;
        }

        public async Task<AddUserCode> AddUser(string user)
        {
            if (this._config.UsersToTrack.Contains(user) || this._savedRecords.UserAndFriends.ContainsKey(user))
            {
                return AddUserCode.AlreadyAdded;
            }

            var userExists = await UserExistsByUsername(user);
            if (!userExists)
            {
                return AddUserCode.DoesNotExist;
            }

            if (!this._savedRecords.UserAndFriends.ContainsKey(user))
            {
                this._savedRecords.UserAndFriends.Add(user, new HashSet<long>());
            }
            this._config.UsersToTrack.Add(user);

            if (!this._savedRecords.IsInitialSetup.ContainsKey(user))
            {
                this._savedRecords.IsInitialSetup.Add(user, true);
            }

            var friends = await GetUserFriends(user);
            this._savedRecords.UserAndFriends[user] = friends.friends;
            this._savedRecords.IsInitialSetup[user] = false;

            PersistSavedRecordsBlocking();
            return AddUserCode.Success;
        }

        public RemoveUserCode RemoveUser(string user)
        {
            var userExists = this._config.UsersToTrack.Contains(user) || this._savedRecords.UserAndFriends.ContainsKey(user);

            if (this._config.UsersToTrack.Contains(user))
            {
                this._config.UsersToTrack.Remove(user);
            }

            if (this._savedRecords.IsInitialSetup.ContainsKey(user))
            {
                this._savedRecords.IsInitialSetup.Remove(user);
            }

            if (this._savedRecords.UserAndFriends.ContainsKey(user))
            {
                this._savedRecords.UserAndFriends.Remove(user);
            }

            if (userExists)
            {
                return RemoveUserCode.Success;
            }

            return RemoveUserCode.WasNotConfigured;
        }

        public RemoveUserCode StopTrackingUser(string user)
        {
            if (!this._config.UsersToTrack.Contains(user))
            {
                return RemoveUserCode.WasNotConfigured;
            }

            this._config.UsersToTrack.Remove(user);
            return RemoveUserCode.Success;
        }

        private async Task SendDiscordMessages(string user, long[] newFriends, long[] removedFriends, long[] friendsChanges)
        {
            List<string> discordMessages = new List<string>(friendsChanges.Length);

            var usersMap = await GetUsersBasicDataByIds(friendsChanges);

            foreach (var newFriend in newFriends)
            {
                var newFriendDetails = usersMap[newFriend];
                discordMessages.Add($"@here {user} Followed: {newFriendDetails.Username} ({newFriendDetails.Name}) . {string.Format(TwitterAccountLink, newFriendDetails.Username)}");
            }

            foreach (var removedFriend in removedFriends)
            {
                var removedFriendDetails = usersMap[removedFriend];
                discordMessages.Add($"@here {user} UnFollowed: {removedFriendDetails.Username} ({removedFriendDetails.Name}) . {string.Format(TwitterAccountLink, removedFriendDetails.Username)}");
            }

            var sucessful = false;
            var attempts = 1;
            const int maxAttempts = 5;
            while (!sucessful && attempts <= maxAttempts)
            {
                try
                {
                    await this._discordBot.Notify(string.Join(Environment.NewLine, discordMessages));
                    sucessful = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception when notifying changes for {user}. {attempts}/{maxAttempts}");
                    Console.WriteLine(ex.Message);
                    attempts += 1;
                }
            }
        }

        private void PersistSavedRecordsBlocking()
        {
            lock (lockObj)
            {
                File.WriteAllText(this._config.SavedRecordsRoute, JsonConvert.SerializeObject(this._savedRecords));
            }
        }

        private async Task<(string user, HashSet<long> friends)> GetUserFriends(string user)
        {
            string respRaw = "";
            bool completed = false;
            while (!completed)
            {
                try
                {
                    respRaw = await this._client.GetStringAsync(string.Format(_userFriendsReqStr, user));
                    completed = true;
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("Too many"))
                    {
                        Console.WriteLine(ex.Message);
                    }

                    await Task.Delay(DelayMs / _config.UsersToTrack.Count);
                }
            }

            var response = JsonConvert.DeserializeObject<TwitterFriendsResponse>(respRaw);

            return (user, response.ids);
        }

        private async Task<bool> UserExistsByUsername(string username)
        {
            try
            {
                var respRaw = await this._client.GetStringAsync(string.Format(GetUserByUsername, username));
                var twitterUsersResp = JsonConvert.DeserializeObject<TwitterUserLookupByNameResp>(respRaw);
                return twitterUsersResp.Data != null;
            }
            catch
            {
                return false;
            }
        }

        private async Task<Dictionary<long, TwitterUser>> GetUsersBasicDataByIds(long[] userIds)
        {
            var respRaw = await this._client.GetStringAsync(string.Format(_userRequestUrl, string.Join(',', userIds)));
            var twitterUsersResp = JsonConvert.DeserializeObject<TwitterUsersLookupResp>(respRaw);
            return twitterUsersResp.Data.ToDictionary(x => x.Id, x => x);
        }
    }
}
