﻿using Client.MVVM.Model.BsonStorages;
using Client.MVVM.Model.JsonSerializables;
using Shared.MVVM.Core;
using Shared.MVVM.Model;
using Shared.MVVM.Model.Networking;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;

namespace Client.MVVM.Model
{
    public class LocalUser : ObservableObject
    {
        #region Properties
        private string _name; // unikalny identyfikator
        // nazwa jest obserwowalna przez UI, dlatego setter z OnPropertyChanged
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public byte[] PasswordSalt { get; set; } // sól do skrótu hasła nie jest obserwowalna

        public byte[] PasswordDigest { get; set; } // skrót hasła nie jest obserwowalny

        // wektor inicjujący i sól hasła do odszyfrowania bazy danych
        public byte[] DbInitializationVector { get; set; }

        public byte[] DbSalt { get; set; }

        public string DirectoryPath => Path.Combine(LocalUsersStorage.USERS_DIRECTORY_NAME, Name);

        private ServersStorage serversStorage => new ServersStorage(this);
        #endregion

        public LocalUser() { }

        public LocalUser(string name, SecureString password)
        {
            Name = name;
            ResetPassword(password);
        }

        public LocalUserSerializable ToSerializable() =>
            new LocalUserSerializable
            {
                Name = Name,
                PasswordSalt = PasswordSalt,
                PasswordDigest = PasswordDigest,
                DbInitializationVector = DbInitializationVector,
                DbSalt = DbSalt
            };

        private byte[] GenerateRandom(int byteCount)
        {
            var bytes = new byte[byteCount];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return bytes;
        }

        public void ResetPassword(SecureString newPassword)
        {
            var pc = new PasswordCryptography();
            var passwordSalt = GenerateRandom(128 / 8); // 128 b sól
            PasswordSalt = passwordSalt;
            PasswordDigest = pc.ComputeDigest(newPassword, passwordSalt); // 128 b - rozmiar klucza AESa
            DbInitializationVector = GenerateRandom(128 / 8); // 128 b - rozmiar bloku AESa
            DbSalt = GenerateRandom(128 / 8); // 128 b sól
        }

        public Status AddServer(Server server) =>
            serversStorage.Add(server);

        public Status GetAllServers() =>
            serversStorage.GetAll();

        public Status ServerExists(IPv4Address ipAddress, Port port) =>
            serversStorage.Exists(ipAddress, port);

        public Status DeleteServer(IPv4Address ipAddress, Port port) =>
            serversStorage.Delete(ipAddress, port);

        private Status GetServerDatabase(IPv4Address ipAddress, Port port) =>
            serversStorage.GetServerDatabase(ipAddress, port);

        public Status GetAllAccounts(IPv4Address ipAddress, Port port)
        {
            var getDbStatus = GetServerDatabase(ipAddress, port);
            if (getDbStatus.Code != 0)
                return getDbStatus.Prepend(-1); // -1
            var db = (ServerDatabase)getDbStatus.Data;

            var getAllStatus = db.GetAllAccounts();
            if (getAllStatus.Code != 0)
                return getAllStatus.Prepend(-2); // -2

            return new Status(0, (List<Account>)getAllStatus.Data);
        }
    }
}
