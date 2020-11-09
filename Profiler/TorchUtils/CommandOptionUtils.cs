using System.Text.RegularExpressions;

namespace TorchUtils
{
    internal static class CommandOptionUtils
    {
        static readonly Regex _optionRegex = new Regex(@"--(\w+?)=(.+?)(?: |$)");

        public static bool TryParseOption(this string arg, out string key, out string value)
        {
            key = null;
            value = null;

            var match = _optionRegex.Match(arg);
            if (!match.Success) return false;

            key = match.Groups[1].Value;
            value = match.Groups[2].Value;
            return true;
        }
    }
}