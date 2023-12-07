﻿using Shared.MVVM.Model;
using System;

namespace Server.MVVM.Model.Networking.UIRequests
{
    public class DisconnectClient : UIRequest
    {
        #region Properties
        public ClientPrimaryKey ClientKey { get; }
        public Action Callback { get; }
        #endregion

        public DisconnectClient(ClientPrimaryKey clientKey, Action callback)
        {
            ClientKey = clientKey;
            Callback = callback;
        }
    }
}