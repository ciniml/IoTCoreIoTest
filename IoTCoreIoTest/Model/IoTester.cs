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
        private SpiDevice device;
        private Task initializationTask;
        private Task communicationTask;
        private CancellationTokenSource communicationCancel;

        private ConcurrentQueue<byte[]> transmissionQueue;

        [NonNotify]
        public bool IsSupported { get; } = Windows.Foundation.Metadata.ApiInformation.IsTypePresent(typeof(Windows.Devices.Gpio.GpioPin).FullName);

        public bool IsInitialized { get { return isInitialized; } set { SetProperty(ref isInitialized, value, isInitializedPropertyChangedEventArgs); } }

        public double LastTransmissionRate { get { return lastTransmissionRate; } private set { SetProperty(ref lastTransmissionRate, value, lastTransmissionRatePropertyChangedEventArgs); } }

        public IoTester()
        {
            this.IsInitialized = false;
            this.initializationTask = this.Initialize();

            this.transmissionQueue = new ConcurrentQueue<byte[]>();
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

                this.communicationCancel = new CancellationTokenSource();
                this.communicationTask = Task.Run(this.CommunicationTask, this.communicationCancel.Token);

            }
            this.IsInitialized = true;
        }

        private async Task CommunicationTask()
        {
            var stopwatch = new Stopwatch();
            byte[] data = new byte[65536];
            try
            {
                while (true)
                {
                    this.communicationCancel.Token.ThrowIfCancellationRequested();
                    
                    stopwatch.Restart();
                    this.device.TransferFullDuplex(data, data);
                    stopwatch.Stop();
                    var totalSeconds = stopwatch.Elapsed.TotalSeconds;
                    this.LastTransmissionRate = totalSeconds > 0 ? data.Length / totalSeconds : 0;

                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public async Task StopCommunication()
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

        private bool isInitialized;
        private static readonly PropertyChangedEventArgs isInitializedPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(IsInitialized));
        private double lastTransmissionRate;
        private static readonly PropertyChangedEventArgs lastTransmissionRatePropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(LastTransmissionRate));

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
