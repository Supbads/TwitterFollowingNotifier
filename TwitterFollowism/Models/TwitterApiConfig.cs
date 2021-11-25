using System.Collections.Generic;

namespace TwitterFollowism
{
    public class TwitterApiConfig
    {
        public string SavedRecordsRoute { get; set; }
        public HashSet<string> UsersToTrack { get; set; }
        public string BearerToken { get; set; }
    }
}
