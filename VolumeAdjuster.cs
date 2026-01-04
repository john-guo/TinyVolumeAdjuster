using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Media;
using System.Windows.Threading;

namespace TinyVolumeAdjuster
{
    public partial class VolumeItem : ObservableObject {
        public int VolumeLevel
        {
            get => (int)(SessionControl?.SimpleAudioVolume.Volume * 100 ?? 0);
            set 
            {
                int volume = -1;
                if (SetProperty(ref volume, value))
                {
                    SessionControl?.SimpleAudioVolume.Volume = volume / 100.0f;
                }
            }
        }


        [ObservableProperty]
        private ImageSource? icon;

        [ObservableProperty]
        private string? displayName;

        private ImageSource? ResolveIcon()
        {
            if (SessionControl == null)
                return null;
            string? iconPath = SessionControl?.IconPath;
            if (string.IsNullOrWhiteSpace(iconPath))
            {
                if (string.IsNullOrEmpty(processPath))
                    return null;
                return Utils.LoadExeIcon(processPath);
            }
            return Utils.LoadIcon(Utils.NormalizeIconPath(iconPath), large: false);
        }

        private string? ResolveName()
        {
            var name = SessionControl?.DisplayName ?? SessionControl?.GetSessionIdentifier;
            if (!string.IsNullOrWhiteSpace(name))
                return Utils.ResolveIndirectString(name);
            name = processPath;
            if (!string.IsNullOrWhiteSpace(name))
                return System.IO.Path.GetFileNameWithoutExtension(name);
            return $"PID: {SessionControl?.GetProcessID}";
        }


        public void NotifyVolumeChanged()
        {
            OnPropertyChanged(nameof(VolumeLevel));
        }

        public void NotifyMuteChanged()
        {
            OnPropertyChanged(nameof(IsMuted));
            OnPropertyChanged(nameof(MuteIcon));
        }

        public IRelayCommand ToggleMuteCommand { get; }

        public bool IsMuted
        {
            get => SessionControl?.SimpleAudioVolume.Mute ?? false;
            set
            {
                bool isMuted = !value;
                if (SetProperty(ref isMuted, value))
                {
                    SessionControl?.SimpleAudioVolume.Mute = isMuted;
                    OnPropertyChanged(nameof(MuteIcon));
                }
            }
        }

        public string MuteIcon => IsMuted ? "🔇" : "🔊";


        [ObservableProperty]
        private AudioSessionControl? sessionControl;

        private string? processPath;
        partial void OnSessionControlChanged(AudioSessionControl? oldValue, AudioSessionControl? newValue)
        {
            int pid = SessionControl == null ? -1 : (int)SessionControl.GetProcessID;
            if (pid < 0)
                return;
            processPath = Utils.GetProcessPath(pid);

            DisplayName = ResolveName();
            Icon = ResolveIcon();
        }

        public VolumeItem()
        {
            ToggleMuteCommand = new RelayCommand(() =>
            {
                IsMuted = !IsMuted;
            });
        }

        internal VolumeAdjuster.AudioSessionEventHandler? _handler;
    }


    class VolumeAdjuster : IDisposable
    {
        internal class AudioSessionEventHandler : IAudioSessionEventsHandler
        {
            private VolumeAdjuster _adjuster;
            private VolumeItem _item;
            public AudioSessionEventHandler(VolumeAdjuster adjuster, VolumeItem item)
            {
                _item = item;
                _adjuster = adjuster;
            }

            public void OnChannelVolumeChanged(uint channelCount, nint newVolumes, uint channelIndex)
            {
                if (_item == null)
                    return;
                App.Current.Dispatcher.BeginInvoke(() => _item.NotifyVolumeChanged());
            }

            public void OnDisplayNameChanged(string displayName)
            {
            }

            public void OnGroupingParamChanged(ref Guid groupingId)
            {
            }

            public void OnIconPathChanged(string iconPath)
            {
            }

            public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
            {
                CloseSession();
            }

            public void OnStateChanged(AudioSessionState state)
            {
                if (state == AudioSessionState.AudioSessionStateExpired)
                {
                    CloseSession();
                }
            }

