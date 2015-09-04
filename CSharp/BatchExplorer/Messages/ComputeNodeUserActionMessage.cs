//Copyright (c) Microsoft Corporation

using System;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum ComputeNodeUserAction
    {
        AddUser,
        DeleteUser,
        EditUser
    }

    public class ComputeNodeUserActionMessage
    {
        public ComputeNodeUserAction Action { get; private set; }

        public string UserName { get; private set; }

        public string Password { get; private set; }

        public bool IsAdmin { get; private set; }

        public DateTime? ExpiryDate { get; private set; }

        public ComputeNodeUserActionMessage(ComputeNodeUserAction action, string userName, string password = null, bool? isAdmin = null, DateTime? expiryDate = null)
        {
            this.Action = action;
            this.UserName = userName;
            this.Password = password;
            this.IsAdmin = isAdmin.HasValue && isAdmin.Value;
            this.ExpiryDate = expiryDate;
        }
    }
}
