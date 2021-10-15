using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Chat.Commands;
using Terraria.UI.Chat;

namespace ArchipelagoTerrariaClient {
    public enum ArchipelagoCommand {
        Help,
        Commands,
        Say,
        SetUrl,
        SetPwd,
        SetUsername,
        SetUuid,
        Connect,
        Forfeit
    }

    class ChatCommandsManager {
        private ArchipelagoNetHandler apNetHandler;
        const int maxLines = 9;

        public ChatCommandsManager (ArchipelagoNetHandler apNetHandler) {
            this.apNetHandler = apNetHandler;
            On.Terraria.Main.NewText_string_byte_byte_byte_bool += NewTextHookString;
            On.Terraria.Main.NewText_List1 += NewTextHookList;
        }

        private void NewTextHookString(On.Terraria.Main.orig_NewText_string_byte_byte_byte_bool orig, string message, byte R, byte G, byte B, bool force = false) {
            orig(message, R, G, B, force);
            ParseChatMessage(message);
        }

        private void NewTextHookList(On.Terraria.Main.orig_NewText_List1 orig, List<TextSnippet> snippetList) {
            orig(snippetList);
            StringBuilder sb = new StringBuilder();
            foreach (TextSnippet snippet in snippetList) {
                sb.Append(snippet.Text);
            }
            string message = sb.ToString();
            ParseChatMessage(message);
        }

