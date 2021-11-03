using System;
using NLog;

namespace Profiler.Core
{
    /// <summary>
    /// Entrypoint to custom profiler measurements
    /// </summary>
    public readonly struct CustomProfiling : IDisposable
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly ProfilerToken? _token;

        /// <summary>
        /// Begins profiling a custom measurement.
        /// `Dispose()` must be called to queue the result
        /// </summary>
        /// <remarks>
        /// `id` will be used to query the profiling result by prefix as in:
        /// `!profile custom --prefix=Tic-Tac-`
        /// </remarks>
        /// <param name="id">Unique name that identifies this custom measurement</param>
        /// <param name="gameObject">Game object associated with this measurement, or null</param>
        /// <returns></returns>
        public static IDisposable Profile(string id, object gameObject = null)
        {
            var index = StringIndexer.Instance.IndexOf(id);
            var token = ProfilerPatch.StartToken(gameObject, index, ProfilerCategory.Custom);
            return new CustomProfiling(token);
        }

        CustomProfiling(ProfilerToken? token)
        {
            _token = token;
        }

        public void Dispose()
        {
            ProfilerPatch.StopToken(_token);
        }
    }
}