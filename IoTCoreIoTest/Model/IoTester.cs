using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;

namespace IoTCoreIoTest.Model
{
    [Notify]
    public class IoTester : INotifyPropertyChanged, IDisposable
    {
        public enum TransferMethod
        {
            Write,
            TransferFullDuplex,
            TransferSequential,
        }

        public enum IoTesterStatus
        {
            Initializing,
            Idle,
            Running,
        }

        private SpiDevice device;
        private Task initializationTask;
        private Task communicationTask;
        private CancellationTokenSource communicationCancel;

        [NonNotify]
        public bool IsSupported { get; } = Windows.Foundation.Metadata.ApiInformation.IsTypePresent(typeof(Windows.Devices.Gpio.GpioPin).FullName);

        public double LastTransmissionRate { get { return lastTransmissionRate; } set { SetProperty(ref lastTransmissionRate, value, lastTransmissionRatePropertyChangedEventArgs); } }

        public TransferMethod Method { get { return method; } set { SetProperty(ref method, value, methodPropertyChangedEventArgs); } }

        public int NumberOfTransfers { get { return numberOfTransfers; } set { SetProperty(ref numberOfTransfers, value, numberOfTransfersPropertyChangedEventArgs); } }

        public IoTesterStatus Status { get { return status; } set { SetProperty(ref status, value, statusPropertyChangedEventArgs); } }

        public double AverageTransmissionRate { get { return averageTransmissionRate; } private set { SetProperty(ref averageTransmissionRate, value, averageTransmissionRatePropertyChangedEventArgs); } }

        public IoTester()
        {
            this.NumberOfTransfers = 10;
            this.Status = IoTesterStatus.Initializing;
            this.initializationTask = this.Initialize();
        }

        private async Task Initialize()
        {
            if (this.IsSupported)
            {
                var aqs = SpiDevice.GetDeviceSelector("SPI0");
                var deviceInformations = await DeviceInformation.FindAllAsync(aqs);
                if (deviceInformations.Count == 0)
                {
                    return;
                }
                var deviceId = deviceInformations.First().Id;

                var settings = new SpiConnectionSettings(0)
                {
                    ClockFrequency = 30 * 1000 * 1000,  // 100[kHz]
                    Mode = SpiMode.Mode0,
                    SharingMode = SpiSharingMode.Exclusive,
                };
                this.device = await SpiDevice.FromIdAsync(deviceId, settings);
            }
            this.Status = IoTesterStatus.Idle;
        }

        public void StartMeasurement()
        {
            if (this.Status != IoTesterStatus.Idle) throw new InvalidOperationException();

            this.Status = IoTesterStatus.Running;
            this.communicationCancel = new CancellationTokenSource();
            this.communicationTask = Task.Run(() => this.CommunicationTask(this.communicationCancel.Token), this.communicationCancel.Token);
        }
        private async Task CommunicationTask(CancellationToken cancellationToken)
        {
            var stopwatch = new Stopwatch();
            byte[] data = new byte[65536];

            var transferMethod = this.Method;
            double totalSeconds = 0;
            var numberOfTransfers = this.NumberOfTransfers;

            try
            {
                for (var count = 0; count < numberOfTransfers; count++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    stopwatch.Restart();
                    switch (transferMethod)
                    {
                        case TransferMethod.Write:
                            this.device.Write(data);
                            break;
                        case TransferMethod.TransferFullDuplex:
                            this.device.TransferFullDuplex(data, data);
                            break;
                        case TransferMethod.TransferSequential:
                            this.device.TransferSequential(data, data);
                            break;
                    }
                    stopwatch.Stop();
                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    totalSeconds += elapsed;
                    this.LastTransmissionRate = elapsed > 0 ? data.Length / elapsed : 0;

                    await Task.Yield();
                }
                this.AverageTransmissionRate = (numberOfTransfers * data.Length) / totalSeconds;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                this.Status = IoTesterStatus.Idle;
            }
        }

        public async Task StopMeasurementAsync()
        {
            this.communicationCancel?.Cancel();
            await this.communicationTask;
        }

        public void Dispose()
        {
            this.initializationTask?.Wait();
            this.initializationTask = null;

            this.communicationCancel?.Cancel();
            this.communicationTask?.Wait();
            this.communicationTask = null;
            this.communicationCancel?.Dispose();
            this.communicationCancel = null;

            this.device?.Dispose();
            this.device = null;
        }

        #region NotifyPropertyChangedGenerator

        public event PropertyChangedEventHandler PropertyChanged;

        private double lastTransmissionRate;
        private static readonly PropertyChangedEventArgs lastTransmissionRatePropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(LastTransmissionRate));
        private TransferMethod method;
        private static readonly PropertyChangedEventArgs methodPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(Method));
        private int numberOfTransfers;
        private static readonly PropertyChangedEventArgs numberOfTransfersPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(NumberOfTransfers));
        private IoTesterStatus status;
        private static readonly PropertyChangedEventArgs statusPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(Status));
        private double averageTransmissionRate;
        private static readonly PropertyChangedEventArgs averageTransmissionRatePropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(AverageTransmissionRate));

        private void SetProperty<T>(ref T field, T value, PropertyChangedEventArgs ev)
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, ev);
            }
        }

        #endregion
    }
}