        public void ParseChatMessage(string message) {
            // ClientLogger.LogMessage("Intercepted chat message: " + message, false);
            // Remove username. If there isn't one, ignore this message.
            int closingAngleBraceIndex = message.IndexOf("> !");
            if (closingAngleBraceIndex > 0) {
                message = message.Substring(closingAngleBraceIndex + 3);
            } else {
                return;
            }
            int firstSpaceIndex = message.IndexOf(" ");
            string cmd;
            if (firstSpaceIndex == -1) {
                cmd = message;
            } else {
                cmd = message.Substring(0, firstSpaceIndex);
            }
            ArchipelagoCommand cmdId;
            if (Enum.TryParse(cmd, out cmdId)) {
                switch (cmdId) {
                    case ArchipelagoCommand.Say:
                        // Ignore spaces for this command
                        ClientLogger.LogMessage("INFO: Executing Say command.");
                        Say(message.Substring(firstSpaceIndex + 1));
                        break;
                    case ArchipelagoCommand.SetUrl:
                        ClientLogger.LogMessage("INFO: Executing SetUrl command.");
                        SetUrl(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.SetPwd:
                        ClientLogger.LogMessage("INFO: Executing SetPwd command.");
                        SetPwd(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.SetUsername:
                        ClientLogger.LogMessage("INFO: Executing SetUsername command.");
                        SetUsername(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.SetUuid:
                        ClientLogger.LogMessage("INFO: Executing SetUuid command.");
                        SetUuid(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.Connect:
                        ClientLogger.LogMessage("INFO: Executing Connect command.");
                        Connect();
                        break;
                    case ArchipelagoCommand.Forfeit:
                        ClientLogger.LogMessage("INFO: Executing Forfeit command.");
                        Forfeit();
                        break;
                    case ArchipelagoCommand.Help:
                        ClientLogger.LogMessage("INFO: Executing Help command.");
                        Help(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.Commands:
                        ClientLogger.LogMessage("INFO: Executing Commands command");
                        int page;
                        if (Int32.TryParse(GetArgs(message, 1)[0], out page)) {
                            Commands(page - 1);
                        } else {
                            Commands(0);
                        }
                        break;
                    default:
                        ClientLogger.LogMessage("ERROR: Unsupported command ID");
                        break;
                }
            }
        }

        public string[] GetArgs(string message, int numArgs) {
            int index = message.IndexOf(" ") + 1;
            int length = 0;
            int numArgsGot = 0;
            string[] returnArgs = new string[numArgs];
            if (index <= 0) {
                return returnArgs;
            }
            while (numArgsGot < numArgs) {
                while (index < message.Length && message[index] != ' ') {
                    length++;
                    index++;
                }
                returnArgs[numArgsGot] = message.Substring(index - length, length);
                length = 0;
                // Skip past the space so we don't get stuck on it.
                index++;
                numArgsGot++;
            }
            return returnArgs;
        }

        public void Say(string message) {
            apNetHandler.Say(message);
        }

        public void SetUrl(string url) {
            if (Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute)) {
                apNetHandler.Url = url;
            } else {
                ClientLogger.LogMessage("WARNING: Invalid URI '" + url + "' supplied to command SetUrl.");
                ArchipelagoTerrariaClient.ShowChatMessage("Invalid URL", Color.Red);
            }
        }

        public void SetPwd(string pwd) {
            apNetHandler.Pwd = pwd;
        }

        public void SetUsername(string username) {
            apNetHandler.Username = username;
        }

        public void SetUuid(string uuid) {
            apNetHandler.Uuid = uuid;
        }

        public void Connect() {
            apNetHandler.CheckConnection();
        }

        public void Forfeit() {
            apNetHandler.TryForfeit();
        }

        public void Help(string commandName = null) {
            ArchipelagoCommand cmdId;
            if (Enum.TryParse(commandName, out cmdId)) {
                switch (cmdId) {
                    case ArchipelagoCommand.Say:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: !Say <Message>", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Displays a message in game, and sends it to other AP players.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Message can contain spaces and special characters.", Color.Green);
                        break;
                    case ArchipelagoCommand.SetUrl:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: !SetUrl <Url>", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Sets the Url that the client will connect to.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Defaults to localhost.", Color.Green);
                        break;
                    case ArchipelagoCommand.SetPwd:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: !SetPwd <Password>", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Sets the password to use when connecting to the AP server.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Password is discarded after attempting to connect to AP server.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Password is not very secure, do not reuse any important passwords.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Not necessary if the session is not password locked.", Color.Green);
                        break;
                    case ArchipelagoCommand.SetUsername:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: !SetUsername <Username>", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Sets what username is used to connect to the AP server.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Defaults to the name of your current Terraria character.", Color.Green);
                        break;
                    case ArchipelagoCommand.SetUuid:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: !SetUuid <Uuid>", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Sets which uuid is used to connect to the AP server.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("You probably don't need to mess with this.", Color.Green);
                        break;
                    case ArchipelagoCommand.Connect:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: !Connect", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Attempts to connect to the AP server.", Color.Green);
                        break;
                    case ArchipelagoCommand.Forfeit:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: !Forfeit", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Will forfeit the game.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("This will not work if your settings disallow it.", Color.Green);
                        break;
                    case ArchipelagoCommand.Help:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: !Help, !Help <Command Name>", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Shows a default help message if no valid command name is provided.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Shows tips about a single command if one is provided.", Color.Green);
                        break;
                    case ArchipelagoCommand.Commands:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage !Commands, !Commands <Page Number>", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Shows a list of possible commands.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Use page number to select which page to view.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Defaults to page 1.", Color.Green);
                        break;
                    default:
                        ArchipelagoTerrariaClient.ShowChatMessage("ERROR: This command does not have a help page.", Color.Red);
                        break;
                }
            } else {
                ArchipelagoTerrariaClient.ShowChatMessage("Type an exclamation mark, followed by the name of the command, to perform a command.", Color.Green);
                ArchipelagoTerrariaClient.ShowChatMessage("Separate arguments with a space, if your command takes any.", Color.Green);
                ArchipelagoTerrariaClient.ShowChatMessage("Type '!Commands' to get a list of commands.", Color.Green);
                ArchipelagoTerrariaClient.ShowChatMessage("Type '!Help <Command Name>' to get help with a specific command.", Color.Green);
            }
        }

        public void Commands(int page) {
            string[] commandNames = Enum.GetNames(typeof(ArchipelagoCommand));
            int numPages = ((int)Math.Floor(commandNames.Length / (double)(maxLines - 1)));
            if (page > numPages || page < 0) {
                ArchipelagoTerrariaClient.ShowChatMessage("Invalid page number!", Color.Red);
                ClientLogger.LogMessage("ERROR: Invalid Page Number provided to command 'Commands'.");
                return;
            }
            ArchipelagoTerrariaClient.ShowChatMessage("Available commands: Page " + (page + 1).ToString() + " of " + (numPages + 1).ToString(), Color.Green);
            for(int i = (maxLines - 1) * page; i < (maxLines - 1) * (page + 1); i++) {
                if (i >= commandNames.Length) {
                    break;
                }
                ArchipelagoTerrariaClient.ShowChatMessage(commandNames[i], Color.Green);
            }
        }
    }
}
