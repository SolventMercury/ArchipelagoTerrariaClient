using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Terraria;
using Terraria.ModLoader;
using Terraria.Achievements;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using Terraria.ID;

namespace ArchipelagoTerrariaClient {
	public class TerrariaReward {
		public List<int> id  = new List<int>();
		public List<int> qty = new List<int>();
		
		public TerrariaReward(List<int> id, List<int> qty) {
			this.id = id;
			this.qty = qty;
        }
	}
	
	public class ArchipelagoTerrariaClient : Mod {
		private List<TerrariaReward> rewards = new List<TerrariaReward>();

		private ArchipelagoNetHandler apNetHandler;

		private ChatCommandsManager chatHandler;

		// Only 
		private bool debug = true;

		// TODO: Put Login Prompt in Chat - listen for response
		public override void Load() {
			//Main.Achievements.OnAchievementCompleted += this.OnAchievementCompleted;
			//ReportAchievementProgress();
			// Hook our custom stuff into the achievement function.
			On.Terraria.Achievements.AchievementCondition.Complete += AchievementCompletionHook;
			// Load achievement definitions
			AllAchievements.Load();
			// Get all rewards set up
			RewardsSetup();
			// Start the network handler
			this.apNetHandler = new ArchipelagoNetHandler("Terraria", rewards, debug);
			// Start the chat commands manager
			this.chatHandler = new ChatCommandsManager(apNetHandler);
			if (debug) {
				ClientLogger.chatLoggingLevel = LoggingLevel.Info;
			} else {
				ClientLogger.chatLoggingLevel = LoggingLevel.None;
            }
		}

        public override void PreSaveAndQuit() {
            base.PreSaveAndQuit();
			apNetHandler.Close();
        }

		// Load up all the rewards
		// TODO: Externalize this
		private void RewardsSetup() {
			TerrariaReward reward = new TerrariaReward(new List<int> { 3507 }, new List<int> { 1 });
			rewards.Add(reward);
        }

		// Condition Prefixes:
		// ItemPickupCondition:       ITEM_PICKUP_[ID]
        // ItemCraftCondition:        ITEM_PICKUP_[ID]
		// TileDestroyedCondition:    TILE_DESTROYED_[ID]
		// ProgressionEventCondition: PROGRESSION_EVENT_[ID]
		// NpcKilledCondition:        NPC_KILLED_[ID]

		private short GetConditionID (string conditionName) {
			int index = conditionName.Length - 1;
			while (conditionName[index] != '_') {
				index--;
			}
			string numString = conditionName.Substring(index + 1);
			return short.Parse(numString);
		}

		public static void ShowChatMessage (string str, Color col) {
			List<Terraria.UI.Chat.TextSnippet> snippets = new List<Terraria.UI.Chat.TextSnippet>();
			snippets.Add(new Terraria.UI.Chat.TextSnippet(str, col));
			Terraria.Main.NewText(snippets);
		}

		public static void AwardItems (TerrariaReward reward) {
			ClientLogger.LogMessage("INFO: Giving " + reward.id.Count.ToString() + " items for this reward.");
			for (int i = 0; i < reward.id.Count; i++) {
				ClientLogger.LogMessage("INFO: Received " + reward.qty[i].ToString() + " of item with index " + reward.id[i].ToString());
				ShowChatMessage("Received " + reward.qty[i].ToString() + " of item with index " + reward.id[i].ToString(), Color.DarkCyan);
				foreach (Player player in Main.player) {
					int number = Item.NewItem((int)player.position.X, (int)player.position.Y, player.width, player.height, reward.id[i], reward.qty[i], false, 0, false, false);
					if (Main.netMode == NetmodeID.MultiplayerClient) {
						NetMessage.SendData(MessageID.SyncItem, -1, -1, null, number, 1f, 0f, 0f, 0, 0, 0);
					}
					//OBSOLETE (probably)
					//for (int j = 0; j < reward.qty[i]; j++) {
					//	player.PutItemInInventory(reward.id[i]);
					//}
				}
				Thread.Sleep(100);
			}
			//Terraria.Item.NewItem(player.position, 2379, noGrabDelay: true);
		}	

