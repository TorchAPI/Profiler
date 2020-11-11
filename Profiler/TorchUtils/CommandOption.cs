using System.Text.RegularExpressions;

namespace TorchUtils
{
    internal class CommandOption
    {
        const string OptionPrefix = "-";
        static readonly Regex _optionRegex = new Regex($@"{OptionPrefix}(\w+?)=(.+?)(?: |$)");
        static readonly Regex _parameterlessOptionRegex = new Regex($@"{OptionPrefix}(\w+?)(?: |$)");

        readonly string _arg;

        CommandOption(string arg)
        {
            _arg = arg;
        }

        public static bool TryGetOption(string arg, out CommandOption option)
        {
            if (arg.StartsWith(OptionPrefix))
            {
                option = new CommandOption(arg);
                return true;
            }

            option = default;
            return false;
        }

        public bool TryParse(string key, out string value)
        {
            value = null;

            var match = _optionRegex.Match(_arg);
            if (!match.Success) return false;

            var keyStr = match.Groups[1].Value;
            if (keyStr == key)
            {
                value = match.Groups[2].Value;
                return true;
            }

            return false;
        }

        public bool IsParameterless(string key)
        {
            var match = _parameterlessOptionRegex.Match(_arg);
            if (!match.Success) return false;

            var keyStr = match.Groups[1].Value;
            return keyStr == key;
        }
    }
}