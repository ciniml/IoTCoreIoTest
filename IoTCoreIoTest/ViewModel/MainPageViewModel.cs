﻿using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTCoreIoTest.ViewModel
{
    public class MainPageViewModel : IDisposable
    {
        private CompositeDisposable disposables = new CompositeDisposable();

        public ReactiveProperty<bool> IsInitialized { get; }
        public ReactiveProperty<double> LastTransmissionRate { get; }
        public ReactiveCommand StopCommunicationCommand { get; }

        public MainPageViewModel()
        {
            var ioTester = ((App)Windows.UI.Xaml.Application.Current).IoTester;

            (this.IsInitialized = ioTester.ObserveProperty(self => self.IsInitialized)
                .ToReactiveProperty()).AddTo(this.disposables);
            (this.LastTransmissionRate = ioTester.ObserveProperty(self => self.LastTransmissionRate)
                .Buffer(TimeSpan.FromSeconds(1))
                .Select(values => values.Count > 0 ? values.Average() : 0)
                .ToReactiveProperty()).AddTo(this.disposables);
            (this.StopCommunicationCommand = new ReactiveCommand()).AddTo(this.disposables);
            this.StopCommunicationCommand
                .Do(_ => ioTester.StopCommunication())
                .Subscribe()
                .AddTo(this.disposables);
        }

        public void Dispose()
        {
            this.disposables?.Dispose();
            this.disposables = null;
        }
    }
}