		// Example condition name:
		// ITEM_PICKUP_9
		// (Pick up item "Wood")
		private void AchievementCompletionHook (On.Terraria.Achievements.AchievementCondition.orig_Complete orig, AchievementCondition ac) {
			// apNetHandler.CheckConnection();
			// Thanks to Black Sliver for this piece of code.
			// Really remarkable, could not have gotten this working without their help.
			Type typeOfAchvCond = null;
			typeOfAchvCond = ac.GetType();
			var achvEvent = typeOfAchvCond.BaseType.GetField("OnComplete", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic).GetValue(ac);
			if (achvEvent == null) {
				ClientLogger.LogMessage("WARNING: Event handler not found - event likely has no subscribers.");
				return;
			}

			Terraria.Achievements.AchievementCondition.AchievementUpdate handler = (Terraria.Achievements.AchievementCondition.AchievementUpdate)achvEvent;
			var delegates = handler.GetInvocationList();
			if (delegates == null) {
				ClientLogger.LogMessage("WARNING: Handler has no delegates.");
				return;
			}

			string achievementName = null;
			foreach (var d in delegates) {
				if (d == null) ClientLogger.LogMessage("WARNING: Delegate is null (unexpected).");
				else if (d.Target == null) ClientLogger.LogMessage("WARNING: Target is null (static method).");
				else if (d.Target.GetType() == null) ClientLogger.LogMessage("WARNING: Target type is null (unexpected).");
				else {
					try {
						achievementName = ((Terraria.Achievements.Achievement)d.Target).Name;
						ClientLogger.LogMessage("INFO: Target is Achievement named " + achievementName);
					} catch (InvalidCastException) {
						ClientLogger.LogMessage("WARNING: Target cast to type Achievement failed");
						return;
					}
				}
			}

			// Check off this condition in our achievement name.
			// TODO: Refactor this - transfer more general functionality to apNetHandler
			lock (apNetHandler) {
				if (achievementName != null && !apNetHandler.HasLocationBeenChecked(achievementName)) {
					ClientLogger.LogMessage("INFO: Registering unique achievement " + achievementName);
					ClientLogger.LogMessage("Previously checked locations:");
					foreach (string location in apNetHandler.LocationsChecked) {
						ClientLogger.LogMessage("\t" + location);
					}
					
					foreach (AchievementData achvData in AllAchievements.achievementList) {
						if (achvData.achievementName.Equals(achievementName)) {
							if (achvData.achievementConditions.Count <= 1) {
								// Give achievement
								AwardAchievement(achievementName);
							} else {
								string ConditionType = "Custom";
								if (ac.Name.Contains("PICKUP")) {
									ConditionType = "Item";
								} else if (ac.Name.Contains("NPC")) {
									ConditionType = "NpcKilledCondition";
								} else if (ac.Name.Contains("TILE")) {
									ConditionType = "TileDestroyedCondition";
								} else if (ac.Name.Contains("EVENT")) {
									ConditionType = "ProgressionEventCondition";
								}
								short ConditionID = GetConditionID(ac.Name);
								foreach (ArchipelagoAchievementCondition condition in achvData.achievementConditions) {
									if (condition.achievementType.Contains(ConditionType)) {
										if (ConditionType != "Custom") {
											foreach (short id in condition.idList) {
												if (ConditionID == id) {
													condition.completed = true;
												}
											}
										} else {
											ClientLogger.LogMessage("ERROR: INVALID ACHIEVEMENT CONDITION DATA");
										}
									}
								}
								for (int i = achvData.achievementConditions.Count - 1; i >= 0; i--) {
									if (achvData.achievementConditions[i].completed) {
										achvData.achievementConditions.RemoveAt(i);
									}
								}
							}
							break;
						}
					}
				}
			}
			orig(ac);
		}

		public void AwardAchievement (string achievementName) {
			ClientLogger.LogMessage("INFO: Player has completed achievement " + achievementName);
			apNetHandler.RevealInternalState();
			string achievementRealName;
			if (AllAchievements.achievementNameDict.TryGetValue(achievementName, out achievementRealName)) {
				ShowChatMessage("Player has completed achievement \"" + achievementRealName + "\"", Color.Yellow);
			} else {
				ShowChatMessage("Player has completed achievement " + achievementName, Color.Yellow);
			}
			lock (apNetHandler) {
				apNetHandler.MarkLocationAsChecked(achievementName);
			}
		}
	}

	class ArchipelagoNetHandler {
		private const int MaxPacketHandlingThreads = 5;
	
		private ArchipelagoSession session = null;
		private Queue<ArchipelagoPacketBase> handshakePacketQueue = new Queue<ArchipelagoPacketBase>();
		private Queue<ArchipelagoPacketBase> packetsToSendQueue = new Queue<ArchipelagoPacketBase>();
		private Queue<ArchipelagoPacketBase> packetsReceivedQueue = new Queue<ArchipelagoPacketBase>();

