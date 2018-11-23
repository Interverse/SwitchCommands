using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Streams;
using System.Linq;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using static SwitchCommands.PluginCommands;

namespace SwitchCommands {
    [ApiVersion(2, 1)]
    public class SwitchCommands : TerrariaPlugin {

        public static Database database;

        public override string Name => "SwitchCommands";
        public override string Author => "Johuan";
        public override string Description => "Run commands with a switch/level/pressure plate";
        public override Version Version => new Version(1, 0, 0, 0);

        public SwitchCommands(Main game) : base(game) { }

        public override void Initialize() {
            database = Database.Read(Database.databasePath);
            if (!File.Exists(Database.databasePath)) {
                database.Write(Database.databasePath);
            }

            PluginCommands.RegisterCommands();

            ServerApi.Hooks.NetGetData.Register(this, GetData);
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                ServerApi.Hooks.NetGetData.Deregister(this, GetData);

                database.Write(Database.databasePath);
            }
            base.Dispose(disposing);
        }

        private void GetData(GetDataEventArgs args) {
            using (MemoryStream data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)) {
                var player = TShock.Players[args.Msg.whoAmI];

                switch (args.MsgID) {
                    case PacketTypes.HitSwitch:
                        SwitchPos pos = new SwitchPos(data.ReadInt16(), data.ReadInt16());
                        var tile = Main.tile[pos.X, pos.Y];

                        if (tile.type == TileID.Lever) {
                            if (tile.frameX % 36 == 0)
                                pos.X++;

                            if (tile.frameY == 0)
                                pos.Y++;
                        }

                        var playerState = player.GetData<PlayerState>("PlayerState");

                        if (playerState == PlayerState.SelectingSwitch) {
                            player.SetData("SwitchPos", pos);
                            player.SendSuccessMessage("Binding commands to switch in X: {0}, Y: {1}".SFormat(pos.X, pos.Y));
                            player.SendSuccessMessage("Type /switch to see list of commands.".SFormat(pos.X, pos.Y));
                            player.SetData("PlayerState", PlayerState.AddingCommands);

                            if (database.switchCommandList.ContainsKey(pos.ToString())) {
                                player.SetData("CommandInfo", database.switchCommandList[pos.ToString()]);
                            }

                            return;
                        }

                        if (playerState == PlayerState.None) {
                            if (database.switchCommandList.ContainsKey(pos.ToString())) {
                                double seconds = 999999;

                                var cooldown = player.GetData<Dictionary<string, DateTime>>("Cooldown");

                                if (cooldown != null && cooldown.ContainsKey(pos.ToString())) {
                                    seconds = (DateTime.Now - player.GetData<Dictionary<string, DateTime>>("Cooldown")[pos.ToString()]).TotalMilliseconds / 1000;
                                }

                                if (seconds < database.switchCommandList[pos.ToString()].cooldown) {
                                    player.SendErrorMessage("You must wait {0} more seconds before using this switch.".SFormat(database.switchCommandList[pos.ToString()].cooldown - seconds));
                                    return;
                                }
                                
                                Group currGroup = null;

                                bool ignorePerms = database.switchCommandList[pos.ToString()].ignorePerms;

                                foreach (string cmd in database.switchCommandList[pos.ToString()].commandList) {
                                    if (ignorePerms) {
                                        currGroup = player.Group;
                                        player.Group = new SuperAdminGroup();
                                    }

                                    Commands.HandleCommand(player, cmd.ReplaceTags(player));

                                    if (ignorePerms) {
                                        player.Group = currGroup;
                                    }
                                }
                                
                                if (cooldown == null) {
                                    cooldown = new Dictionary<string, DateTime>() { { pos.ToString(), DateTime.Now } };
                                } else {
                                    cooldown[pos.ToString()] = DateTime.Now;
                                }

                                player.SetData("Cooldown", cooldown);
                            }
                        }

                        break;
                }
            }
        }
    }

    public static class StringManipulator {
        public static string ReplaceTags(this string s, TSPlayer player) {
            List<string> response = s.Split(' ').ToList();

            for (int x = response.Count - 1; x >= 0; x--)
                if (response[x] == "$name")
                    response[x] = player.Name;

            return string.Join(" ", response);
        }
    }
}
