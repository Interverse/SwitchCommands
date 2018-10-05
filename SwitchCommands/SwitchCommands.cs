﻿using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.IO.Streams;
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
                                var seconds = (DateTime.Now - player.GetData<DateTime>("Cooldown")).TotalMilliseconds / 1000;

                                if (seconds < database.switchCommandList[pos.ToString()].cooldown) {
                                    player.SendErrorMessage("You must wait {0} more seconds before using this switch.".SFormat(seconds));
                                    return;
                                }
                                
                                Group currGroup = null;

                                bool ignorePerms = database.switchCommandList[pos.ToString()].ignorePerms;

                                foreach (string cmd in database.switchCommandList[pos.ToString()].commandList) {
                                    if (ignorePerms) {
                                        currGroup = player.Group;
                                        player.Group = new SuperAdminGroup();
                                    }

                                    Commands.HandleCommand(player, cmd);

                                    if (ignorePerms) {
                                        player.Group = currGroup;
                                    }
                                }

                                player.SetData("Cooldown", DateTime.Now);
                            }
                        }

                        break;
                }
            }
        }
    }
}