		private Thread apConnectThread = null;

		private Thread apSendPacketsThread = null;

		private Thread[] apPacketReceivedThreads = new Thread[MaxPacketHandlingThreads];
		private Thread apPacketReceivedThreadsManager;
		private bool receivedLastSessionLocations = false;
		private Semaphore delayItemAwardsSemaphore = new Semaphore(1, 1);

		private int itemIndex = 0;
		private List<TerrariaReward> rewards = new List<TerrariaReward>();
		private List<string> locationsChecked = new List<string>();

		// Used to keep track of how many locations we completed this session the last time after we received the last item packet.
		// The different between current location count and last location count is the number of items we should be receiving.
		private int lastLocationCount = 0;

		private string game = "Game";
		private string url = "localhost";
		private string username = "YourName1";
		private string pwd = "";
		private bool debug = false;

		// Properties

		public string Url { set => url = value; }
		public string Username { set => url = value; }
		public string Pwd { set => pwd = value; }
		public bool Debug { set => debug = value; }

		public List<string> LocationsChecked { get => locationsChecked; }

		// Methods

		public ArchipelagoNetHandler (string game, List<TerrariaReward> rewards, bool debug = false) {
			this.game = game;
			this.rewards = rewards;
			this.debug = debug;
        }

		public void TryConnect() {
			bool madeWebsocketConnection = false, serverAcceptedHandshake = false;
			// Connect to server's websocket
			while (!madeWebsocketConnection) {
				madeWebsocketConnection = StartSession();
				if (!madeWebsocketConnection) {
					Thread.Sleep(100);
				}
			}
			// Start packet receiving thread

			// Perform server handshake
			apPacketReceivedThreadsManager = new Thread(ManagePacketReceivedQueue);
			apPacketReceivedThreadsManager.IsBackground = true;
			apPacketReceivedThreadsManager.Start();
			while (!serverAcceptedHandshake) {
				serverAcceptedHandshake = Handshake();
				if (!serverAcceptedHandshake) {
					Thread.Sleep(100);
				}
			}
			apSendPacketsThread = new Thread(TrySendPackets);
			apSendPacketsThread.IsBackground = true;
			apSendPacketsThread.Start();
			// Send all locations completed.
			session.SendPacket(GetLocationsCheckedPacket());
		}

		public bool StartSession() {
			// Thanks to ljwu for this tidy method of constructing the URI.
			var uri = new UriBuilder();
			uri.Scheme = "ws://";
			uri.Host = url;
			uri.Port = 38281;
			string uriString = uri.Uri.ToString();
			//string uriString = "ws://localhost:38281";
			ClientLogger.LogMessage("INFO: Attempting to Connect to Server at " + uriString + "...");
			session = new ArchipelagoSession(uriString);
			session.PacketReceived += PacketReceived;
			session.ErrorReceived += ErrorReceived;
			session.Connect();
			if (session.Connected) {
				ClientLogger.LogMessage("INFO: Connected to Server");
				return true;
			} else {
				ClientLogger.LogMessage("WARNING: Unable to Connect to Server");
				return false;
			}
		}

		public void ManagePacketReceivedQueue() {
			while(true) {
				if (packetsReceivedQueue.Count > 0) {
					for (int i = 0; i < apPacketReceivedThreads.Length; i++) {
						if (apPacketReceivedThreads[i] == null || apPacketReceivedThreads[i].IsAlive) {
							// Start a new thread and shove it into slot i
							apPacketReceivedThreads[i] = StartPacketReceivedThread(packetsReceivedQueue.Dequeue());
							if (packetsReceivedQueue.Count == 0) {
								break;
                            }
                        }
                    }
                }
				Thread.Sleep(100);
            }
        }

		public Thread StartPacketReceivedThread(ArchipelagoPacketBase packet) {
			Thread packetReceivedThread = new Thread(() => HandleReceivedPacket(packet));
			packetReceivedThread.IsBackground = true;
			packetReceivedThread.Start();
			return packetReceivedThread;
        }

		public void PacketReceived(ArchipelagoPacketBase packet) {
			packetsReceivedQueue.Enqueue(packet);
		}

