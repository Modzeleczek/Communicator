﻿using Server.MVVM.View.Windows;
using Shared.MVVM.Model;
using Shared.MVVM.ViewModel.LongBlockingOperation;
using System.ComponentModel;
using System.Windows;
using BaseProgressBarViewModel = Shared.MVVM.ViewModel.LongBlockingOperation.ProgressBarViewModel;

namespace Server.MVVM.ViewModel
{
    public class ProgressBarViewModel : BaseProgressBarViewModel
    {
        protected ProgressBarViewModel(Work work, string description, bool cancelable,
            bool progressBarVisible) :
            base(work, description, cancelable, progressBarVisible) { }

        // wywoływane po wyjściu z handlera DoWork poprzez return lub wyjątek
        protected override void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var status = (Status)e.Result;
            /* if (e.Error != null) // wystąpił błąd
            else if (e.Cancelled) // użytkownik anulował
            else // zakończono powodzeniem */
            if (status.Code < 0) Alert(status.Message);
            OnRequestClose(status); // we wszystkich 3 przypadkach (błąd, anulowanie, powodzenie) informacja dla wywołującego viewmodelu jest w statusie
        }

        public static Status ShowDialog(Window owner, string operationDescriptionText,
            bool cancelable, Work work, bool progressBarVisible = true)
        {
            var vm = new ProgressBarViewModel(work, operationDescriptionText, cancelable,
                progressBarVisible);
            var win = new ProgressBarWindow(owner, vm);
            // zapobiegamy ALT + F4 w oknie z progress barem
            win.Closable = false;
            vm.RequestClose += (sender, args) =>
            {
                win.Closable = true;
                win.Close();
            };
            vm.BeginWorking();
            win.ShowDialog();
            return vm.Status;
        }

        private void Alert(string description) => AlertViewModel.ShowDialog(window, description);
    }
}
