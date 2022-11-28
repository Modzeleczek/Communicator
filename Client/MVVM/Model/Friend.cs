﻿using Client.MVVM.Core;

namespace Client.MVVM.Model
{
    public class Friend : ObservableObject
    {
        private string nickname;
        public string Nickname
        {
            get => nickname;
            set { nickname = value; OnPropertyChanged(); }
        }

        private string alias;
        public string Alias
        {
            get => alias;
            set { alias = value; OnPropertyChanged(); }
        }

        private string imagePath;
        public string ImagePath
        {
            get => imagePath;
            set { imagePath = value; OnPropertyChanged(); }
        }
    }
}
