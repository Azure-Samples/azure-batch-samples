using System;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum TVMUserAction
    {
        AddUser,
        DeleteUser,
        EditUser
    }

    public class TvmUserActionMessage
    {
        public TVMUserAction Action { get; private set; }

        public string Username { get; private set; }

        public string Password { get; private set; }

        public bool IsAdmin { get; private set; }

        public DateTime? ExpiryDate { get; private set; }

        public TvmUserActionMessage(TVMUserAction action, string username, string password = null, bool? isAdmin = null, DateTime? expiryDate = null)
        {
            this.Action = action;
            this.Username = username;
            this.Password = password;
            this.IsAdmin = isAdmin.HasValue && isAdmin.Value;
            this.ExpiryDate = expiryDate;
        }


    }
}
