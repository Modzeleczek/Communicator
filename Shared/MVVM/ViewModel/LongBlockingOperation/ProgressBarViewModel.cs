﻿using Shared.MVVM.Core;
using System.ComponentModel;
using System.Windows;

namespace Shared.MVVM.ViewModel.LongBlockingOperation
{
    public abstract class ProgressBarViewModel : WindowViewModel
    {
        #region Properties
        private double progress;
        public double Progress
        {
            get => progress;
            set { progress = value; OnPropertyChanged(); }
        }
        // tylko progress ma setter z powiadamianiem widoku (view) o zmianie (OnPropertyChanged), ponieważ 
        public string Description { get; }
        public bool Cancelable { get; }
        public bool ProgressBarVisible { get; }
        #endregion

        #region Fields
        private BackgroundWorker worker;
        #endregion

        protected ProgressBarViewModel(Work work, string description, bool cancelable,
            bool progressBarVisible)
        {
            // potrzebne, jeżeli chcemy pojawiać alerty nad oknem postępu (ProgressBarWindow)
            WindowLoaded = new RelayCommand(e => window = (Window)e);
            /* worker.CancelAsync tylko ustawia worker.cancellationPending na true
            getterem do worker.cancellationPending jest worker.CancellationPending */
            Close = new RelayCommand(e => worker.CancelAsync());
            worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            DoWorkEventHandler doWork = (worker, args) =>
            {
                work(new ProgressReporter((BackgroundWorker)worker, args));
            };
            worker.DoWork += doWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            Description = description;
            Cancelable = cancelable;
            ProgressBarVisible = progressBarVisible;
        }

        protected void BeginWorking()
        {
            worker.RunWorkerAsync();
        }

        // wywoływane co wywołanie worker.ReportProgress
        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Progress = e.ProgressPercentage;
        }

        // wywoływane po wyjściu z handlera DoWork poprzez return lub wyjątek
        protected abstract void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e);
    }
}