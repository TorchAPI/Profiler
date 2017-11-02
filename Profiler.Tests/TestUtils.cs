using System;
using System.IO;
using Torch.Utils;

namespace Profiler.Tests
{
    public sealed class TestUtils
    {
        public static void Init()
        {
            if (_torchResolver == null)
                _torchResolver = new TorchAssemblyResolver(GetBinaries("TorchBinaries"), GetBinaries("GameBinaries"));
        }

        private static string GetBinaries(string tag)
        {
            string dir = Environment.CurrentDirectory;
            while (!string.IsNullOrWhiteSpace(dir))
            {
                string gameBin = Path.Combine(dir, tag);
                if (Directory.Exists(gameBin))
                    return gameBin;

                dir = Path.GetDirectoryName(dir);
            }
            throw new Exception($"GetBinaries failed to find a folder named {tag} in the directory tree");
        }

        private static TorchAssemblyResolver _torchResolver;
    }
}
