using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using VeigarConfigurator.App.Commands;
using VeigarConfigurator.App.Lighting;
using VeigarConfigurator.App.Models;
using VeigarConfigurator.App.Profiles;
using VeigarConfigurator.Core.Devices;
using VeigarConfigurator.Windows.Audio;
using VeigarConfigurator.Windows.RawInput;

namespace VeigarConfigurator.App.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IDeviceController _deviceController;
        private readonly AudioPeakMeter _audioMeter;
        private readonly DispatcherTimer _deviceTimer;
        private readonly DispatcherTimer _renderTimer;
        private readonly Stopwatch _clock;
        private readonly Dictionary<int, double> _keyIntensity;
        private readonly HashSet<int> _heldSlots;
        private bool _isConnected;
        private bool _isBusy;
        private bool _isSendingFrame;
        private byte[] _pendingFrame;
        private bool _pendingFrameShowResult;
        private bool _disposed;
        private bool _isMappingArmed;
        private bool _audioReactiveEnabled;
        private bool _keyReactiveEnabled;
        private bool _animationRunning;
        private string _connectionTitle;
        private string _connectionMessage;
        private string _operationMessage;
        private string _footerStatus;
        private string _profileName;
        private string _hexInput;
        private string _effectiveColorHex;
        private string _audioStatus;
        private string _lastDetectedKey;
        private string _selectedSlotMapping;
        private string _selectedLayerType;
        private string _selectedTool;
        private string _selectedKeyboardKey;
        private bool _paintOnPhysicalPress;
        private bool _isLightTheme;
        private readonly HashSet<string> _pressedKeyboardKeys = new HashSet<string>();
        private double _redValue;
        private double _greenValue;
        private double _blueValue;
        private double _brightnessValue;
        private double _audioPeakLevel;
        private double _lastAudioSentLevel = -1.0;
        private int _selectedSlotIndex;
        private int _selectedWorkspaceTabIndex;
        private Brush _previewBrush;
        private LightingLayer _selectedLayer;
        private string _selectedKeyEffect;

        public MainViewModel(IDeviceController deviceController, AudioPeakMeter audioMeter)
        {
            _deviceController = deviceController;
            _audioMeter = audioMeter;
            _clock = Stopwatch.StartNew();
            _keyIntensity = new Dictionary<int, double>();
            _heldSlots = new HashSet<int>();

            LedSlots = new ObservableCollection<LedSlotViewModel>();
            KeyboardKeys = KeyboardLayoutBuilder.Create();
            Layers = new ObservableCollection<LightingLayer>();
            AvailableLayerTypes = new ObservableCollection<string>
            {
                "Static",
                "Gradient",
                "Animation",
                "AudioReactive",
                "KeyReactive"
            };
            BlendModes = new ObservableCollection<string>
            {
                "Replace",
                "Add",
                "Max"
            };
            KeyEffectOptions = new ObservableCollection<string>
            {
                "Flash",
                "Fade",
                "HoldGlow"
            };

            for (int slot = 0; slot < 126; slot++)
            {
                LedSlots.Add(new LedSlotViewModel(slot));
            }

            _redValue = 255;
            _greenValue = 32;
            _blueValue = 32;
            _brightnessValue = 100;
            _profileName = "My K643 lighting profile";
            _hexInput = "FF2020";
            _effectiveColorHex = "#FF2020";
            _previewBrush = new SolidColorBrush(Color.FromRgb(255, 32, 32));
            _selectedSlotIndex = 0;
            _selectedLayerType = "Static";
            _selectedKeyEffect = "Fade";
            _selectedTool = "Brush";
            _selectedKeyboardKey = "W";
            _paintOnPhysicalPress = true;
            _audioStatus = _audioMeter == null
                ? "Audio meter is unavailable on this system."
                : "Ready to read the default Windows audio output peak.";
            _lastDetectedKey = "No key event received yet.";
            _selectedSlotMapping = "No physical key assigned to this LED slot.";

            var baseLayer = new LightingLayer();
            baseLayer.Name = "Base colour";
            baseLayer.Mode = "Static";
            baseLayer.StartColor = new RgbColor(255, 32, 32);
            Layers.Add(baseLayer);
            SelectedLayer = baseLayer;

            ConnectionTitle = "Searching for Veigar K643...";
            ConnectionMessage = "Checking USB HID interfaces.";
            OperationMessage = "Connect the keyboard to begin.";
            FooterStatus = "Checking device";

            RefreshCommand = new RelayCommand(
                delegate { RefreshDevice(); },
                delegate { return !IsBusy; });

            SendSceneCommand = new RelayCommand(
                delegate { SendCurrentFrame(true); },
                delegate { return IsConnected && !IsBusy; });

            SendPresetCommand = new RelayCommand(
                delegate(object parameter) { ApplyPreset(parameter as string); },
                delegate { return IsConnected && !IsBusy; });

            ApplyHexCommand = new RelayCommand(
                delegate { ApplyHexColor(); },
                delegate { return IsConnected && !IsBusy; });

            SelectSlotCommand = new RelayCommand(
                delegate(object parameter) { SelectSlot(parameter); },
                delegate { return true; });

            ApplySlotColorCommand = new RelayCommand(
                delegate { ApplyColorToSelectedSlot(); },
                delegate { return !IsBusy; });

            FillAllSlotsCommand = new RelayCommand(
                delegate { FillAllSlots(); },
                delegate { return !IsBusy; });

            AddLayerCommand = new RelayCommand(
                delegate { AddLayer(); },
                delegate { return true; });

            AddAnimationLayerCommand = new RelayCommand(
                delegate { AddAnimationLayer(); },
                delegate { return true; });

            SelectWorkspaceTabCommand = new RelayCommand(
                delegate(object parameter) { SelectWorkspaceTab(parameter); },
                delegate { return true; });

            RemoveLayerCommand = new RelayCommand(
                delegate { RemoveSelectedLayer(); },
                delegate { return SelectedLayer != null && Layers.Count > 1; });

            MoveLayerUpCommand = new RelayCommand(
                delegate { MoveLayer(-1); },
                delegate { return SelectedLayer != null && Layers.IndexOf(SelectedLayer) > 0; });

            MoveLayerDownCommand = new RelayCommand(
                delegate { MoveLayer(1); },
                delegate { return SelectedLayer != null && Layers.IndexOf(SelectedLayer) >= 0 &&
                                 Layers.IndexOf(SelectedLayer) < Layers.Count - 1; });

            SetLayerStartColorCommand = new RelayCommand(
                delegate { SetSelectedLayerColor(false); },
                delegate { return SelectedLayer != null; });

            SetLayerEndColorCommand = new RelayCommand(
                delegate { SetSelectedLayerColor(true); },
                delegate { return SelectedLayer != null; });

            AddAnimationFrameCommand = new RelayCommand(
                delegate { AddAnimationFrame(); },
                delegate { return SelectedLayer != null && SelectedLayer.Mode == "Animation"; });

            RemoveAnimationFrameCommand = new RelayCommand(
                delegate { RemoveAnimationFrame(); },
                delegate { return SelectedLayer != null && SelectedLayer.Mode == "Animation" &&
                                 SelectedLayer.Frames != null && SelectedLayer.Frames.Count > 0; });

            ToggleAnimationCommand = new RelayCommand(
                delegate { AnimationRunning = !AnimationRunning; },
                delegate { return Layers.Any(item => item.Mode == "Animation" && item.IsEnabled); });

            ArmMappingCommand = new RelayCommand(
                delegate { IsMappingArmed = true; OperationMessage = "Press one key on the Veigar K643 to assign it to LED slot " + (_selectedSlotIndex + 1) + "."; },
                delegate { return IsConnected; });

            ClearMappingCommand = new RelayCommand(
                delegate { ClearSelectedMapping(); },
                delegate { return HasSelectedMapping; });

            SelectKeyboardKeyCommand = new RelayCommand(
                delegate(object parameter) { SelectKeyboardKey(parameter as string); },
                delegate { return true; });

            SelectToolCommand = new RelayCommand(
                delegate(object parameter) { SelectedTool = parameter as string; },
                delegate { return true; });

            ToggleThemeCommand = new RelayCommand(
                delegate { IsLightTheme = !IsLightTheme; },
                delegate { return true; });

            SaveProfileCommand = new RelayCommand(
                delegate { SaveProfile(); },
                delegate { return !IsBusy; });

            LoadProfileCommand = new RelayCommand(
                delegate { LoadProfile(); },
                delegate { return !IsBusy; });

            SaveAnimationCommand = new RelayCommand(
                delegate { SaveAnimation(); },
                delegate { return Layers.Any(item => item.Mode == "Animation"); });

            LoadAnimationCommand = new RelayCommand(
                delegate { LoadAnimation(); },
                delegate { return !IsBusy; });

            _deviceTimer = new DispatcherTimer();
            _deviceTimer.Interval = TimeSpan.FromSeconds(2);
            _deviceTimer.Tick += DeviceTimer_Tick;
            _deviceTimer.Start();

            _renderTimer = new DispatcherTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(40);
            _renderTimer.Tick += RenderTimer_Tick;
            _renderTimer.Start();

            RefreshSlotPreviews();
            RefreshDevice();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<LedSlotViewModel> LedSlots { get; private set; }
        public ObservableCollection<KeyboardKeyViewModel> KeyboardKeys { get; private set; }
        public ObservableCollection<LightingLayer> Layers { get; private set; }
        public ObservableCollection<string> AvailableLayerTypes { get; private set; }
        public ObservableCollection<string> BlendModes { get; private set; }
        public ObservableCollection<string> KeyEffectOptions { get; private set; }

        public ICommand RefreshCommand { get; private set; }
        public ICommand SendSceneCommand { get; private set; }
        public ICommand SendPresetCommand { get; private set; }
        public ICommand ApplyHexCommand { get; private set; }
        public ICommand SelectSlotCommand { get; private set; }
        public ICommand ApplySlotColorCommand { get; private set; }
        public ICommand FillAllSlotsCommand { get; private set; }
        public ICommand AddLayerCommand { get; private set; }
        public ICommand AddAnimationLayerCommand { get; private set; }
        public ICommand SelectWorkspaceTabCommand { get; private set; }
        public ICommand RemoveLayerCommand { get; private set; }
        public ICommand MoveLayerUpCommand { get; private set; }
        public ICommand MoveLayerDownCommand { get; private set; }
        public ICommand SetLayerStartColorCommand { get; private set; }
        public ICommand SetLayerEndColorCommand { get; private set; }
        public ICommand AddAnimationFrameCommand { get; private set; }
        public ICommand RemoveAnimationFrameCommand { get; private set; }
        public ICommand ToggleAnimationCommand { get; private set; }
        public ICommand ArmMappingCommand { get; private set; }
        public ICommand ClearMappingCommand { get; private set; }
        public ICommand SaveProfileCommand { get; private set; }
        public ICommand LoadProfileCommand { get; private set; }
        public ICommand SelectKeyboardKeyCommand { get; private set; }
        public ICommand SelectToolCommand { get; private set; }
        public ICommand ToggleThemeCommand { get; private set; }
        public ICommand SaveAnimationCommand { get; private set; }
        public ICommand LoadAnimationCommand { get; private set; }

        public bool IsConnected
        {
            get { return _isConnected; }
            private set { SetField(ref _isConnected, value, "IsConnected"); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set { SetField(ref _isBusy, value, "IsBusy"); }
        }

        public string ConnectionTitle
        {
            get { return _connectionTitle; }
            private set { SetField(ref _connectionTitle, value, "ConnectionTitle"); }
        }

        public string ConnectionMessage
        {
            get { return _connectionMessage; }
            private set { SetField(ref _connectionMessage, value, "ConnectionMessage"); }
        }

        public string OperationMessage
        {
            get { return _operationMessage; }
            private set { SetField(ref _operationMessage, value, "OperationMessage"); }
        }

        public string FooterStatus
        {
            get { return _footerStatus; }
            private set { SetField(ref _footerStatus, value, "FooterStatus"); }
        }

        public string ProfileName
        {
            get { return _profileName; }
            set { SetField(ref _profileName, value, "ProfileName"); }
        }

        public int SelectedWorkspaceTabIndex
        {
            get { return _selectedWorkspaceTabIndex; }
            set
            {
                int next = (int)Clamp(value, 0, 4);
                SetField(ref _selectedWorkspaceTabIndex, next, "SelectedWorkspaceTabIndex");
            }
        }

        public string HexInput
        {
            get { return _hexInput; }
            set { SetField(ref _hexInput, value, "HexInput"); }
        }

        public string EffectiveColorHex
        {
            get { return _effectiveColorHex; }
            private set { SetField(ref _effectiveColorHex, value, "EffectiveColorHex"); }
        }

        public Brush PreviewBrush
        {
            get { return _previewBrush; }
            private set { SetField(ref _previewBrush, value, "PreviewBrush"); }
        }

        public double RedValue
        {
            get { return _redValue; }
            set { SetChannel(ref _redValue, value, "RedValue"); }
        }

        public double GreenValue
        {
            get { return _greenValue; }
            set { SetChannel(ref _greenValue, value, "GreenValue"); }
        }

        public double BlueValue
        {
            get { return _blueValue; }
            set { SetChannel(ref _blueValue, value, "BlueValue"); }
        }

        public double BrightnessValue
        {
            get { return _brightnessValue; }
            set
            {
                double next = Clamp(value, 0, 100);
                if (Math.Abs(_brightnessValue - next) > 0.001)
                {
                    _brightnessValue = next;
                    OnPropertyChanged("BrightnessValue");
                    UpdateColorPresentation(false);
                }
            }
        }

        public LightingLayer SelectedLayer
        {
            get { return _selectedLayer; }
            set
            {
                if (object.ReferenceEquals(_selectedLayer, value)) return;
                SetField(ref _selectedLayer, value, "SelectedLayer");
                OnPropertyChanged("AnimationFrames");
                OnPropertyChanged("AnimationFrameCount");
            }
        }

        public IList<AnimationFrame> AnimationFrames
        {
            get
            {
                return SelectedLayer == null || SelectedLayer.Frames == null
                    ? (IList<AnimationFrame>)new List<AnimationFrame>()
                    : SelectedLayer.Frames;
            }
        }

        public int AnimationFrameCount
        {
            get { return AnimationFrames.Count; }
        }

        public string SelectedLayerType
        {
            get { return _selectedLayerType; }
            set { SetField(ref _selectedLayerType, value, "SelectedLayerType"); }
        }

        public string SelectedKeyEffect
        {
            get { return _selectedKeyEffect; }
            set
            {
                SetField(ref _selectedKeyEffect, value, "SelectedKeyEffect");
                LightingLayer layer = GetKeyReactiveLayer();
                if (layer != null)
                {
                    layer.KeyEffect = value;
                }
            }
        }

        public int SelectedSlotIndex
        {
            get { return _selectedSlotIndex; }
            private set
            {
                if (_selectedSlotIndex == value)
                {
                    return;
                }

                _selectedSlotIndex = value;
                OnPropertyChanged("SelectedSlotIndex");
                OnPropertyChanged("SelectedSlotLabel");
                UpdateSelectedSlotMapping();
                for (int index = 0; index < LedSlots.Count; index++)
                {
                    LedSlots[index].IsSelected = LedSlots[index].SlotIndex == value;
                }
                OnPropertyChanged("SelectedKeyboardKeyStatus");
            }
        }

        public string SelectedSlotLabel
        {
            get { return "LED SLOT " + (_selectedSlotIndex + 1).ToString("000"); }
        }

        public string SelectedSlotMapping
        {
            get { return _selectedSlotMapping; }
            private set { SetField(ref _selectedSlotMapping, value, "SelectedSlotMapping"); }
        }

        public string SelectedTool
        {
            get { return _selectedTool; }
            set
            {
                if (SetFieldValue(ref _selectedTool, value, "SelectedTool"))
                    OperationMessage = "Tool selected: " + value + ".";
            }
        }

        public string SelectedKeyboardKey
        {
            get { return _selectedKeyboardKey; }
            private set
            {
                if (SetFieldValue(ref _selectedKeyboardKey, value, "SelectedKeyboardKey"))
                {
                    OnPropertyChanged("SelectedKeyboardKeyLabel");
                    OnPropertyChanged("SelectedKeyboardKeyStatus");
                    RefreshKeyboardPreviews();
                }
            }
        }

        public string SelectedKeyboardKeyLabel
        {
            get
            {
                KeyboardKeyViewModel key = KeyboardKeys.FirstOrDefault(item => item.KeyId == SelectedKeyboardKey);
                return key == null ? "—" : (string.IsNullOrEmpty(key.Label) ? "SPACE" : key.Label);
            }
        }

        public string SelectedKeyboardKeyStatus
        {
            get
            {
                KeyboardKeyViewModel key = KeyboardKeys.FirstOrDefault(item => item.KeyId == SelectedKeyboardKey);
                KeyLedMapping mapping = key == null ? null : FindMappingForKey(key);
                return mapping == null
                    ? "Not calibrated · choose Map and assign an LED slot"
                    : "Mapped to LED slot " + (mapping.SlotIndex + 1).ToString("000");
            }
        }

        public bool PaintOnPhysicalPress
        {
            get { return _paintOnPhysicalPress; }
            set
            {
                if (SetFieldValue(ref _paintOnPhysicalPress, value, "PaintOnPhysicalPress"))
                    RefreshKeyboardPreviews();
            }
        }

        public bool IsLightTheme
        {
            get { return _isLightTheme; }
            set
            {
                if (SetFieldValue(ref _isLightTheme, value, "IsLightTheme"))
                {
                    OnPropertyChanged("ThemeButtonText");
                    RefreshKeyboardPreviews();
                }
            }
        }

        public string ThemeButtonText
        {
            get { return IsLightTheme ? "☀ LIGHT" : "◐ DARK"; }
        }

        public bool HasSelectedMapping
        {
            get { return FindCustomMappingForSlot(_selectedSlotIndex) != null; }
        }

        public bool IsMappingArmed
        {
            get { return _isMappingArmed; }
            private set
            {
                SetField(ref _isMappingArmed, value, "IsMappingArmed");
                OnPropertyChanged("MappingButtonText");
            }
        }

        public string MappingButtonText
        {
            get { return IsMappingArmed ? "WAITING FOR KEY PRESS..." : "ASSIGN PHYSICAL KEY"; }
        }

        public int MappingsCount
        {
            get { return KeyboardKeys.Count(item => FindMappingForKey(item) != null); }
        }

        public bool AudioReactiveEnabled
        {
            get { return _audioReactiveEnabled; }
            set
            {
                if (_audioReactiveEnabled != value)
                {
                    _audioReactiveEnabled = value;
                    OnPropertyChanged("AudioReactiveEnabled");
                    EnsureLayer("AudioReactive", "Audio reactive");
                    OperationMessage = value
                        ? "Audio-reactive lighting is enabled. It follows the Windows default output peak meter."
                        : "Audio-reactive lighting is paused.";
                }
            }
        }

        public bool KeyReactiveEnabled
        {
            get { return _keyReactiveEnabled; }
            set
            {
                if (_keyReactiveEnabled != value)
                {
                    _keyReactiveEnabled = value;
                    OnPropertyChanged("KeyReactiveEnabled");
                    EnsureLayer("KeyReactive", "Key press response");
                    OperationMessage = value
                        ? "Key response is enabled for mapped keys only."
                        : "Key response is paused.";
                }
            }
        }

        public bool AnimationRunning
        {
            get { return _animationRunning; }
            set
            {
                if (_animationRunning != value)
                {
                    _animationRunning = value;
                    OnPropertyChanged("AnimationRunning");
                    OnPropertyChanged("AnimationButtonText");
                    FooterStatus = value ? "Animation playing" : "Animation stopped";
                }
            }
        }

        public string AnimationButtonText
        {
            get { return AnimationRunning ? "STOP ANIMATION" : "PLAY ANIMATION"; }
        }

        public double AudioPeakLevel
        {
            get { return _audioPeakLevel; }
            private set { SetField(ref _audioPeakLevel, value, "AudioPeakLevel"); }
        }

        public string AudioStatus
        {
            get { return _audioStatus; }
            private set { SetField(ref _audioStatus, value, "AudioStatus"); }
        }

        public string LastDetectedKey
        {
            get { return _lastDetectedKey; }
            private set { SetField(ref _lastDetectedKey, value, "LastDetectedKey"); }
        }

        private readonly List<KeyLedMapping> KeyMappings = new List<KeyLedMapping>();

        private void SetChannel(ref double field, double value, string propertyName)
        {
            double next = Clamp(value, 0, 255);
            if (Math.Abs(field - next) > 0.001)
            {
                field = next;
                OnPropertyChanged(propertyName);
                UpdateColorPresentation(true);
            }
        }

        private void DeviceTimer_Tick(object sender, EventArgs e)
        {
            if (!IsBusy)
            {
                RefreshDevice();
            }
        }

        private void RenderTimer_Tick(object sender, EventArgs e)
        {
            if (!IsConnected)
            {
                return;
            }

            bool needsRender = AnimationRunning;

            if (AudioReactiveEnabled && _audioMeter != null)
            {
                float peak;
                if (_audioMeter.TryGetPeakLevel(out peak))
                {
                    AudioPeakLevel = peak * 100.0;
                    AudioStatus = "Default Windows output · peak " + AudioPeakLevel.ToString("F0") + "%";

                    if (_lastAudioSentLevel < 0.0 ||
                        Math.Abs(AudioPeakLevel - _lastAudioSentLevel) >= 1.0)
                    {
                        needsRender = true;
                        _lastAudioSentLevel = AudioPeakLevel;
                    }
                }
                else
                {
                    AudioStatus = "Audio endpoint meter is unavailable.";
                    AudioPeakLevel = 0;
                }
            }

            LightingLayer keyLayer = GetKeyReactiveLayer();
            bool keyFadeActive = KeyReactiveEnabled &&
                                 _keyIntensity.Count > 0 &&
                                 (keyLayer == null || keyLayer.KeyEffect != "HoldGlow");

            if (keyFadeActive)
            {
                UpdateKeyDecay();
                needsRender = true;
            }

            if (needsRender)
            {
                SendCurrentFrame(false);
            }
        }

        private void UpdateKeyDecay()
        {
            LightingLayer keyLayer = GetKeyReactiveLayer();
            string effect = keyLayer == null ? "Fade" : keyLayer.KeyEffect;

            if (effect == "HoldGlow")
            {
                foreach (int slot in _heldSlots)
                {
                    _keyIntensity[slot] = 1.0;
                }

                List<int> clear = _keyIntensity.Keys.Where(slot => !_heldSlots.Contains(slot)).ToList();
                for (int index = 0; index < clear.Count; index++)
                {
                    _keyIntensity.Remove(clear[index]);
                }

                return;
            }

            List<int> keys = _keyIntensity.Keys.ToList();
            for (int index = 0; index < keys.Count; index++)
            {
                int slot = keys[index];
                double next = _keyIntensity[slot] - (effect == "Flash" ? 0.25 : 0.06);
                if (next <= 0.0)
                {
                    _keyIntensity.Remove(slot);
                }
                else
                {
                    _keyIntensity[slot] = next;
                }
            }
        }

        private void RefreshDevice()
        {
            try
            {
                DeviceConnectionInfo info = _deviceController.RefreshConnection();
                IsConnected = info.IsConnected;
                ConnectionTitle = info.Title;
                ConnectionMessage = info.Message;
                FooterStatus = info.IsConnected ? "Device ready" : "Device unavailable";

                if (!info.IsConnected)
                {
                    OperationMessage = "Lighting controls are unavailable until the target HID interface is connected.";
                }
            }
            catch (Exception exception)
            {
                IsConnected = false;
                ConnectionTitle = "Device check failed";
                ConnectionMessage = exception.Message;
                FooterStatus = "Device check error";
            }
        }

        private void SendCurrentFrame(bool showResult)
        {
            if (!IsConnected || _disposed)
            {
                return;
            }

            byte[] frame = RenderCurrentFrame();
            if (_isSendingFrame)
            {
                // Keep only the newest complete scene. This prevents rapid key presses
                // or slider changes from being silently lost while a HID write is active.
                _pendingFrame = frame;
                _pendingFrameShowResult = _pendingFrameShowResult || showResult;
                return;
            }

            StartFrameSend(frame, showResult);
        }

        private void StartFrameSend(byte[] frame, bool showResult)
        {
            _isSendingFrame = true;
            if (showResult)
            {
                IsBusy = true;
                FooterStatus = "Sending RGB frame";
            }

            SendFrameAsync(frame, Stopwatch.StartNew(), showResult);
        }

        private async void SendFrameAsync(byte[] frame, Stopwatch stopwatch, bool showResult)
        {
            try
            {
                await _deviceController.SendRgbFrameAsync(frame, CancellationToken.None);
                stopwatch.Stop();
                if (showResult)
                {
                    OperationMessage = "Applied " + EffectiveColorHex + " in " +
                                       stopwatch.ElapsedMilliseconds + " ms · one 382-byte HID report.";
                    FooterStatus = "Lighting updated";
                }
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                if (showResult || !AnimationRunning)
                {
                    OperationMessage = "RGB update failed: " + exception.Message;
                    FooterStatus = "RGB update error";
                }
            }
            finally
            {
                _isSendingFrame = false;
                if (showResult)
                {
                    IsBusy = false;
                }

                byte[] nextFrame = _pendingFrame;
                bool nextShowResult = _pendingFrameShowResult;
                _pendingFrame = null;
                _pendingFrameShowResult = false;

                if (nextFrame != null && !_disposed && IsConnected)
                {
                    StartFrameSend(nextFrame, nextShowResult);
                }
            }
        }

        private byte[] RenderCurrentFrame()
        {
            return RenderCurrentFrame(null);
        }

        private byte[] RenderCurrentFrame(LightingLayer excludedLayer)
        {
            var renderLayers = new List<LightingLayer>();
            for (int index = 0; index < Layers.Count; index++)
            {
                LightingLayer layer = Layers[index];
                if (layer == null || !layer.IsEnabled ||
                    object.ReferenceEquals(layer, excludedLayer))
                {
                    continue;
                }

                if (layer.Mode == "AudioReactive" && !AudioReactiveEnabled)
                {
                    continue;
                }

                if (layer.Mode == "KeyReactive" && !KeyReactiveEnabled)
                {
                    continue;
                }

                renderLayers.Add(layer);
            }

            double elapsed = _clock.Elapsed.TotalMilliseconds;
            return LayerCompositor.Render(
                renderLayers,
                AudioPeakLevel / 100.0,
                _keyIntensity,
                elapsed);
        }

        private void ApplyPreset(string hex)
        {
            byte red;
            byte green;
            byte blue;
            if (!TryParseColor(hex, out red, out green, out blue))
            {
                OperationMessage = "Invalid preset colour.";
                return;
            }

            RedValue = red;
            GreenValue = green;
            BlueValue = blue;
            ApplyCurrentColorToPreview();
            ApplyCurrentColorToSelectedKeyAndSend();
        }

        private void ApplyHexColor()
        {
            byte red;
            byte green;
            byte blue;
            if (!TryParseColor(HexInput, out red, out green, out blue))
            {
                OperationMessage = "Enter a six-digit HEX colour, for example FF2020.";
                return;
            }

            RedValue = red;
            GreenValue = green;
            BlueValue = blue;
            ApplyCurrentColorToPreview();
            ApplyCurrentColorToSelectedKeyAndSend();
        }

        private void ApplyCurrentColorToSelectedKeyAndSend()
        {
            KeyboardKeyViewModel key = KeyboardKeys.FirstOrDefault(
                item => item.KeyId == SelectedKeyboardKey);
            KeyLedMapping mapping = FindMappingForKey(key);
            if (mapping == null)
            {
                OperationMessage = "The selected key has no measured LED address. Choose a mapped key or calibrate it manually.";
                return;
            }

            SelectedSlotIndex = mapping.SlotIndex;
            PaintSlotColor(mapping.SlotIndex, GetCurrentColor());
            OperationMessage = "Applied " + EffectiveColorHex + " to " + key.Label +
                               " · LED slot " + (mapping.SlotIndex + 1).ToString("000") + ".";
            SendCurrentFrame(true);
        }

        private void SelectSlot(object parameter)
        {
            int slot;
            if (parameter == null || !Int32.TryParse(parameter.ToString(), out slot))
            {
                return;
            }

            if (slot < 0 || slot >= LedSlots.Count)
            {
                return;
            }

            SelectedSlotIndex = slot;
        }

        private void SelectKeyboardKey(string keyId)
        {
            KeyboardKeyViewModel key = KeyboardKeys.FirstOrDefault(item => item.KeyId == keyId);
            if (key == null) return;
            SelectedKeyboardKey = keyId;

            if (SelectedTool == "Map")
            {
                MapKeyboardKeyToSelectedSlot(key);
                return;
            }

            KeyLedMapping mapping = FindMappingForKey(key);
            if (mapping == null)
            {
                OperationMessage = key.Label + " is not calibrated to a protocol LED slot. Select a slot in Calibration, then use Map.";
                return;
            }

            SelectedSlotIndex = mapping.SlotIndex;
            if (SelectedTool == "Brush")
            {
                PaintSlotColor(mapping.SlotIndex, GetCurrentColor());
                SendCurrentFrame(true);
            }
            else if (SelectedTool == "Eraser")
            {
                PaintSlotColor(mapping.SlotIndex, new RgbColor(0, 0, 0));
                SendCurrentFrame(true);
            }
            else if (SelectedTool == "Fill")
            {
                FillAllSlots();
                SendCurrentFrame(true);
            }
            else if (SelectedTool == "Picker")
            {
                PickColorFromSlot(mapping.SlotIndex);
            }
        }

        private void MapKeyboardKeyToSelectedSlot(KeyboardKeyViewModel key)
        {
            if (key == null)
            {
                OperationMessage = "Select a physical key on the map first.";
                return;
            }

            // One physical key must point to one LED slot. Remove any previous custom
            // assignment for that same physical key before adding the new one.
            KeyLedMapping previousForKey = KeyMappings.FirstOrDefault(item =>
                item.VirtualKey == key.VirtualKeyCode &&
                item.IsExtended == key.IsExtendedHint &&
                (item.ScanCode == 0 || key.ScanCodeHint == 0 || item.ScanCode == key.ScanCodeHint));

            KeyLedMapping mapping = FindCustomMappingForSlot(_selectedSlotIndex);
            if (mapping == null)
            {
                mapping = new KeyLedMapping();
                mapping.SlotIndex = _selectedSlotIndex;
                KeyMappings.Add(mapping);
            }

            if (previousForKey != null && !object.ReferenceEquals(previousForKey, mapping))
                KeyMappings.Remove(previousForKey);

            mapping.VirtualKey = key.VirtualKeyCode;
            mapping.ScanCode = key.ScanCodeHint;
            mapping.KeyName = key.Label;
            mapping.IsExtended = key.IsExtendedHint;
            OnPropertyChanged("MappingsCount");
            UpdateSelectedSlotMapping();
            RefreshSlotPreviews();
            OperationMessage = key.Label + " is assigned to LED slot " + (_selectedSlotIndex + 1) + ".";
        }

        private KeyLedMapping FindCustomMappingForSlot(int slot)
        {
            return KeyMappings.FirstOrDefault(item => item.SlotIndex == slot);
        }

        private KeyLedMapping FindMappingForKey(KeyboardKeyViewModel key)
        {
            if (key == null) return null;

            KeyLedMapping custom = KeyMappings.FirstOrDefault(item =>
                item.VirtualKey == key.VirtualKeyCode &&
                item.IsExtended == key.IsExtendedHint &&
                (item.ScanCode == 0 || key.ScanCodeHint == 0 || item.ScanCode == key.ScanCodeHint));
            if (custom != null) return custom;

            int measuredSlot;
            if (!VeigarK643LedMap.TryGetSlot(key.KeyId, out measuredSlot)) return null;

            return new KeyLedMapping
            {
                VirtualKey = key.VirtualKeyCode,
                ScanCode = key.ScanCodeHint,
                IsExtended = key.IsExtendedHint,
                SlotIndex = measuredSlot,
                KeyName = key.Label
            };
        }

        private void PaintSlotColor(int slot, RgbColor color)
        {
            if (slot < 0 || slot >= 126 || color == null) return;
            LightingLayer layer = GetBaseLayer();
            LedSlotColor existing = FindSlotColor(layer.SlotColors, slot);
            if (existing == null)
            {
                layer.SlotColors.Add(new LedSlotColor { SlotIndex = slot, Color = color });
            }
            else
            {
                existing.Color = color;
            }
            RefreshSlotPreviews();
        }

        private void PickColorFromSlot(int slot)
        {
            byte[] rendered = RenderCurrentFrame();
            int offset = slot * 3;
            RedValue = rendered[offset];
            GreenValue = rendered[offset + 1];
            BlueValue = rendered[offset + 2];
            BrightnessValue = 100;
            UpdateColorPresentation(true);
            OperationMessage = "Picked colour from " + SelectedKeyboardKeyLabel + ".";
        }

        private void RefreshKeyboardPreviews()
        {
            if (KeyboardKeys == null) return;

            Brush pressedBrush = new SolidColorBrush(Color.FromArgb(235, 47, 190, 177));
            Brush selectedBorder = new SolidColorBrush(Color.FromRgb(232, 87, 98));
            Brush normalBorder = new SolidColorBrush(IsLightTheme
                ? Color.FromRgb(117, 132, 148)
                : Color.FromRgb(93, 107, 123));
            Brush defaultKeyBrush = new SolidColorBrush(IsLightTheme
                ? Color.FromRgb(248, 250, 252)
                : Color.FromRgb(57, 67, 80));

            foreach (KeyboardKeyViewModel key in KeyboardKeys)
            {
                KeyLedMapping mapping = FindMappingForKey(key);
                key.MappingName = mapping == null
                    ? key.Label + " · no measured LED address"
                    : key.Label + " · LED slot " + (mapping.SlotIndex + 1).ToString("000");
                key.IsSelected = key.KeyId == SelectedKeyboardKey;
                key.IsPhysicallyPressed = _pressedKeyboardKeys.Contains(key.KeyId);

                if (key.IsPhysicallyPressed)
                {
                    if (PaintOnPhysicalPress)
                    {
                        key.Brush = new SolidColorBrush(Color.FromArgb(
                            245, ToByte(RedValue), ToByte(GreenValue), ToByte(BlueValue)));
                    }
                    else
                    {
                        key.Brush = pressedBrush;
                    }
                }
                else if (mapping != null && mapping.SlotIndex >= 0 && mapping.SlotIndex < LedSlots.Count)
                {
                    SolidColorBrush slotBrush = LedSlots[mapping.SlotIndex].Brush as SolidColorBrush;
                    Color colour = slotBrush == null ? Colors.Black : slotBrush.Color;
                    bool isOff = colour.R == 0 && colour.G == 0 && colour.B == 0;
                    key.Brush = isOff
                        ? defaultKeyBrush
                        : new SolidColorBrush(Color.FromArgb(220, colour.R, colour.G, colour.B));
                }
                else
                {
                    key.Brush = defaultKeyBrush;
                }

                key.BorderBrush = key.IsSelected ? selectedBorder : normalBorder;
            }
        }

        private static bool SetFieldValue<T>(ref T field, T value, string propertyName, Action<string> notify)
        {
            if (object.Equals(field, value)) return false;
            field = value;
            if (notify != null) notify(propertyName);
            return true;
        }

        private bool SetFieldValue<T>(ref T field, T value, string propertyName)
        {
            if (object.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void ApplyColorToSelectedSlot()
        {
            LightingLayer layer = GetBaseLayer();
            LedSlotColor existing = FindSlotColor(layer.SlotColors, _selectedSlotIndex);
            var color = GetCurrentColor();

            if (existing == null)
            {
                layer.SlotColors.Add(new LedSlotColor
                {
                    SlotIndex = _selectedSlotIndex,
                    Color = color
                });
            }
            else
            {
                existing.Color = color;
            }

            RefreshSlotPreviews();
            OperationMessage = "Colour assigned to LED slot " + (_selectedSlotIndex + 1) +
                               ". Press SEND SCENE to update the keyboard.";
        }

        private void FillAllSlots()
        {
            LightingLayer layer = GetBaseLayer();
            layer.StartColor = GetCurrentColor();
            layer.SlotColors.Clear();
            RefreshSlotPreviews();
            OperationMessage = "Base colour assigned to all 126 LED slots. The complete scene preview is updated.";
        }

        private LightingLayer GetBaseLayer()
        {
            LightingLayer layer = Layers.FirstOrDefault(item => item.Mode == "Static");
            if (layer == null)
            {
                layer = new LightingLayer();
                layer.Name = "Base colour";
                layer.Mode = "Static";
                Layers.Insert(0, layer);
            }

            return layer;
        }

        private void SelectWorkspaceTab(object parameter)
        {
            int index;
            if (parameter == null || !Int32.TryParse(parameter.ToString(), out index))
                return;
            SelectedWorkspaceTabIndex = index;
        }

        private void AddAnimationLayer()
        {
            SelectedLayerType = "Animation";
            AddLayer();
        }

        private void AddLayer()
        {
            string mode = SelectedLayerType;
            var layer = new LightingLayer();
            layer.Mode = mode;
            layer.Name = mode + " layer " + (Layers.Count + 1);
            layer.StartColor = GetCurrentColor();
            layer.EndColor = new RgbColor(32, 80, 255);

            if (mode == "Animation")
            {
                var frame = new AnimationFrame();
                frame.BaseColor = GetCurrentColor();
                layer.Frames.Add(frame);
            }

            Layers.Add(layer);
            SelectedLayer = layer;
            if (mode == "AudioReactive") AudioReactiveEnabled = true;
            if (mode == "KeyReactive") KeyReactiveEnabled = true;
            RefreshSlotPreviews();
            OperationMessage = mode + " layer added to the lighting stack.";
        }

        private void RemoveSelectedLayer()
        {
            if (SelectedLayer == null || Layers.Count <= 1)
            {
                return;
            }

            int index = Layers.IndexOf(SelectedLayer);
            Layers.Remove(SelectedLayer);
            SelectedLayer = Layers[Math.Max(0, Math.Min(index, Layers.Count - 1))];
            RefreshSlotPreviews();
        }

        private void MoveLayer(int offset)
        {
            if (SelectedLayer == null)
            {
                return;
            }

            int index = Layers.IndexOf(SelectedLayer);
            int destination = index + offset;
            if (destination < 0 || destination >= Layers.Count)
            {
                return;
            }

            Layers.Move(index, destination);
            RefreshSlotPreviews();
        }

        private void SetSelectedLayerColor(bool endColor)
        {
            if (SelectedLayer == null)
            {
                return;
            }

            if (endColor)
            {
                SelectedLayer.EndColor = GetCurrentColor();
            }
            else
            {
                SelectedLayer.StartColor = GetCurrentColor();
            }

            RefreshSlotPreviews();
        }

        private void AddAnimationFrame()
        {
            if (SelectedLayer == null || SelectedLayer.Mode != "Animation")
            {
                return;
            }

            // Capture the lighting layers underneath this animation layer. Capturing
            // RenderCurrentFrame() would include the old animation output and mask edits
            // made to the base colour while creating the next frame.
            byte[] rendered = RenderCurrentFrame(SelectedLayer);
            var frame = new AnimationFrame();
            frame.BaseColor = GetCurrentColor();

            for (int slot = 0; slot < 126; slot++)
            {
                int offset = slot * 3;
                frame.SlotColors.Add(new LedSlotColor
                {
                    SlotIndex = slot,
                    Color = new RgbColor(rendered[offset], rendered[offset + 1], rendered[offset + 2])
                });
            }

            SelectedLayer.Frames.Add(frame);
            OnPropertyChanged("AnimationFrames");
            OnPropertyChanged("AnimationFrameCount");
            OperationMessage = "Captured animation frame " + SelectedLayer.Frames.Count +
                               " from the layers below the animation layer.";
        }

        private void RemoveAnimationFrame()
        {
            if (SelectedLayer != null && SelectedLayer.Mode == "Animation" &&
                SelectedLayer.Frames != null && SelectedLayer.Frames.Count > 0)
            {
                SelectedLayer.Frames.RemoveAt(SelectedLayer.Frames.Count - 1);
                OnPropertyChanged("AnimationFrames");
                OnPropertyChanged("AnimationFrameCount");
                OperationMessage = "Last animation frame removed.";
            }
        }

        private void EnsureLayer(string mode, string name)
        {
            if (!Layers.Any(item => item.Mode == mode))
            {
                var layer = new LightingLayer();
                layer.Mode = mode;
                layer.Name = name;
                layer.StartColor = GetCurrentColor();
                Layers.Add(layer);
            }

            RefreshSlotPreviews();
        }

        private LightingLayer GetKeyReactiveLayer()
        {
            return Layers.FirstOrDefault(item => item.Mode == "KeyReactive");
        }

        public void OnRawInputStatus(string message)
        {
            FooterStatus = "Raw Input unavailable";
            OperationMessage = message ?? "Raw Input could not be registered.";
        }

        public void OnPhysicalKeyReceived(RawKeyEventArgs args)
        {
            if (args == null) return;

            string keyName;
            try { keyName = System.Windows.Input.KeyInterop.KeyFromVirtualKey(args.VirtualKey).ToString(); }
            catch { keyName = "VK " + args.VirtualKey; }

            string keyId = KeyboardLayoutBuilder.KeyIdFromInput(args.VirtualKey, args.ScanCode, args.IsExtended);
            KeyboardKeyViewModel keyboardKey = KeyboardKeys.FirstOrDefault(item => item.KeyId == keyId);
            if (keyboardKey != null)
            {
                if (args.IsKeyDown)
                {
                    _pressedKeyboardKeys.Add(keyId);
                    SelectedKeyboardKey = keyId;
                }
                else
                {
                    _pressedKeyboardKeys.Remove(keyId);
                }
                RefreshKeyboardPreviews();
            }

            LastDetectedKey = (keyboardKey == null ? keyName : keyboardKey.Label) + " · scan " + args.ScanCode;

            if (IsMappingArmed && args.IsKeyDown)
            {
                // Find only an explicit custom mapping here. FindMappingForSlot() can
                // return a temporary object from the built-in measured table.
                KeyLedMapping existing = FindCustomMappingForSlot(_selectedSlotIndex);
                KeyLedMapping previousForKey = KeyMappings.FirstOrDefault(item =>
                    item.VirtualKey == args.VirtualKey &&
                    item.IsExtended == args.IsExtended &&
                    (item.ScanCode == args.ScanCode || item.ScanCode == 0));

                if (existing == null)
                {
                    existing = new KeyLedMapping();
                    existing.SlotIndex = _selectedSlotIndex;
                    KeyMappings.Add(existing);
                }

                if (previousForKey != null && !object.ReferenceEquals(previousForKey, existing))
                    KeyMappings.Remove(previousForKey);

                existing.VirtualKey = args.VirtualKey;
                existing.ScanCode = args.ScanCode;
                existing.IsExtended = args.IsExtended;
                existing.KeyName = keyboardKey == null ? keyName : keyboardKey.Label;
                IsMappingArmed = false;
                OnPropertyChanged("MappingsCount");
                UpdateSelectedSlotMapping();
                RefreshSlotPreviews();
                OperationMessage = existing.KeyName + " mapped to LED slot " + (_selectedSlotIndex + 1) + ".";
                return;
            }

            KeyLedMapping mapping = KeyMappings.FirstOrDefault(
                item => item.VirtualKey == args.VirtualKey &&
                        item.IsExtended == args.IsExtended &&
                        (item.ScanCode == args.ScanCode || item.ScanCode == 0));

            if (mapping == null && keyboardKey != null)
            {
                mapping = FindMappingForKey(keyboardKey);
            }

            if (mapping == null)
            {
                if (args.IsKeyDown && keyboardKey != null && PaintOnPhysicalPress)
                    OperationMessage = keyboardKey.Label + " detected. No measured LED address exists for this key; it can be calibrated manually.";
                return;
            }

            if (args.IsKeyDown)
            {
                _heldSlots.Add(mapping.SlotIndex);
                _keyIntensity[mapping.SlotIndex] = 1.0;

                if (PaintOnPhysicalPress && (SelectedTool == "Brush" || SelectedTool == "Eraser"))
                {
                    RgbColor color = SelectedTool == "Eraser" ? new RgbColor(0, 0, 0) : GetCurrentColor();
                    PaintSlotColor(mapping.SlotIndex, color);
                    SendCurrentFrame(false);
                    OperationMessage = "Painted " + mapping.KeyName + " on the keyboard.";
                    return;
                }

                if (KeyReactiveEnabled) SendCurrentFrame(false);
            }
            else
            {
                _heldSlots.Remove(mapping.SlotIndex);
                LightingLayer layer = GetKeyReactiveLayer();
                if (layer != null && layer.KeyEffect == "HoldGlow")
                {
                    _keyIntensity.Remove(mapping.SlotIndex);
                    if (KeyReactiveEnabled) SendCurrentFrame(false);
                }
            }
        }

        private void ClearSelectedMapping()
        {
            KeyLedMapping existing = FindCustomMappingForSlot(_selectedSlotIndex);
            if (existing != null)
            {
                KeyMappings.Remove(existing);
                OnPropertyChanged("MappingsCount");
                UpdateSelectedSlotMapping();
                RefreshSlotPreviews();
            }
            else
            {
                OperationMessage = "The measured factory map is read-only. Assign another key to this slot to override it.";
            }
        }

        private KeyLedMapping FindMappingForSlot(int slot)
        {
            KeyLedMapping custom = FindCustomMappingForSlot(slot);
            if (custom != null) return custom;

            string keyId;
            if (!VeigarK643LedMap.TryGetKeyId(slot, out keyId)) return null;

            KeyboardKeyViewModel key = KeyboardKeys.FirstOrDefault(item => item.KeyId == keyId);
            if (key == null) return null;

            bool keyWasOverridden = KeyMappings.Any(item =>
                item.VirtualKey == key.VirtualKeyCode &&
                item.IsExtended == key.IsExtendedHint &&
                item.SlotIndex != slot);
            if (keyWasOverridden) return null;

            return new KeyLedMapping
            {
                VirtualKey = key.VirtualKeyCode,
                ScanCode = key.ScanCodeHint,
                IsExtended = key.IsExtendedHint,
                SlotIndex = slot,
                KeyName = key.Label
            };
        }

        private static LedSlotColor FindSlotColor(IList<LedSlotColor> colors, int slot)
        {
            if (colors == null)
            {
                return null;
            }

            return colors.FirstOrDefault(item => item != null && item.SlotIndex == slot);
        }

        private void UpdateSelectedSlotMapping()
        {
            KeyLedMapping mapping = FindMappingForSlot(_selectedSlotIndex);
            SelectedSlotMapping = mapping == null
                ? "No physical key is recorded for this LED slot."
                : (FindCustomMappingForSlot(_selectedSlotIndex) == null ? "Measured key: " : "Custom key: ") +
                  mapping.KeyName + " · LED " + (_selectedSlotIndex + 1).ToString("000") +
                  " · VK " + mapping.VirtualKey + " · scan " + mapping.ScanCode;
            OnPropertyChanged("HasSelectedMapping");
        }

        private void RefreshSlotPreviews()
        {
            byte[] rendered = RenderCurrentFrame();
            for (int slot = 0; slot < LedSlots.Count; slot++)
            {
                int offset = slot * 3;
                LedSlots[slot].Brush = new SolidColorBrush(Color.FromRgb(
                    rendered[offset],
                    rendered[offset + 1],
                    rendered[offset + 2]));

                KeyLedMapping mapping = FindMappingForSlot(slot);
                LedSlots[slot].MappingName = mapping == null ? string.Empty : mapping.KeyName;
            }
            RefreshKeyboardPreviews();
        }

        private void SaveProfile()
        {
            var dialog = new SaveFileDialog();
            dialog.Title = "Save K643 lighting profile";
            dialog.Filter = "Veigar Configurator Paint (*.vcp)|*.vcp|Legacy JSON profile (*.json)|*.json";
            dialog.DefaultExt = ".vcp";
            dialog.AddExtension = true;
            dialog.FileName = MakeSafeFileName(ProfileName) + ".vcp";

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var profile = new LightingProfile
                {
                    Name = string.IsNullOrWhiteSpace(ProfileName) ? "My K643 lighting profile" : ProfileName.Trim(),
                    Red = ToByte(RedValue),
                    Green = ToByte(GreenValue),
                    Blue = ToByte(BlueValue),
                    Brightness = (int)Math.Round(BrightnessValue),
                    Layers = Layers.ToList(),
                    KeyMappings = KeyMappings.ToList()
                };

                LightingProfileStore.Save(dialog.FileName, profile);
                OperationMessage = "Profile saved locally: " + dialog.FileName;
                FooterStatus = "Profile saved";
            }
            catch (Exception exception)
            {
                OperationMessage = "Could not save profile: " + exception.Message;
                FooterStatus = "Profile save error";
            }
        }

        private void LoadProfile()
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Load K643 lighting profile";
            dialog.Filter = "Veigar Configurator Paint (*.vcp)|*.vcp|Legacy JSON profile (*.json)|*.json|All files (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                LightingProfile profile = LightingProfileStore.Load(dialog.FileName);
                ProfileName = profile.Name ?? "Loaded K643 profile";
                RedValue = Clamp(profile.Red, 0, 255);
                GreenValue = Clamp(profile.Green, 0, 255);
                BlueValue = Clamp(profile.Blue, 0, 255);
                BrightnessValue = Clamp(profile.Brightness, 0, 100);

                Layers.Clear();
                for (int index = 0; index < profile.Layers.Count; index++)
                {
                    Layers.Add(profile.Layers[index]);
                }

                KeyMappings.Clear();
                for (int index = 0; index < profile.KeyMappings.Count; index++)
                {
                    KeyMappings.Add(profile.KeyMappings[index]);
                }

                if (Layers.Count == 0)
                {
                    GetBaseLayer();
                }

                SelectedLayer = Layers[0];
                OnPropertyChanged("MappingsCount");
                RefreshSlotPreviews();
                OperationMessage = "Profile loaded. Press SEND SCENE to apply it to the keyboard.";
                FooterStatus = "Profile loaded";
            }
            catch (Exception exception)
            {
                OperationMessage = "Could not load profile: " + exception.Message;
                FooterStatus = "Profile load error";
            }
        }

        private void SaveAnimation()
        {
            var dialog = new SaveFileDialog();
            dialog.Title = "Save Veigar animation";
            dialog.Filter = "Veigar Configurator Animation (*.vca)|*.vca";
            dialog.DefaultExt = ".vca";
            dialog.AddExtension = true;
            dialog.FileName = MakeSafeFileName(ProfileName) + ".vca";
            if (dialog.ShowDialog() != true) return;

            try
            {
                LightingProfileStore.SaveAnimation(dialog.FileName, ProfileName, Layers.ToList());
                OperationMessage = "Animation project saved: " + dialog.FileName;
                FooterStatus = "Animation saved";
            }
            catch (Exception exception)
            {
                OperationMessage = "Could not save animation: " + exception.Message;
                FooterStatus = "Animation save error";
            }
        }

        private void LoadAnimation()
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Load Veigar animation";
            dialog.Filter = "Veigar Configurator Animation (*.vca)|*.vca";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;
            if (dialog.ShowDialog() != true) return;

            try
            {
                IList<LightingLayer> imported = LightingProfileStore.LoadAnimation(dialog.FileName);
                foreach (LightingLayer layer in imported)
                {
                    Layers.Add(layer);
                }
                LightingLayer first = imported.FirstOrDefault();
                if (first != null) SelectedLayer = first;
                RefreshSlotPreviews();
                OperationMessage = "Loaded " + imported.Count + " animation layer(s).";
                FooterStatus = "Animation loaded";
            }
            catch (Exception exception)
            {
                OperationMessage = "Could not load animation: " + exception.Message;
                FooterStatus = "Animation load error";
            }
        }

        private void UpdateColorPresentation(bool updateHexInput)
        {
            byte red = ToByte(RedValue);
            byte green = ToByte(GreenValue);
            byte blue = ToByte(BlueValue);

            if (updateHexInput)
            {
                HexInput = string.Format("{0:X2}{1:X2}{2:X2}", red, green, blue);
            }

            double scale = BrightnessValue / 100.0;
            byte outputRed = (byte)Math.Round(red * scale);
            byte outputGreen = (byte)Math.Round(green * scale);
            byte outputBlue = (byte)Math.Round(blue * scale);

            PreviewBrush = new SolidColorBrush(Color.FromRgb(outputRed, outputGreen, outputBlue));
            EffectiveColorHex = string.Format("#{0:X2}{1:X2}{2:X2}", outputRed, outputGreen, outputBlue);
            RefreshKeyboardPreviews();
        }

        private void ApplyCurrentColorToPreview()
        {
            UpdateColorPresentation(true);
            RefreshSlotPreviews();
        }

        private RgbColor GetCurrentColor()
        {
            double scale = BrightnessValue / 100.0;
            return new RgbColor(
                (int)Math.Round(ToByte(RedValue) * scale),
                (int)Math.Round(ToByte(GreenValue) * scale),
                (int)Math.Round(ToByte(BlueValue) * scale));
        }

        private static bool TryParseColor(string value, out byte red, out byte green, out byte blue)
        {
            red = 0;
            green = 0;
            blue = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().TrimStart('#');
            if (normalized.Length != 6)
            {
                return false;
            }

            try
            {
                red = Convert.ToByte(normalized.Substring(0, 2), 16);
                green = Convert.ToByte(normalized.Substring(2, 2), 16);
                blue = Convert.ToByte(normalized.Substring(4, 2), 16);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Round(Clamp(value, 0, 255));
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static string MakeSafeFileName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "Veigar-K643-profile" : value.Trim();
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '-');
            }

            return name;
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (object.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }

            CommandManager.InvalidateRequerySuggested();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _deviceTimer.Stop();
            _renderTimer.Stop();
            _deviceTimer.Tick -= DeviceTimer_Tick;
            _renderTimer.Tick -= RenderTimer_Tick;

            if (_audioMeter != null)
            {
                _audioMeter.Dispose();
            }

            _deviceController.Dispose();
        }
    }
}
