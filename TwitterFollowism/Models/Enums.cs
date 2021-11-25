namespace TwitterFollowism.Models
{
    public class Enums
    {
        public enum AddUserCode
        {
            Success,
            AlreadyAdded,
            DoesNotExist,
            NotConfigured,
        }

        public enum RemoveUserCode
        {
            Success,
            WasNotConfigured
        }
    }
}
