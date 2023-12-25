using Client.MVVM.Model;
using Client.MVVM.Model.Networking;
using Client.MVVM.Model.Networking.UIRequests;
using Client.MVVM.View.Windows;
using Client.MVVM.ViewModel.AccountActions;
using Client.MVVM.ViewModel.Conversations;
using Client.MVVM.ViewModel.LocalUsers;
using Client.MVVM.ViewModel.Observables;
using Client.MVVM.ViewModel.ServerActions;
using Shared.MVVM.Core;
using Shared.MVVM.Model.Cryptography;
using Shared.MVVM.Model.Networking.Packets.ServerToClient.Conversation;
using Shared.MVVM.Model.Networking.Packets.ServerToClient.Participation;
using Shared.MVVM.View.Localization;
using Shared.MVVM.View.Windows;
using Shared.MVVM.ViewModel;
using Shared.MVVM.ViewModel.LongBlockingOperation;
using Shared.MVVM.ViewModel.Results;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows;

namespace Client.MVVM.ViewModel
{
    public class MainViewModel : WindowViewModel
    {
        #region Commands
        public RelayCommand OpenSettings { get; }
        public RelayCommand AddServer { get; }
        public RelayCommand EditServer { get; }
        public RelayCommand DeleteServer { get; }
        public RelayCommand AddAccount { get; }
        public RelayCommand EditAccount { get; }
        public RelayCommand DeleteAccount { get; }
        public RelayCommand AddConversation { get; }
        public RelayCommand OpenConversationDetails { get; }
        public RelayCommand Disconnect { get; }
        #endregion

        #region Properties
        private ObservableCollection<Observables.Server> _servers =
            new ObservableCollection<Observables.Server>();
        // Potencjalnie można usunąć _servers i zostawić samą właściwość Servers.
        public ObservableCollection<Observables.Server> Servers
        {
            get => _servers;
            set { _servers = value; OnPropertyChanged(); }
        }

        private Observables.Server? _selectedServer = null;
        public Observables.Server? SelectedServer
        {
            get => _selectedServer;
            set
            {
                window!.SetEnabled(false);

                if (!(SelectedServer is null) && !(SelectedAccount is null))
                    _client.Request(new Disconnect(SelectedServer.GetPrimaryKey(), () => UIInvoke(() =>
                        // Wątek UI na zlecenie (poprzez UIInvoke) wątku Client.Process
                        /* Przed wywołaniem niniejszego callbacka w OnServerEndedConnection
                        czyścimy konwersacje i zaznaczone konto. */
                        FinishSettingSelectedServer(value))));
                else
                    // Na pewno SelectedAccount is null.
                    FinishSettingSelectedServer(value);
            }
        }

        private void FinishSettingSelectedServer(Observables.Server? value)
        {
            _selectedServer = value;
            OnPropertyChanged(nameof(SelectedServer));

            // Czyścimy i odświeżamy listę kont.
            Accounts.Clear();
            if (!(value is null))
            {
                try
                {
                    var accounts = _storage.GetAllAccounts(_loggedUserKey,
                        value.GetPrimaryKey());
                    foreach (var acc in accounts)
                        Accounts.Add(acc);
                }
                catch (Error e)
                {
                    e.Prepend("|Could not| |read user's account list|.");
                    Alert(e.Message);
                    throw;
                }
            }

            window!.SetEnabled(true);
        }

        private ObservableCollection<Account> _accounts =
            new ObservableCollection<Account>();
        public ObservableCollection<Account> Accounts
        {
            get => _accounts;
            set { _accounts = value; OnPropertyChanged(); }
        }

        private Account? _selectedAccount = null;
        public Account? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                /* Na pewno !(SelectedServer is null), w przeciwnym przypadku użytkownik nie
                widzi nic na liście kont. */

                // Wyłączamy wszelkie interakcje z oknem.
                window!.SetEnabled(false);

