using System.Xml.Serialization;

namespace Profiler.Database
{
    public sealed class DbProfilerConfig
    {
        [XmlElement("DbFactionGridProfiler.FactionTag")]
        public string FactionTag { get; set; } = "MME";
    }
}