            private void CloseSession()
            {
                if (_item == null)
                    return;
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        _adjuster.RemoveItem(_item);
                        _item.SessionControl?.UnRegisterEventClient(this);
                        _item._handler = null;
                        _item.SessionControl?.Dispose();
                        _item.SessionControl = null;
                        _item = null!;
                    }
                    catch { }
                });
            }

            public void OnVolumeChanged(float volume, bool isMuted)
            {
                if (_item == null)
                    return;
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    _item.NotifyVolumeChanged();
                    _item.NotifyMuteChanged();
                });
            }
        }

        class DeviceNotificationHandler : IMMNotificationClient
        {
            private VolumeAdjuster _adjuster;
            public DeviceNotificationHandler(VolumeAdjuster adjuster)
            {
                _adjuster = adjuster;
            }

            public void OnDeviceAdded(string pwstrDeviceId)
            {
                
            }
            public void OnDeviceRemoved(string pwstrDeviceId)
            {
                
            }
            public void OnDeviceStateChanged(string pwstrDeviceId, DeviceState dwNewState)
            {
                if (pwstrDeviceId == _adjuster.DevID && dwNewState != DeviceState.Active)
                {
                    _adjuster.DeviceUninit();
                }
                else if (pwstrDeviceId == _adjuster.DevID && dwNewState == DeviceState.Active)
                {
                    _adjuster.DeviceInit();
                }
            }
            public void OnDefaultDeviceChanged(DataFlow flow, Role role, string pwstrDefaultDeviceId)
            {
                if (flow == DataFlow.Render && role == Role.Multimedia)
                {
                    _adjuster.DeviceUninit();
                    _adjuster.DeviceInit();
                }
            }
            public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
            {
                
            }
        }

        private MMDeviceEnumerator? _deviceEnumerator;
        private MMDevice? _device;
        private AudioSessionManager? _sessionManager;
        private ObservableCollection<VolumeItem> _items;
        private byte loc = 0;
        private DeviceNotificationHandler? _handler;
        public VolumeAdjuster()
        {
            _items = [];
            DeviceInit();
        }

        string DevID => _device?.ID ?? string.Empty;

        private void DeviceInit()
        {
            if (Interlocked.CompareExchange(ref loc, 2, 0) != 0)
                return;

            if (_deviceEnumerator != null)
                return;
            try
            {
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    _deviceEnumerator = new MMDeviceEnumerator();
                    _handler = new DeviceNotificationHandler(this);
                    _deviceEnumerator.RegisterEndpointNotificationCallback(_handler);
                    _device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    if (_device == null)
                        return;
                    _sessionManager = _device.AudioSessionManager;
                    _items.Clear();
                    for (int i = 0; i < _sessionManager.Sessions.Count; i++)
                    {
                        var session = _sessionManager.Sessions[i];
                        var item = new VolumeItem
                        {
                            SessionControl = session
                        };
                        item._handler = new AudioSessionEventHandler(this, item);
                        session.RegisterEventClient(item._handler);
                        _items.Add(item);
                    }
                    _sessionManager.OnSessionCreated += OnSessionCreated;
                });
            }
            finally
            {
                Interlocked.Exchange(ref loc, 1);
            }
        }

        private void DeviceUninit()
        {
            if (Interlocked.CompareExchange(ref loc, 2, 1) != 1)
                return;
            if (_deviceEnumerator == null)
                return;
            try
            {
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (_sessionManager != null)
                    {
                        _sessionManager.OnSessionCreated -= OnSessionCreated;

                        _sessionManager.Dispose();
                        _sessionManager = null;
                    }
                    if (_device != null)
                    {
                        _device.Dispose();
                        _device = null;
                    }
                    if (_deviceEnumerator != null)
                    {
                        _deviceEnumerator.UnregisterEndpointNotificationCallback(_handler);
                        _deviceEnumerator.Dispose();
                        _deviceEnumerator = null;
                    }
                });
            }
            finally
            {
                Interlocked.Exchange(ref loc, 0);
            }
        }

        private void OnSessionCreated(object? sender, IAudioSessionControl newSession)
        {
            App.Current.Dispatcher.BeginInvoke(() => 
            {
                try
                {
                    var item = new VolumeItem
                    {
                        SessionControl = new AudioSessionControl(newSession)
                    };
                    item._handler = new AudioSessionEventHandler(this, item);
                    item.SessionControl?.RegisterEventClient(item._handler);
                    _items.Add(item);
                }
                catch { }
            });
        }

        public ObservableCollection<VolumeItem> GetItems()
        {
            return _items;
        }

        void RemoveItem(VolumeItem item)
        {
            _items.Remove(item);
        }

        public void Dispose()
        {
            DeviceUninit();
        }
    }
}
