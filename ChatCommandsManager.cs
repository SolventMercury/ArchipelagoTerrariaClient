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
        Connect,
        Forfeit,
        Disconnect,
        ToggleDebug,
        SetLoggingLevel,
        RevealInternalState
    }

    class ChatCommandsManager {
        private ArchipelagoNetHandler apNetHandler;
        private const int maxLines = 9;

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

        private void ParseChatMessage(string message) {
            // ClientLogger.LogMessage("Intercepted chat message: " + message, false);
            // Remove username. If there isn't one, ignore this message.
            // TODO: Base this on player name
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
                string[] args;
                switch (cmdId) {
                    case ArchipelagoCommand.Say:
                        // Ignore spaces for this command
                        ClientLogger.LogMessage("INFO: Attempting to Execute Command \"Say\".");
                        Say(message.Substring(firstSpaceIndex + 1));
                        break;
                    case ArchipelagoCommand.SetUrl:
                        ClientLogger.LogMessage("INFO: Attempting to Execute Command \"SetUrl\".");
                        args = GetArgs(message, 1);
                        if (args[0] == null) {
                            ArchipelagoTerrariaClient.ShowChatMessage("You must specify a valid url!", Color.Red);
                            ClientLogger.LogMessage("ERROR: Player called SetUrl but did not provide a url.");
                            break;
                        }
                        SetUrl(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.SetPwd:
                        ClientLogger.LogMessage("INFO: Attempting to Execute Command \"SetPwd\".");
                        args = GetArgs(message, 1);
                        if (args[0] == null) {
                            ArchipelagoTerrariaClient.ShowChatMessage("You must specify a valid password!", Color.Red);
                            ClientLogger.LogMessage("ERROR: Player called SetPwd but did not provide a password.");
                            break;
                        }
                        SetPwd(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.SetUsername:
                        ClientLogger.LogMessage("INFO: Attempting to Execute Command \"SetUsername\".");
                        args = GetArgs(message, 1);
                        if (args[0] == null) {
                            ArchipelagoTerrariaClient.ShowChatMessage("You must provide a username!", Color.Red);
                            ClientLogger.LogMessage("ERROR: Player called SetUsername but did not provide a username.");
                            break;
                        }
                        SetUsername(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.Connect:
                        ClientLogger.LogMessage("INFO: Attempting to Execute Command \"Connect\".");
                        Connect();
                        break;
                    case ArchipelagoCommand.Forfeit:
                        ClientLogger.LogMessage("INFO: Attempting to Execute Command \"Forfeit\".");
                        Forfeit();
                        break;
                    case ArchipelagoCommand.Help:
                        ClientLogger.LogMessage("INFO: Attempting to Execute Command \"Help\".");
                        Help(GetArgs(message, 1)[0]);
                        break;
                    case ArchipelagoCommand.Commands:
                        ClientLogger.LogMessage("INFO: Attempting to Execute Command \"Commands\".");
                        int page;
                        if (Int32.TryParse(GetArgs(message, 1)[0], out page)) {
                            Commands(page - 1);
                        } else {
                            Commands(0);
                        }
                        break;
                    case ArchipelagoCommand.Disconnect:
                        ClientLogger.LogMessage("INFO: Attempting to Execute command \"Disconnect\".");
                        Disconnect();
                        break;
                    case ArchipelagoCommand.ToggleDebug:
                        ClientLogger.LogMessage("INFO: Attempting to Execute command \"ToggleDebug\".");
                        ToggleDebug();
                        break;
                    case ArchipelagoCommand.SetLoggingLevel:
                        ClientLogger.LogMessage("INFO: Attempting to Execute command \"SetChatLoggingLevel\".");
                        int loggingLevelId;
                        LoggingLevel loggingLevel;
                        args = GetArgs(message, 1);
                        if (args[0] == null) {
                            ArchipelagoTerrariaClient.ShowChatMessage("You must specify a valid logging level!", Color.Red);
                            ClientLogger.LogMessage("ERROR: Player attempted to set chatLoggingLevel to invalid logging level " + args[0] + ".");
                            break;
                        }
                        if (Int32.TryParse(args[0], out loggingLevelId)) {
                            if (loggingLevelId >= 0 && loggingLevelId <= 4) {
                                SetChatLoggingLevel((LoggingLevel)loggingLevelId);
                            } else {
                                ArchipelagoTerrariaClient.ShowChatMessage(args[0].ToString() + " is not a valid logging level!", Color.Red);
                                ClientLogger.LogMessage("ERROR: Player attempted to set chatLoggingLevel to invalid logging level " + args[0] + ".");
                            }
                        } else {
                            if (Enum.TryParse(args[0], out loggingLevel)) {
                                SetChatLoggingLevel(loggingLevel);
                            } else {
                                ArchipelagoTerrariaClient.ShowChatMessage(args[0].ToString() + " is not a valid logging level!", Color.Red);
                                ClientLogger.LogMessage("ERROR: Player attempted to set chatLoggingLevel to invalid logging level " + args[0] + ".");
                            }
                        }  
                        break;
                    case ArchipelagoCommand.RevealInternalState:
                        apNetHandler.RevealInternalState();
                        break;
                    default:
                        ClientLogger.LogMessage("ERROR: Unsupported command ID");
                        break;
                }
            } else {
                ArchipelagoTerrariaClient.ShowChatMessage("\"" + cmd + "\" is not a valid command!", Color.Red);
                ClientLogger.LogMessage("ERROR: Player attempted to enter invalid command \"" + cmd + "\".");
            }
        }

        // TODO: Base this on player name
        private string[] GetArgs(string message, int numArgs) {
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
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!Say <Message>\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Displays a message in game, and sends it to other AP players.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Message can contain spaces and special characters.", Color.Green);
                        break;
                    case ArchipelagoCommand.SetUrl:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!SetUrl <Url>\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Sets the Url that the client will connect to.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Defaults to localhost.", Color.Green);
                        break;
                    case ArchipelagoCommand.SetPwd:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!SetPwd <Password>\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Sets the password to use when connecting to the AP server.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Password is discarded after attempting to connect to AP server.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Password is not very secure, do not reuse any important passwords.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Not necessary if the session is not password locked.", Color.Green);
                        break;
                    case ArchipelagoCommand.SetUsername:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!SetUsername <Username>\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Sets what username is used to connect to the AP server.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Defaults to the name of your current Terraria character.", Color.Green);
                        break;
                    case ArchipelagoCommand.Connect:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!Connect\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Attempts to connect to the AP server.", Color.Green);
                        break;
                    case ArchipelagoCommand.Forfeit:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!Forfeit\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Will forfeit the game.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("This will not work if your settings disallow it.", Color.Green);
                        break;
                    case ArchipelagoCommand.Help:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!Help\", \"!Help <Command Name>\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Shows a default help message if no valid command name is provided.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Shows tips about a single command if one is provided.", Color.Green);
                        break;
                    case ArchipelagoCommand.Commands:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!Commands\", \"!Commands <Page Number>\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Shows a list of possible commands.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Use page number to select which page to view.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Defaults to page 1.", Color.Green);
                        break;
                    case ArchipelagoCommand.Disconnect:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!Disconnect\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Disconnects your game from Archipelago (AP).", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Has no effect if you are not connected to AP when issuing this command.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("While disconnected, you will not receive messages, items, or other interactions from AP.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("You will also be unable to send items or messages, forfeit, or otherwise interact with AP.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("Achievements obtained while disconnected will be registered to AP as soon as you reconnect.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("However, obtained achievements will be forgotten if you close the game while disconnected.", Color.Green);
                        break;
                    case ArchipelagoCommand.ToggleDebug:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!ToggleDebug\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Toggles client debugging features.", Color.Green);
                        break;
                    case ArchipelagoCommand.SetLoggingLevel:
                        ArchipelagoTerrariaClient.ShowChatMessage("Usage: \"!SetChatLoggingLevel <LoggingLevel>\"", Color.Yellow);
                        ArchipelagoTerrariaClient.ShowChatMessage("Sets the level at which log messages are also written to chat.", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("0: No messages", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("1: Error messages", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("2: Warning messages", Color.Green);
                        ArchipelagoTerrariaClient.ShowChatMessage("3: Info messages", Color.Green);
                        break;
                        
                    default:
                        ArchipelagoTerrariaClient.ShowChatMessage("ERROR: This command does not have a help page.", Color.Red);
                        break;
                }
            } else {
                ArchipelagoTerrariaClient.ShowChatMessage("Type an exclamation mark, followed by the name of the command, to perform a command.", Color.Green);
                ArchipelagoTerrariaClient.ShowChatMessage("Separate arguments with a space, if your command takes any.", Color.Green);
                ArchipelagoTerrariaClient.ShowChatMessage("Type \"!Commands\" to get a list of commands.", Color.Green);
                ArchipelagoTerrariaClient.ShowChatMessage("Type \"!Help <Command Name>\" to get help with a specific command.", Color.Green);
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

        public void Disconnect() {
            apNetHandler.Close();
        }

        public void ToggleDebug() {
            apNetHandler.ToggleDebug();
            ArchipelagoTerrariaClient.ShowChatMessage("Debug toggled.", Color.Green);
        }

        public void SetChatLoggingLevel(LoggingLevel loggingLevel) {
            ClientLogger.chatLoggingLevel = loggingLevel;
            ArchipelagoTerrariaClient.ShowChatMessage("Logging level set to " + loggingLevel.ToString(), Color.Green);
        }
    }
}
