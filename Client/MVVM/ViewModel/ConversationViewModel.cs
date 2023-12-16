using Client.MVVM.Model.Networking;
using Client.MVVM.ViewModel.Observables;
using Shared.MVVM.Core;
using Shared.MVVM.View.Windows;
using Shared.MVVM.ViewModel;
using Shared.MVVM.ViewModel.Results;
using System;
using System.Collections.Generic;

namespace Client.MVVM.ViewModel
{
    public class ConversationViewModel : UserControlViewModel
    {
        #region Commands
        public RelayCommand Send { get; }
        public RelayCommand AddParticipant { get; }
        #endregion

        #region Properties
        private Conversation? _conversation;
        public Conversation? Conversation
        {
            get => _conversation;
            set { _conversation = value; OnPropertyChanged(); }
        }

        private string _writtenMessage = string.Empty;
        public string WrittenMessage
        {
            get => _writtenMessage;
            set { _writtenMessage = value; OnPropertyChanged(); }
        }
        #endregion

        public ConversationViewModel(DialogWindow owner, ClientMonolith client)
            : base(owner)
        {
            AddParticipant = new RelayCommand(_ =>
            {
                var result = AddParticipantViewModel.ShowDialog(window!, client);
                if (!(result is Success))
                    // Anulowanie
                    return;
            });

            Send = new RelayCommand(_ =>
            {
                /* Konwersacja nie może być null, bo wtedy
                ConversationView jest ukryte w MainWindow. */
                if (string.IsNullOrEmpty(WrittenMessage))
                    return;
                DateTime now = DateTime.Now;
                var message = new Message
                {
                    PlainContent = WrittenMessage,
                    SendTime = now,
                    ReceiveTime = now.AddMilliseconds(100),
                    DisplayTime = now.AddSeconds(10),
                    IsDeleted = false,
                    Attachments = new List<Attachment>()
                };
                _conversation!.Messages.Add(message);
                WrittenMessage = string.Empty;
                /* TODO: pisanie do wybranej konwersacji
                na wybranym koncie na wybranym serwerze */
            });
        }
    }
}
