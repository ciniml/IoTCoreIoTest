using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public ReactiveProperty<double> AverageTransmissionRate { get; }
        public ReactiveCommand StartCommunicationCommand { get; }
        public ReactiveCommand StopCommunicationCommand { get; }
        public ReactiveProperty<Model.IoTester.TransferMethod> TransferMethod { get; }

        public IReadOnlyList<Model.IoTester.TransferMethod> TransferMethods { get; }
         
        public MainPageViewModel()
        {
            var ioTester = ((App)Windows.UI.Xaml.Application.Current).IoTester;

            this.IsInitialized = ioTester.ObserveProperty(self => self.Status)
                .Select(value => value != Model.IoTester.IoTesterStatus.Initializing)
                .ToReactiveProperty().AddTo(this.disposables);

            this.LastTransmissionRate = ioTester.ObserveProperty(self => self.LastTransmissionRate)
                .Buffer(TimeSpan.FromSeconds(0.5))
                .Select(values => values.Count > 0 ? values.Average() : 0)
                .ToReactiveProperty().AddTo(this.disposables);
            this.AverageTransmissionRate = ioTester.ObserveProperty(self => self.AverageTransmissionRate)
                .ToReactiveProperty().AddTo(this.disposables);

            this.StartCommunicationCommand = ioTester.ObserveProperty(self => self.Status)
                .Select(value => value == Model.IoTester.IoTesterStatus.Idle)
                .ToReactiveCommand().AddTo(this.disposables);
            this.StartCommunicationCommand
                .Do(_ => ioTester.StartMeasurement())
                .Subscribe()
                .AddTo(this.disposables);

            this.StopCommunicationCommand = ioTester.ObserveProperty(self => self.Status)
                .Select(value => value == Model.IoTester.IoTesterStatus.Running)
                .ToReactiveCommand()
                .AddTo(this.disposables);
            this.StopCommunicationCommand
                .Do(_ => ioTester.StopMeasurementAsync())
                .Subscribe()
                .AddTo(this.disposables);

            this.TransferMethod = ioTester.ToReactivePropertyAsSynchronized(self => self.Method).AddTo(this.disposables);

            this.TransferMethods =
                Enum.GetValues(typeof (Model.IoTester.TransferMethod))
                    .OfType<Model.IoTester.TransferMethod>()
                    .ToImmutableList();
        }

        public void Dispose()
        {
            this.disposables?.Dispose();
            this.disposables = null;
        }
    }
}
