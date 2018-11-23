using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace SwitchCommands {
    public class PluginCommands {
        public static string switchParameters = "/switch <add/list/del/cooldown/ignoreperms/cancel/rebind/done>";

        public static void RegisterCommands() {
            Commands.ChatCommands.Add(new Command("switchcommands", SwitchCmd, "switch"));
            Commands.ChatCommands.Add(new Command("switchcommands", SwitchReload, "reload"));
        }

        private static void SwitchReload(CommandArgs args) {
            SwitchCommands.database = Database.Read(Database.databasePath);
            if (!File.Exists(Database.databasePath)) {
                SwitchCommands.database.Write(Database.databasePath);
            }
        }

        private static void SwitchCmd(CommandArgs args) {
            var player = args.Player;

            switch (player.GetData<PlayerState>("PlayerState")) {
                case PlayerState.None:
                    player.SendSuccessMessage("Activate a switch to bind it to commands.");
                    player.SetData("PlayerState", PlayerState.SelectingSwitch);
                    return;

                case PlayerState.AddingCommands:
                    if (args.Parameters.Count == 0) {
                        player.SendErrorMessage("Invalid syntax. " + switchParameters);
                        return;
                    }

                    if (player.GetData<CommandInfo>("CommandInfo") == null)
                        player.SetData("CommandInfo", new CommandInfo());

                    var cmdInfo = player.GetData<CommandInfo>("CommandInfo");

                    switch (args.Parameters[0].ToLower()) {
                        case "add":
                            var command = "/" + string.Join(" ", args.Parameters.Skip(1));
                            cmdInfo.commandList.Add(command);
                            player.SendSuccessMessage("Added {0}".SFormat(command));
                            break;

                        case "list":
                            player.SendMessage("Current commands binded:", Color.Green);
                            for (int x = 0; x < cmdInfo.commandList.Count; x++) {
                                player.SendMessage("({0}) ".SFormat(x) + cmdInfo.commandList[x], Color.Yellow);
                            }
                            break;

                        case "del":
                            int commandIndex = 0;

                            if (args.Parameters.Count < 2 || !int.TryParse(args.Parameters[1], out commandIndex)) {
                                player.SendErrorMessage("Wrong syntax. /switch del <command index>");
                                return;
                            }

                            var cmdDeleted = cmdInfo.commandList[commandIndex];
                            cmdInfo.commandList.RemoveAt(commandIndex);

                            player.SendSuccessMessage("Removed {0} at index {1}.".SFormat(cmdDeleted, commandIndex));
                            break;

                        case "cooldown":
                            float cooldown = 0;

                            if (args.Parameters.Count < 2 || !float.TryParse(args.Parameters[1], out cooldown)) {
                                player.SendErrorMessage("Wrong syntax. /switch cooldown <seconds>");
                                return;
                            }

                            cmdInfo.cooldown = cooldown;

                            player.SendSuccessMessage("Switch cooldown set to {0} seconds.".SFormat(cooldown));
                            break;

                        case "ignoreperms":
                            bool ignorePerms = false;

                            if (args.Parameters.Count < 2 || !bool.TryParse(args.Parameters[1], out ignorePerms)) {
                                player.SendErrorMessage("Wrong syntax. /switch ignoreperms <true/false>");
                                return;
                            }

                            cmdInfo.ignorePerms = ignorePerms;
                            
                            player.SendSuccessMessage("Switch ignoring player perms: {0}.".SFormat(ignorePerms));
                            break;

                        case "cancel":
                            player.SetData("PlayerState", PlayerState.None);
                            player.SetData("CommandInfo", new CommandInfo());
                            player.SendSuccessMessage("Cancelled adding commands to switch.");
                            return;

                        case "rebind":
                            player.SendSuccessMessage("Press a switch to rebind.");
                            player.SetData("PlayerState", PlayerState.SelectingSwitch);
                            return;

                        case "done":
                            var switchPos = player.GetData<SwitchPos>("SwitchPos");

                            player.SendSuccessMessage("Binded switch at X: {0}, Y: {1} with commands:".SFormat(switchPos.X, switchPos.Y));
                            foreach(string cmd in cmdInfo.commandList) {
                                player.SendMessage(cmd, Color.Yellow);
                            }
                            SwitchCommands.database.switchCommandList[player.GetData<SwitchPos>("SwitchPos").ToString()] = cmdInfo;
                            player.SetData("PlayerState", PlayerState.None);
                            player.SetData("SwitchPos", new Vector2());
                            player.SetData("CommandInfo", new CommandInfo());
                            return;

                        default:
                            player.SendErrorMessage("Invalid syntax. " + switchParameters);
                            return;
                    }

                    player.SetData("CommandInfo", cmdInfo);

                    return;
            }
        }

        public enum PlayerState {
            None,
            AddingCommands,
            SelectingSwitch
        }
    }
}
