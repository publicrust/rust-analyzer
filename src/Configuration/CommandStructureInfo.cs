using System.Collections.Generic;

namespace RustAnalyzer.Configuration
{
    public class CommandStructure
    {
        public string AttributeName { get; }
        public string[] ParameterTypes { get; }
        public string[] ParameterNames { get; }
        public string Example { get; }

        public CommandStructure(
            string attributeName,
            string[] parameterTypes,
            string[] parameterNames,
            string example
        )
        {
            AttributeName = attributeName;
            ParameterTypes = parameterTypes;
            ParameterNames = parameterNames;
            Example = example;
        }
    }

    public static class CommandStructureInfo
    {
        public static readonly Dictionary<string, CommandStructure> CommandStructures =
            new Dictionary<string, CommandStructure>
            {
                {
                    "ChatCommand",
                    new CommandStructure(
                        "ChatCommand",
                        new[] { "BasePlayer", "string", "string[]" },
                        new[] { "player", "command", "args" },
                        "[ChatCommand(\"name\")]\nvoid CommandName(BasePlayer player, string command, string[] args)"
                    )
                },
                {
                    "Command",
                    new CommandStructure(
                        "Command",
                        new[] { "IPlayer", "string", "string[]" },
                        new[] { "player", "command", "args" },
                        "[Command(\"name\")]\nvoid CommandName(IPlayer player, string command, string[] args)"
                    )
                },
                {
                    "ConsoleCommand",
                    new CommandStructure(
                        "ConsoleCommand",
                        new[] { "ConsoleSystem.Arg" },
                        new[] { "args" },
                        "[ConsoleCommand(\"name\")]\nvoid CommandName(ConsoleSystem.Arg args)"
                    )
                },
            };
    }
}