		public void HandleReceivedPacket (ArchipelagoPacketBase packet) {
			ClientLogger.LogMessage("INFO: Packet Received:");
			ClientLogger.LogMessage("\t" + packet.ToString());
			if (ShouldRespondToPacketAutomatically(packet.PacketType)) {
				switch (packet.PacketType) {
					case ArchipelagoPacketType.ReceivedItems:
						while (receivedLastSessionLocations == false) {
							Thread.Sleep(100);
                        }
						delayItemAwardsSemaphore.WaitOne();
						List<NetworkItem> netItems = ((ReceivedItemsPacket)packet).Items;
						int netIndex = ((ReceivedItemsPacket)packet).Index;
						if (netIndex == itemIndex) {
							// Only reward the last n items we receive,
							// Where n is the number of locations sent since last session.
							// If we already got this item in a previous session,
							// Don't give it to us again.
							ClientLogger.LogMessage("Delivered " + (netItems.Count - (netItems.Count - (locationsChecked.Count - lastLocationCount))).ToString() + " items.");
							for (int i = netItems.Count - (locationsChecked.Count - lastLocationCount); i < netItems.Count; i++) {
								NetworkItem item = netItems[i];
								// Award item
								if (item.Item - 73000 > 0) {
									ArchipelagoTerrariaClient.AwardItems(rewards[item.Item - 73001]);
								} else if (item.Item == 0) {
									ArchipelagoTerrariaClient.ShowChatMessage("You win!", Color.RoyalBlue);
								} else {
									ClientLogger.LogMessage("ERROR: Unknown Item " + item.Item.ToString() + " received.");
								}
							}
							lastLocationCount = locationsChecked.Count;
							itemIndex += ((ReceivedItemsPacket)packet).Items.Count;
						} else if (netIndex == 0) {
							foreach (NetworkItem item in netItems.GetRange(itemIndex, netItems.Count - itemIndex)) {
								if (item.Item - 73000 > 0) {
									ArchipelagoTerrariaClient.AwardItems(rewards[item.Item - 73001]);
								} else if (item.Item == 0) {
									ArchipelagoTerrariaClient.ShowChatMessage("You win!", Color.RoyalBlue);
								} else {
									ClientLogger.LogMessage("ERROR: Unknown Item " + item.Item.ToString() + " received.");
								}
							}
							itemIndex = ((ReceivedItemsPacket)packet).Items.Count;
						} else {
							lock (packetsToSendQueue) {
								packetsToSendQueue.Enqueue(new SyncPacket());
								packetsToSendQueue.Enqueue(GetLocationsCheckedPacket());
							}
						}
						delayItemAwardsSemaphore.Release();
						break;
					case ArchipelagoPacketType.RoomUpdate:

						break;
					case ArchipelagoPacketType.Print:
						ClientLogger.LogMessage("INFO: Received message " + ((PrintPacket)packet).Text);
						ArchipelagoTerrariaClient.ShowChatMessage(((PrintPacket)packet).Text, Color.Green);
						break;
					case ArchipelagoPacketType.PrintJSON:
						List<JsonMessagePart> jsonMessageParts = ((PrintJsonPacket)packet).Data;
						StringBuilder sb = new StringBuilder();
						foreach (JsonMessagePart part in jsonMessageParts) {
							sb.Append(part.Text);
						}
						ArchipelagoTerrariaClient.ShowChatMessage(sb.ToString(), Color.Aquamarine);
						break;
					case ArchipelagoPacketType.Bounce:

						break;
					case ArchipelagoPacketType.LocationChecks:
						InterpretLocationsCheckedPacket((LocationChecksPacket)packet);
						break;
				}
			} else {
				lock (handshakePacketQueue) {
					this.handshakePacketQueue.Enqueue(packet);
				}
			}
		}

		public LocationChecksPacket GetLocationsCheckedPacket() {
			var packetToReturn = new LocationChecksPacket();
			List<int> indicesChecked = new List<int>();
			lock (locationsChecked) {
				foreach (string location in locationsChecked) {
					for (int i = 0; i < AllAchievements.achievementList.Count; i++) {
						AchievementData achievement = AllAchievements.achievementList[i];
						if (achievement.achievementName.Equals(location)) {
							indicesChecked.Add(i + 73001);
							break;
						}
					}
				}
			}
			packetToReturn.Locations = indicesChecked;
			return packetToReturn;
		}

		public void InterpretLocationsCheckedPacket(LocationChecksPacket packet) {
			InterpretLocationsCheckedPacket(packet.Locations);
		}

		public void InterpretLocationsCheckedPacket(List<int> locations) {
			lock (locationsChecked) {
				foreach (int locationIndex in locations) {
					string achievementName = AllAchievements.achievementList[locationIndex - 73001].achievementName;
					MarkLocationAsChecked(achievementName);
					// Do not count items we are being told of by the server on connection - since we did them in a prior session.
					ClientLogger.LogMessage("INFO: Server sent location " + locationIndex.ToString() + " on connection.");
				}
			}
		}

