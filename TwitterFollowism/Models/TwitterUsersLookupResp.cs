namespace TwitterFollowism
{
    //https://api.twitter.com/2/users?ids=635112756,18856867
    // endpoint response array of the following object
    public class TwitterUsersLookupResp
    {
        public TwitterUser[] Data { get; set; }
    }

    public class TwitterUser
    {
        public long Id { get; set; }

        //current nickname
        public string Name { get; set; }

        // @account name
        public string Username { get; set; }
    }

    public class TwitterUserLookupByNameResp
    {
        public TwitterUser Data { get; set; }
    }
}