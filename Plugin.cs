using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.IO;
using Dalamud.Utility;
using System.Collections.Generic;
using Microsoft.VisualBasic;
using System.Data.Common;
using ImGuiNET;

namespace FC {
    public sealed class Plugin : IDalamudPlugin {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;

        private const string CommandName = "/fcg";
        private const string CommandUI = "/fui";
        private const string LogFilePath = "E:\\DalamudPlugins\\FC\\Log.txt";
        private bool uiVisible = false;
        private List<(DateTime, string, int, int)> rollTable = new List<(DateTime, string, int, int)>();
        public string Name => "FC Giveaway";

        public Plugin() {
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
                HelpMessage = "Type /fcg to start."
            });
            CommandManager.AddHandler(CommandUI, new CommandInfo(OnUICommand) {
                HelpMessage = "Open the roll table UI."
            });

            if (!File.Exists(LogFilePath)) {
                File.Create(LogFilePath).Dispose();
            }
            OnCommand(CommandName, string.Empty);
            OnUICommand(CommandName, string.Empty);
            PluginInterface.UiBuilder.Draw += DrawUI;
        }

        public void Dispose() {
            // Unsubscribe from the chat message event
            ChatGui.ChatMessage -= OnChatMessage;

            CommandManager.RemoveHandler(CommandName);
        }

        private void OnCommand(string command, string args) {
            ChatGui.Print("Message Event Started");
            ChatGui.ChatMessage += OnChatMessage;
        }
        private void OnUICommand(string command, string args) {
            uiVisible = true;
        }

        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
            using (StreamWriter writer = new StreamWriter(LogFilePath, true)) {
                writer.WriteLine($"[{DateTime.Now}] - {sender.TextValue} - {message.TextValue}");
            }
            if (message.TextValue.Contains("Random")) {
                ExtractRoll(sender.TextValue, message.TextValue);
            }
        }
       private void ExtractRoll(string sender, string message) {
            string[] tokens = message.Split(' ');
            int roll = 0, maxRoll = 0;

            switch (tokens.Length) {
                case 2:
                    foreach (string input in tokens) {
                        if (int.TryParse(input, out roll)) {
                            rollTable.Add((DateTime.Now, sender, roll, 1000));
                        }
                    }
                    break;

                case 3:
                    foreach (string input in tokens) {
                        if (input.Contains(')')) {
                            string trimmed = input.Trim('(', ')');
                            string[] parts = trimmed.Split('-');
                            if (parts.Length == 2 && int.TryParse(parts[1], out maxRoll)) {
                                // Extract maxRoll successfully
                            }
                        }
                    }
                    if (int.TryParse(tokens[2], out roll)) {
                        rollTable.Add((DateTime.Now, sender, roll, maxRoll));
                    }
                    break;

                case 5:
                    foreach (string input in tokens) {
                        if (int.TryParse(input.Trim('.'), out roll)) {
                            string name = ClientState.LocalPlayer?.Name.ToString() ?? "You";
                            rollTable.Add((DateTime.Now, name, roll, 1000)); 
                            break;
                        }
                    }
                    break;

                case 6:
                    foreach (string input in tokens) {
                        if (int.TryParse(input.Trim('.'), out roll)) {
                            string senderName = $"{tokens[1]} {tokens[2]}";
                            rollTable.Add((DateTime.Now, senderName, roll, 1000)); 
                            break;
                        }
                    }
                    break;

                case 8:
                    foreach (string input in tokens) {
                        if (int.TryParse(input, out int parsedRoll)) {
                            roll = parsedRoll;
                        } else if (input.Contains(')')) {
                            string trimmed = input.Trim(')', '.');
                            if (int.TryParse(trimmed, out int parsedMaxRoll)) {
                                maxRoll = parsedMaxRoll;
                                string name = ClientState.LocalPlayer?.Name.ToString() ?? "You";
                                rollTable.Add((DateTime.Now, name, roll, maxRoll));
                            }
                        }
                    }
                    break;

                case 9:
                    if (int.TryParse(tokens[5], out roll)) {
                        rollTable.Add((DateTime.Now, sender, roll, 1000)); 
                    } else {
                        foreach (string input in tokens) {
                            if (input.Contains(')')) {
                                string trimmed = input.Trim(')');
                                if (int.TryParse(trimmed, out maxRoll)) {
                                    // Valid maxRoll found
                                }
                            }
                        }
                        rollTable.Add((DateTime.Now, sender, roll, maxRoll));
                    }
                    break;

                default:
                    ChatGui.PrintError("Unrecognized roll message format.");
                    break;
            }
        }

        private void DrawUI() {
            if (!uiVisible) return;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(470, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Roll Table", ref uiVisible)) {
                string currentWinner = "None";
                int highestRoll = 0;
                foreach (var (_, sender, roll, _) in rollTable) {
                    if (roll > highestRoll) {
                        highestRoll = roll;
                        currentWinner = sender;
                    }
                }

                ImGui.Text($"Current Winner: {currentWinner} - {highestRoll}");
                ImGui.NewLine();
                if (ImGui.Button("Reset")) {
                    rollTable.Clear();
                }

                ImGui.Separator();

                if (ImGui.BeginTable("Rolls", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) {
                    ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.WidthFixed, 150);
                    ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthFixed, 150);
                    ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 85);
                    ImGui.TableSetupColumn("Max Roll", ImGuiTableColumnFlags.WidthFixed, 70);
                    ImGui.TableHeadersRow();

                    foreach (var (datetime, sender, roll, maxRoll) in rollTable) {
                        ImGui.TableNextRow();

                        if (roll == highestRoll) {
                            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            ImGui.PushStyleColor(ImGuiCol.TableRowBg, new System.Numerics.Vector4(0.5f, 0.5f, 0.0f, 0.2f));
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text(datetime.ToString("h:mm:ss tt"));
                        ImGui.TableNextColumn();
                        ImGui.Text(sender);
                        ImGui.TableNextColumn();
                        ImGui.Text(roll.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(maxRoll == 0 ? "N/A" : maxRoll.ToString());

                        if (roll == highestRoll) {
                            ImGui.PopStyleColor(2);
                        }
                    }
                    ImGui.EndTable();
                }
            }
            ImGui.End();
        }
    }
}