		public void TrySendPackets() {
			while (true) {
				Thread.Sleep(100);
				lock (packetsToSendQueue) {
					if (packetsToSendQueue.Count > 0) {
						// TODO: Add error handling
						foreach (ArchipelagoPacketBase packet in packetsToSendQueue.ToArray()) {
                            ClientLogger.LogMessage("INFO: Packet of type " + packet.GetType().ToString() + "was sent.");
                        }
                        session.SendMultiplePackets(packetsToSendQueue.ToArray());
						packetsToSendQueue.Clear();
					}
				}
			}
		}

		public void RevealInternalState() {
			if (debug) {
				ClientLogger.LogMessage("INFO: Locations Checked:");
				foreach (string location in locationsChecked) {
					ClientLogger.LogMessage("*" + location);
				}
				ClientLogger.LogMessage("INFO: Number of locations checked: " + locationsChecked.Count.ToString());
				ClientLogger.LogMessage("INFO: Item index: " + itemIndex.ToString());
				ClientLogger.LogMessage("INFO: Last Location Count: " + lastLocationCount.ToString());

            }
        }

		public bool ShouldRespondToPacketAutomatically(ArchipelagoPacketType packetType) {
			List<ArchipelagoPacketType> packetTypes = new List<ArchipelagoPacketType>{
				ArchipelagoPacketType.ReceivedItems,
				ArchipelagoPacketType.RoomUpdate,
				ArchipelagoPacketType.Print,
				ArchipelagoPacketType.PrintJSON,
				ArchipelagoPacketType.Bounce,
				ArchipelagoPacketType.LocationChecks
			};
			return packetTypes.Contains(packetType);
		}

		public void ErrorReceived(Exception e, string message) {
			ClientLogger.LogMessage("INFO: Error Received");
		}

		public bool Handshake() {
			var roomInfoPacket = WaitForHandshakePacket();
			if (roomInfoPacket.PacketType == ArchipelagoPacketType.RoomInfo) {
				ClientLogger.LogMessage("INFO: Received room info packet!");
				ClientLogger.LogMessage("\tVersion " + ((RoomInfoPacket)roomInfoPacket).Version.ToString());
				ClientLogger.LogMessage(((RoomInfoPacket)roomInfoPacket).Players.Count.ToString() + " \tPlayers.");
				foreach (NetworkPlayer player in ((RoomInfoPacket)roomInfoPacket).Players) {
					ClientLogger.LogMessage("\tPlayer " + player.Slot.ToString() + ":");
					ClientLogger.LogMessage("\t\tPlayer Name: " + player.Name);
					ClientLogger.LogMessage("\t\tPlayer Alias: " + player.Alias);
					ClientLogger.LogMessage("\t\tPlayer Team: " + player.Team.ToString());
				}
			} else {
				// Throw exception TODO
				return false;
			}
			SendConnectInfo();
			var connectResponsePacket = WaitForHandshakePacket();
			if (connectResponsePacket.PacketType == ArchipelagoPacketType.Connected) {
				ClientLogger.LogMessage("INFO: Received connection response packet!");
				ClientLogger.LogMessage("INFO: Connection established!");
				InterpretLocationsCheckedPacket(((ConnectedPacket)connectResponsePacket).ItemsChecked);
				lastLocationCount = ((ConnectedPacket)connectResponsePacket).ItemsChecked.Count;
				receivedLastSessionLocations = true;
				return true;
			} else if (connectResponsePacket.PacketType == ArchipelagoPacketType.ConnectionRefused) {
				ClientLogger.LogMessage("INFO: Received connection response packet!");
				ClientLogger.LogMessage("WARNING: Connection refused!");
				return false;
			} else {
				// Throw exception TODO
				return false;
			}

		}

		public ArchipelagoPacketBase WaitForHandshakePacket() {
			ArchipelagoPacketBase packet = null;
			while (packet == null) {
				if (handshakePacketQueue.Count > 0) {
					packet = handshakePacketQueue.Dequeue();
					break;
				}
				Thread.Sleep(300);
			}
			return packet;
		}

