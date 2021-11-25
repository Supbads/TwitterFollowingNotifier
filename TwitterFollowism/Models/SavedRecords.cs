using System.Collections.Generic;

namespace TwitterFollowism
{
    public class SavedRecords
    {
        public SavedRecords()
        {
            this.UserAndFriends = new Dictionary<string, HashSet<long>>();
            this.IsInitialSetup = new Dictionary<string, bool>();
        }

        //a map for screen_name to the user's friends
        // todo try hashset instead of long[]
        public Dictionary<string, HashSet<long>> UserAndFriends { get; set; }

        // a bool map to indicate if it's the initial setup for a user.
        public Dictionary<string, bool> IsInitialSetup { get; set; }
    }
}
