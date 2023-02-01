using System.Xml.Serialization;
using Torch;
using Utils.Torch;

namespace Profiler
{
    public sealed class ProfilerConfig : ViewModel, FileLoggingConfigurator.IConfig
    {
        public const string DefaultLogFilePath = "Logs/TorchMoreModuli-${shortdate}.log";

        bool _enabled;
        bool _suppressWpfOutput;
        bool _enableLoggingTrace;
        bool _enableLoggingDebug;
        string _logFilePath;
        bool _silenceInvalidPatch;

        public static ProfilerConfig Instance { get; set; }

        public static ProfilerConfig Default { get; } = new()
        {
            Enabled = true,
            SilenceInvalidPatch = false,
            LogFilePath = DefaultLogFilePath,
        };

        [XmlElement]
        public bool Enabled
        {
            get => _enabled;
            set => SetValue(ref _enabled, value);
        }

        [XmlElement]
        public bool SilenceInvalidPatch
        {
            get => _silenceInvalidPatch;
            set => SetValue(ref _silenceInvalidPatch, value);
        }

        // logging stuff

        [XmlElement]
        public bool SuppressWpfOutput
        {
            get => _suppressWpfOutput;
            set => SetValue(ref _suppressWpfOutput, value);
        }

        [XmlElement]
        public bool EnableLoggingTrace
        {
            get => _enableLoggingTrace;
            set => SetValue(ref _enableLoggingTrace, value);
        }

        [XmlElement]
        public bool EnableLoggingDebug
        {
            get => _enableLoggingDebug;
            set => SetValue(ref _enableLoggingDebug, value);
        }

        [XmlElement]
        public string LogFilePath
        {
            get => _logFilePath;
            set => SetValue(ref _logFilePath, value);
        }
    }
}