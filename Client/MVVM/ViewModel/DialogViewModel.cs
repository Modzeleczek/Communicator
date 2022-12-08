﻿using Client.MVVM.Model;
using Shared.MVVM.Core;
using System;

namespace Client.MVVM.ViewModel
{
    public class DialogViewModel : ViewModel
    {
        #region Commands
        public RelayCommand Close { get; protected set; }
        #endregion

        protected DialogViewModel()
        {
            Close = new RelayCommand(e => OnRequestClose(new Status(1)));
        }

        public Status Status { get; protected set; } = new Status(1);
        public event EventHandler RequestClose = null;

        protected virtual void OnRequestClose(Status status)
        {
            if (RequestClose != null) RequestClose(this, null);
            Status = status;
        }
    }
}