		public void SendConnectInfo() {
			string playerName = Main.player[Main.myPlayer].name;
			var connectPacket = new ConnectPacket();

			connectPacket.Game = this.game;
			if (username is null) {
				// TODO
				if (debug) {
					connectPacket.Name = "YourName1";
				} else {
					connectPacket.Name = playerName;
				}

			} else {
				connectPacket.Name = username;
            }
			
			// TODO
			// Figure this bit out
			connectPacket.Uuid = Convert.ToString(connectPacket.Name.GetHashCode(), 16);

			// SEND VERSION
			connectPacket.Version = new Version(0, 3, 0);
			connectPacket.Tags = new List<string> { "AP" };

			if (pwd == null) {
				connectPacket.Password = "";
			} else {
				connectPacket.Password = pwd;
				// null the password after using it
				pwd = null;
            }
			session.SendPacket(connectPacket);
		}

		public void AbortThreads() {
			// If we have a thread running trying to send data and the player quits, cancel it.
			if (apSendPacketsThread != null && apSendPacketsThread.IsAlive) {
				apSendPacketsThread.Abort();
			}
			// If we're trying to connect to a server and the player quits, cancel that thread too.
			if (apConnectThread != null && apConnectThread.IsAlive) {
				apConnectThread.Abort();
			}
			// Abort threads that try and handle packets
			foreach (Thread packetReceivedThread in apPacketReceivedThreads) {
				if (packetReceivedThread != null && packetReceivedThread.IsAlive) {
					packetReceivedThread.Abort();
				}
			}
		}
			
		public void Close() {
			this.AbortThreads();
			// NOTE: Disconnect() works even if player is not currently connected.
			if (this.session != null) {
				if (this.session.Connected) {
					this.session.Disconnect();
					ArchipelagoTerrariaClient.ShowChatMessage("Successfully disconnected.", Color.Yellow);
					ClientLogger.LogMessage("INFO: Connection to server closed.");
				}
			}
			
		}

		public void CheckConnection() {
			// If we are not trying to connect, and we haven't already entered a session, begin the connection process
			if ((apConnectThread == null || !apConnectThread.IsAlive) && (session == null || session.Connected == false)) {
				apConnectThread = new Thread(() => TryConnect());
				apConnectThread.IsBackground = true;
				apConnectThread.Start();
			} else {
				ArchipelagoTerrariaClient.ShowChatMessage("Already connected to AP server.", Color.Red);
            }
		}

		public void QueueLocationsCheckedPacket() {
			packetsToSendQueue.Enqueue(GetLocationsCheckedPacket());
		}

		// public void QueueLocationCheckedPacket(int locationIndex) {
		// 	  List<int> indexList = new List<int>();
		// 	  packetsToSendQueue.Enqueue();
		// }

		public void MarkLocationAsChecked(string locationName) {
			if (!this.locationsChecked.Contains(locationName)) {
				this.locationsChecked.Add(locationName);
			}
        }

		public bool HasLocationBeenChecked(string locationName) {
			return this.locationsChecked.Contains(locationName);
        }

		// TODO
		public void TryForfeit() {
			throw new NotImplementedException();
		}

		// TODO
		public void Say(string message) {
			throw new NotImplementedException();
		}

		public void ToggleDebug() {
			this.debug = !this.debug;
        }
	}

	public enum LoggingLevel {
		None,
		Error,
		Warning,
		Info
    }

	class ClientLogger {
		private static Semaphore loggingSemaphore = new Semaphore(1, 1);
		// What level of message to show in chat
		public static LoggingLevel chatLoggingLevel = LoggingLevel.None;
		// What level of message to write to log
		public static LoggingLevel loggingLevel = LoggingLevel.Info;

		// Thread safe
		// Uses static semaphore so that only one thread writes to file at a time.
		// Other threads will wait their turn if they cannot do so.
		public static void LogMessage(string str) {
			loggingSemaphore.WaitOne();
			StreamWriter sw = new StreamWriter(Path.Combine(Logging.LogDir, "ArchipelagoLog.txt"), append: true);

			LoggingLevel messageLevel = LoggingLevel.Info;
			Color col = Color.White;
			if (str.Contains("ERROR")) {
				col = Color.Red;
				messageLevel = LoggingLevel.Error;
			} else if (str.Contains("WARNING")) {
				messageLevel = LoggingLevel.Warning;
				col = Color.Yellow;
			}
			if (messageLevel <= chatLoggingLevel) {
				ArchipelagoTerrariaClient.ShowChatMessage(str, col);
			}
			if (messageLevel <= loggingLevel) {
				sw.WriteLine(str);
			}
			sw.Flush();
			sw.Close();
			loggingSemaphore.Release();
		}
	}

}