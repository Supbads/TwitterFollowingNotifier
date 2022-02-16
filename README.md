# TwitterFollowingNotifier
A .Net implementation of a Twitter to Discord Bot to sink updates any updates in twitter followings for configured users.
Updates appear every N minutes (due to Twitter API call limitations) and would compare previous snapshot followings to the new one determining if an account has followed/unfollowed or in a rare case an account has been remove which would automatically unfollow the account