                if (!(SelectedAccount is null))
                {
                    // Rozłączamy aktualne konto, bo jesteśmy połączeni.
                    if (!(value is null))
                        // Chcemy się połączyć.
                        DisconnectAndConnectWithAccount(value);
                    else
                        /* Nie chcemy się połączyć. Obsługujemy rozłączenie w OnServerEndedConnection,
                        a następnie w callbacku UIRequesta przywracamy interakcje. */
                        _client.Request(new Disconnect(SelectedServer!.GetPrimaryKey(),
                            () => UIInvoke(() => window.SetEnabled(true))));
                }
                else
                {
                    // Nie jesteśmy połączeni.
                    if (!(value is null))
                        // Chcemy się połączyć.
                        DisconnectAndConnectWithAccount(value);

                    // else (nieprawdopodobne): nie chcemy się połączyć.
                    window.SetEnabled(true);
                }
            }
        }

        private void DisconnectAndConnectWithAccount(Account value)
        {
            // Wątek UI
            _client.Request(new Disconnect(SelectedServer!.GetPrimaryKey(), () =>
            {
                // Wątek Client.Process
                _client.Request(new Connect(SelectedServer!.GetPrimaryKey(),
                    value.Login, value.PrivateKey, errorMsg =>
                    {
                        // Wątek Client.Process
                        // Można też dać UIInvoke na całą lambdę.
                        Account? newValue = value;
                        if (!(errorMsg is null))
                            // Nie połączyliśmy się.
                            newValue = null;

                        _selectedAccount = newValue;
                        UIInvoke(() =>
                        {
                            // Wątek UI
                            OnPropertyChanged(nameof(SelectedAccount));

                            /* Przed callbackiem UIRequesta Disconnect zostanie
                            wykonane OnServerEndedConnection. */
                            if (!(errorMsg is null))
                                // Nie połączyliśmy się.
                                Alert(errorMsg);

                            // Przywracamy interakcje z oknem.
                            window!.SetEnabled(true);
                        });
                    }));
            }));
        }

        private ObservableCollection<Conversation> _conversations =
            new ObservableCollection<Conversation>();
        public ObservableCollection<Conversation> Conversations
        {
            get => _conversations;
            set { _conversations = value; OnPropertyChanged(); }
        }

        private ConversationViewModel _conversationVM = null!;
        public ConversationViewModel ConversationVM
        {
            get => _conversationVM;
            private set { _conversationVM = value; OnPropertyChanged(); }
        }
        #endregion

        #region Fields
        private LocalUserPrimaryKey _loggedUserKey;
        private readonly ClientMonolith _client;
        private readonly Storage _storage;
        #endregion

        public MainViewModel()
        {
            // Do potomnych okien przekazujemy na zasadzie dependency injection.
            try { _storage = new Storage(); }
            catch (Error e)
            {
                Alert(e.Message);
                throw;
            }

            _client = new ClientMonolith();

            WindowLoaded = new RelayCommand(windowLoadedE =>
            {
                window = (DialogWindow)windowLoadedE!;
                // window.SetEnabled(false)
                // zapobiega ALT + F4 w głównym oknie
                window.Closable = false;

                ConversationVM = new ConversationViewModel(window, _client);

                try { d.ActiveLanguage = (Translator.Language)_storage.GetActiveLanguage(); }
                catch (Error e)
                {
                    Alert(e.Message);
                    // przekazanie dalej wyjątku spowoduje zamknięcie programu
                    throw;
                }

                try { ((App)Application.Current).ActiveTheme = (App.Theme)_storage.GetActiveTheme(); }
                catch (Error e)
                {
                    Alert(e.Message);
                    throw;
                }

                var loggedLocalUserKey = _storage.GetLoggedLocalUserKey();
                if (!(loggedLocalUserKey is null)) // jakiś użytkownik jest już zalogowany
                {
                    if (_storage.LocalUserExists(loggedLocalUserKey.Value))
                    {
                        // zalogowany użytkownik istnieje w BSONie
                        _loggedUserKey = loggedLocalUserKey.Value;
                        ResetLists();
                        return;
                    }
                    Alert("|User set as logged does not exist.|");
                }
                else
                    Alert("|No user is logged.|");
                ShowLocalUsersDialog();
            });

            AddServer = new RelayCommand(_ =>
            {
                var vm = new CreateServerViewModel(_storage, _loggedUserKey)
                {
                    Title = "|Add_server|",
                    ConfirmButtonText = "|Add|"
                };
                new FormWindow(window!, vm).ShowDialog();
                var result = vm.Result;
                if (result is Success success)
                    Servers.Add((Observables.Server)success.Data!);
            });
            EditServer = new RelayCommand(obj =>
            {
                var server = (Observables.Server)obj!;
                /* Asynchronicznie rozłączamy i odznaczamy aktualnie wybrany
                serwer. Na UI zostanie wyświetlony dialog (okno) edycji serwera,
                a w oknie pod nim użytkownik nie może w tym czasie nic kliknąć,
                dzięki czemu możemy w tle asynchronicznie rozłączyć. */
                if (SelectedServer == server)
                    SelectedServer = null;

                var vm = new EditServerViewModel(_storage,
                    _loggedUserKey, server.GetPrimaryKey())
                {
                    Title = "|Edit server|",
                    ConfirmButtonText = "|Save|"
                };
                new FormWindow(window!, vm).ShowDialog();
                if (vm.Result is Success success)
                {
                    var updatedServer = (Observables.Server)success.Data!;
                    updatedServer.CopyTo(server);
                }
            });
            DeleteServer = new RelayCommand(obj =>
            {
                var server = (Observables.Server)obj!;

                var serverKey = server.GetPrimaryKey();
                var confirmRes = ConfirmationViewModel.ShowDialog(window!,
                    "|Do you want to delete| |server|" +
                    $" {serverKey.ToString("{0}:{1}")}?",
                    "|Delete server|", "|No|", "|Yes|");
                if (!(confirmRes is Success)) return;

                if (SelectedServer == server)
                    SelectedServer = null;

                try { _storage.DeleteServer(_loggedUserKey, serverKey); }
                catch (Error e)
                {
                    e.Prepend("|Error occured while| |deleting| " +
                        "|server;D| |from user's database.|");
                    Alert(e.Message);
                    throw;
                }
                Servers.Remove(server);
            });

            AddAccount = new RelayCommand(_ =>
            {
                var vm = new CreateAccountViewModel(_storage, _loggedUserKey,
                    SelectedServer!.GetPrimaryKey())
                {
                    Title = "|Add_account|",
                    ConfirmButtonText = "|Add|"
                };
                new FormWindow(window!, vm).ShowDialog();
                if (vm.Result is Success success)
                    Accounts.Add((Account)success.Data!);
            });
            EditAccount = new RelayCommand(obj =>
            {
                var account = (Account)obj!;
                if (SelectedAccount == account)
                    SelectedAccount = null;

                var vm = new EditAccountViewModel(_storage, _loggedUserKey,
                    SelectedServer!.GetPrimaryKey(), account.Login)
                {
                    Title = "|Edit account|",
                    ConfirmButtonText = "|Save|"
                };
                new FormWindow(window!, vm).ShowDialog();
                if (vm.Result is Success success)
                {
                    var updatedAccount = (Account)success.Data!;
                    updatedAccount.CopyTo(account);
                }
            });
            DeleteAccount = new RelayCommand(obj =>
            {
                var account = (Account)obj!;
                var confirmRes = ConfirmationViewModel.ShowDialog(window!,
                    "|Do you want to delete| |account|" + $" {account.Login}?",
                    "|Delete account|", "|No|", "|Yes|");
                if (!(confirmRes is Success)) return;

                if (SelectedAccount == account)
                    SelectedAccount = null;

                try
                {
                    _storage.DeleteAccount(_loggedUserKey,
                        SelectedServer!.GetPrimaryKey(), account.Login);
                }
                catch (Error e)
                {
                    e.Prepend("|Error occured while| |deleting| " +
                        "|account;D| |from user's database.|");
                    Alert(e.Message);
                    throw;
                }
                Accounts.Remove(account);
            });

            Close = new RelayCommand(_ =>
            {
                var stopProcessRequest = new StopProcess(() => UIInvoke(() =>
                {
                    // Wątek Client.Process
                    window!.Closable = true;
                    // zamknięcie MainWindow powoduje zakończenie programu
                    window.Close();
                }));

                if (!(SelectedAccount is null))
                    _client.Request(new Disconnect(SelectedServer!.GetPrimaryKey(),
                        // Wątek Client.Process
                        () => _client.Request(stopProcessRequest)));
                else
                    _client.Request(stopProcessRequest);
            });

            OpenSettings = new RelayCommand(_ =>
            {
                var vm = new SettingsViewModel(_storage);
                var win = new SettingsWindow(window!, vm);
                vm.RequestClose += () => win.Close();
                win.ShowDialog();
                if (vm.Result is Success settingsSuc &&
                    (SettingsViewModel.Operations)settingsSuc.Data! ==
                    SettingsViewModel.Operations.LocalLogout) // wylogowanie
                {
                    SelectedServer = null;
                    Servers.Clear();
                    var loginRes = LocalLoginViewModel.ShowDialog(window!, _storage, _loggedUserKey, true);
                    if (!(loginRes is Success loginSuc))
                    {
                        ResetLists();
                        return;
                    }
                    var currentPassword = (SecureString)loginSuc.Data!;

                    _storage.SetLoggedLocalUser(false);

                    var encryptRes = ProgressBarViewModel.ShowDialog(window!,
                        "|Encrypting user's database.|", true,
                        (reporter) => _storage.EncryptLocalUser(ref reporter, _loggedUserKey,
                        currentPassword));
                    currentPassword.Dispose();
                    if (encryptRes is Cancellation)
                        return;
                    else if (encryptRes is Failure failure)
                    {
                        // nie udało się zaszyfrować katalogu użytkownika, więc crashujemy program
                        var e = failure.Reason;
                        e.Prepend("|Error occured while| " +
                            "|encrypting user's database.| |Database may have been corrupted.|");
                        Alert(e.Message);
                        throw e;
                    }
                    _loggedUserKey = default;
                    ShowLocalUsersDialog();
                }
            });

            AddConversation = new RelayCommand(_ =>
            {
                var vm = new CreateConversationViewModel()
                {
                    Title = "|Add_conversation|",
                    ConfirmButtonText = "|Add|"
                };
                new FormWindow(window!, vm).ShowDialog();
                if (!(vm.Result is Success success))
                    // Anulowanie
                    return;

                _client.Request(new AddConversationUIRequest((string)success.Data!));
            });

            OpenConversationDetails = new RelayCommand(obj =>
            {
                var conversationObs = (Conversation)obj!;
                var knownUsers = ListKnownUsers();

                if (SelectedAccount!.RemoteId == conversationObs.Owner.Id)
                {
                    // Aktualny użytkownik jest właścicielem konwersacji.
                    OwnerConversationDetailsViewModel.ShowDialog(window!, _client, knownUsers,
                        SelectedAccount, conversationObs);
                    return;
                }

                // Pętla do przełączania trybu okna z Regular na Admin.
                Result result;
                do
                {
                    var userAdminParticipation = conversationObs.Participations.SingleOrDefault(
                    p => p.ParticipantId == SelectedAccount.RemoteId && p.IsAdministrator);
                    result = userAdminParticipation is null ?
                        // Aktualny użytkownik nie jest administratorem konwersacji.
                        RegularConversationDetailsViewModel.ShowDialog(window!, _client, knownUsers,
                            SelectedAccount, conversationObs) :
                        // Aktualny użytkownik jest administratorem konwersacji.
                        AdminConversationDetailsViewModel.ShowDialog(window!, _client, knownUsers,
                            SelectedAccount, conversationObs);
                } while (!(result is Cancellation));
            });

            Disconnect = new RelayCommand(_ =>
            {
                /* Przycisk jest niewidoczny, kiedy SelectedAccount jest
                nullem, więc SelectedServer nie jest tutaj nullem. */
                _client.Request(new Disconnect(SelectedServer!.GetPrimaryKey(), null));
            });

            _client.ServerIntroduced += OnServerIntroduced;
            _client.ServerHandshaken += OnServerHandshaken;
            _client.ReceivedConversationsAndUsersList += OnReceivedConversationsAndUsersList;
            _client.ServerEndedConnection += OnServerEndedConnection;
            _client.ReceivedAddedConversation += OnReceivedAddedConversation;
            _client.ReceivedEditedConversation += OnReceivedEditedConversation;
            _client.ReceivedDeletedConversation += OnReceivedDeletedConversation;
            _client.ReceivedAddedParticipation += OnReceivedAddedParticipation;
            _client.ReceivedAddedYouAsParticipant += OnReceivedAddedYouAsParticipant;
            _client.ReceivedEditedParticipation += OnReceivedEditedParticipation;
            _client.ReceivedDeletedParticipation += OnReceivedDeletedParticipation;
        }

        private void ShowLocalUsersDialog()
        {
            while (true)
            {
                var loginRes = LocalUsersViewModel.ShowDialog(window!, _storage);
                /* jeżeli użytkownik zamknął okno bez zalogowania się
                (nie ma innych możliwych wyników niż Success i Cancellation) */
                if (!(loginRes is Success loginSuc))
                {
                    Application.Current.Shutdown();
                    return;
                }
                // jeżeli użytkownik się zalogował, to vm.Result is Success
                var dat = (dynamic)loginSuc.Data!;
                var curPas = (SecureString)dat.Password;
                var user = (LocalUser)dat.LoggedUser;
                var userKey = user.GetPrimaryKey();

                var decryptRes = ProgressBarViewModel.ShowDialog(window!,
                    "|Decrypting user's database.|", true,
                    (reporter) => _storage.DecryptLocalUser(ref reporter, userKey, curPas));
                curPas.Dispose();
                if (decryptRes is Cancellation)
                    Alert("|User's database decryption canceled. Logging out.|");
                /* jeżeli decryptRes is Failure, to alert z błędem został już wyświetlony w
                ProgressBarViewModel.Worker_RunWorkerCompleted */
                else if (decryptRes is Success)
                {
                    _storage.SetLoggedLocalUser(true, userKey);
                    _loggedUserKey = userKey;
                    ResetLists();
                    return;
                }
            }
        }

        private void ResetLists()
        {
            Servers.Clear();
            try
            {
                var servers = _storage.GetAllServers(_loggedUserKey);
                for (int i = 0; i < servers.Count; ++i)
                    Servers.Add(servers[i]);
            }
            catch (Error e)
            {
                e.Prepend("|Could not| |read user's server list|.");
                Alert(e.Message);
                throw;
            }
        }

        #region Client events
        private void OnServerIntroduced(RemoteServer server)
        {
            // Wątek Client.Process
            /* Nie synchronizujemy, bo użytkownik może edytować tylko serwer,
            z którym nie jest połączony. Jeżeli chce edytować zaznaczony serwer,
            to jest z nim rozłączany przed otwarciem okna edycji serwera. */
            if ((!SelectedServer!.Guid.Equals(server.Guid)
                || !(SelectedServer.PublicKey is null))
                /* Już wcześniej ustawiono GUID lub klucz publiczny, więc aktualizujemy
                zgodnie z danymi od serwera, o ile użytkownik się zgodzi. Użytkownik
                musi szybko klikać, bo serwer liczy timeout, podczas którego czeka
                na przedstawienie się klienta. */
                && !AskIfServerTrusted(server.Guid!.Value, server.PublicKey!))
            {
                /* Serwer rozłączy klienta przez timeout, ale możemy
                też sami się rozłączyć za pomocą UIRequesta. */
                _client.Request(new Disconnect(server.GetPrimaryKey(), null));
                return;
            }

            /* Jeszcze nie ustawiono GUIDu i klucza publicznego lub
            były już ustawione, ale użytkownik zgodził się na
            aktualizację. */
            SelectedServer.Guid = server.Guid.Value;
            SelectedServer.PublicKey = server.PublicKey;
            _storage.UpdateServer(_loggedUserKey,
                SelectedServer.GetPrimaryKey(), SelectedServer);

            _client.Request(new IntroduceClient(server.GetPrimaryKey()));
        }

        private bool AskIfServerTrusted(Guid guid, PublicKey publicKey)
        {
            bool guidChanged = false;
            if (!guid.Equals(SelectedServer!.Guid))
                guidChanged = true;
            bool publicKeyChanged = false;
            if (!publicKey.Equals(SelectedServer.PublicKey))
                publicKeyChanged = true;

            // Nic się nie zmieniło.
            if (!guidChanged && !publicKeyChanged)
                return true;

            var sb = new StringBuilder("|Server changed its| ");
            if (guidChanged && publicKeyChanged)
                sb.Append("GUID |and| |public key|");
            else if (guidChanged)
                sb.Append("GUID");
            else // publicKeyChanged
                sb.Append("|public key|");

            return UIInvoke<bool>(() =>
            {
                var confirmRes = ConfirmationViewModel.ShowDialog(window!,
                    $"{sb}. |Do you still want to connect to the server|?",
                    "|Connect|", "|No|", "|Yes|");
                return confirmRes is Success;
            });
        }

        private void OnServerHandshaken(RemoteServer server, ulong accountId)
        {
            // Bez UIInvoke, bo GUI nie obserwuje RemoteId.
            SelectedAccount!.RemoteId = accountId;
        }

        private void OnReceivedConversationsAndUsersList(RemoteServer server,
            Conversation[] conversations)
        {
            UIInvoke(() =>
            {
                ConversationVM.Conversation = null;
                Conversations.Clear();

                foreach (var c in conversations)
                    Conversations.Add(c);
            });
        }

        private void OnServerEndedConnection(RemoteServer server, string statusMsg)
        {
            // Wątek Client.Process
            UIInvoke(() =>
            {
                _selectedAccount = null;
                OnPropertyChanged(nameof(SelectedAccount));

                ConversationVM.Conversation = null;
                Conversations.Clear();

                Alert($"{server} {statusMsg}");
            });
        }

        private void OnReceivedAddedConversation(RemoteServer server,
            AddedConversation.Conversation inConversation)
        {
            // Wątek Client.Process
            /* Zakładamy, że autorytatywny serwer wykonał walidację i nie
            wysłał nam już istniejącej konwersacji jako nowo dodanej. */
            User? owner = null;
            foreach (var c in Conversations)
            {
                if (c.Owner.Id == SelectedAccount!.RemoteId)
                {
                    owner = c.Owner;
                    break;
                }

                owner = c.Participations.SingleOrDefault(
                    p => p.ParticipantId == SelectedAccount.RemoteId)?.Participant;
                if (!(owner is null))
                    break;
            }
            // Jeszcze nie mamy nigdzie zapisanego aktualnego użytkownika.
            if (owner is null)
                owner = new User
                {
                    Id = SelectedAccount!.RemoteId,
                    Login = SelectedAccount.Login,
                    PublicKey = SelectedAccount.PrivateKey.ToPublicKey(),
                    // Aktualny użytkownik nie jest zablokowany, skoro używa konta.
                    IsBlocked = false
                };

            UIInvoke(() =>
            {
                // Aktualny użytkownik dodał konwersację.
                Conversations.Add(new Conversation
                {
                    Id = inConversation.Id,
                    Owner = owner,
                    Name = inConversation.Name,
                    // Participations i Messages pozostają puste.
                });
            });
        }

        private void OnReceivedEditedConversation(RemoteServer server,
            EditedConversation.Conversation inConversation)
        {
            // Wątek Client.Process
            var conversationObs = Conversations.Single(c => c.Id == inConversation.Id);
            // Aktualny użytkownik zmodyfikował konwersację.
            UIInvoke(() => conversationObs.Name = inConversation.Name);
        }

        private void OnReceivedDeletedConversation(RemoteServer server, ulong inConversationId)
        {
            // Wątek Client.Process
            var conversationObs = Conversations.Single(c => c.Id == inConversationId);
            UIInvoke(() => Conversations.Remove(conversationObs));
        }

        private void OnReceivedAddedParticipation(RemoteServer server,
            AddedParticipation.Participation inParticipation)
        {
            // Wątek Client.Process
            var inParticipant = inParticipation.Participant;
            var knownUsers = ListKnownUsers();
            if (!knownUsers.ContainsKey(inParticipant.Id))
                /* Jeszcze nie mamy obiektu użytkownika (z którejś z
                istniejących konwersacji), więc go dodajemy. */
                knownUsers.Add(inParticipant.Id, new User
                {
                    Id = inParticipant.Id,
                    Login = inParticipant.Login,
                    PublicKey = inParticipant.PublicKey,
                    IsBlocked = false
                });

            var conversationObs = Conversations.Single(c => c.Id == inParticipation.ConversationId);
            var cpObs = new ConversationParticipation()
            {
                ConversationId = conversationObs.Id,
                Conversation = conversationObs,
                ParticipantId = inParticipation.Participant.Id,
                Participant = knownUsers[inParticipant.Id],
                JoinTime = DateTimeOffset.FromUnixTimeMilliseconds(inParticipation.JoinTime).UtcDateTime,
                IsAdministrator = inParticipation.IsAdministrator != 0
            };

            UIInvoke(() => conversationObs.Participations.Add(cpObs));
        }

        private void OnReceivedAddedYouAsParticipant(RemoteServer server,
            AddedYouAsParticipant.YourParticipation yourParticipation)
        {
            // Wątek Client.Process
            var knownUsers = ListKnownUsers();
            // Dołączamy właściciela konwersacji do jej uczestników
            foreach (var u in yourParticipation.Conversation.Participations.Select(p => p.Participant)
                .Append(yourParticipation.Conversation.Owner))
                if (!knownUsers.ContainsKey(u.Id))
                    knownUsers.Add(u.Id, new User
                    {
                        Id = u.Id,
                        Login = u.Login,
                        PublicKey = u.PublicKey,
                        IsBlocked = false // Do usunięcia
                    });

            var conversationObs = new Conversation
            {
                Id = yourParticipation.Conversation.Id,
                Owner = knownUsers[yourParticipation.Conversation.Owner.Id],
                Name = yourParticipation.Conversation.Name
            };

            foreach (var p in yourParticipation.Conversation.Participations)
                conversationObs.Participations.Add(new ConversationParticipation
                {
                    ConversationId = yourParticipation.Conversation.Id,
                    Conversation = conversationObs,
                    ParticipantId = p.Participant.Id,
                    Participant = knownUsers[p.Participant.Id],
                    JoinTime = DateTimeOffset.FromUnixTimeMilliseconds(p.JoinTime).UtcDateTime,
                    IsAdministrator = p.IsAdministrator != 0
                });

            UIInvoke(() => Conversations.Add(conversationObs));
        }

        private Dictionary<ulong, User> ListKnownUsers()
        {
            var knownUsers = new Dictionary<ulong, User>();
            foreach (var c in Conversations)
            {
                if (!knownUsers.ContainsKey(c.Owner.Id))
                    knownUsers.Add(c.Owner.Id, c.Owner);

                foreach (var p in c.Participations)
                    if (!knownUsers.ContainsKey(p.ParticipantId))
                        knownUsers.Add(p.ParticipantId, p.Participant);
            }
            return knownUsers;
        }

        private void OnReceivedEditedParticipation(RemoteServer server,
            EditedParticipation.Participation inParticipation)
        {
            // Wątek Client.Process
            var conversationObs = Conversations.Single(c => c.Id == inParticipation.ConversationId);
            // Wszystko z conversationObs.Participations ma to samo ConversationId.
            var cpObs = conversationObs.Participations.Single(
                p => p.ParticipantId == inParticipation.ParticipantId);
            UIInvoke(() => cpObs.IsAdministrator = inParticipation.IsAdministrator != 0);
        }

        private void OnReceivedDeletedParticipation(RemoteServer server,
            DeletedParticipation.Participation inParticipation)
        {
            // Wątek Client.Process
            var conversationObs = Conversations.Single(c => c.Id == inParticipation.ConversationId);
            var cpObs = conversationObs.Participations.Single(
                p => p.ParticipantId == inParticipation.ParticipantId);
            UIInvoke(() =>
            {
                conversationObs.Participations.Remove(cpObs);
                if (inParticipation.ParticipantId == SelectedAccount!.RemoteId)
                    Conversations.Remove(conversationObs);
            });
        }
        #endregion
    }
}
