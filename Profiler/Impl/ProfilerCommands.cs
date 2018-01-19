using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch;
using Torch.Commands;
using Torch.API.Managers;

namespace Profiler.Impl
{
    [Category("profiler")]
    public class ProfilerCommands : CommandModule
    {
        [Command("dump", "Dumps the profiler data into the default dump file.")]
        public void Dump() => DumpTo("profiler_dump.xml");

        [Command("dump to", "Dumps the profiler data into the specified file.", "Dumps the profiler data into the specified file.\nExample: !profiler dump to myfile.dmp")]
        public void DumpTo(string filename)
        {
            TorchBase.Instance?.Managers.GetManager<ProfilerManager>()?.DumpToFile(System.IO.Path.Combine(TorchBase.Instance.Config.InstancePath, filename));
            Context.Respond($"Dump saved to {filename}.");
        }

        [Command("top", "List the top N entities by usage time", "List the top N entities by usage time\nReturns only leaf nodes, ie. entities with no child entities.\nExample: !profiler top 10")]
        public void Top(int n)
        {
            Context.Respond(String.Join("\n", ProfilerData.GetTopEntityUpdateTimes().Take(n).Select(x => $"{x.Item1}: {(x.Item2 * 1000):0.0000}ms")));
        }
    }
}
