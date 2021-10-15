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
using WebSocketSharp;
using System.Text;
using System.Diagnostics;

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
		
		public bool debug = true;

		private List<TerrariaReward> rewards = new List<TerrariaReward>();

		private ArchipelagoNetHandler apNetHandler;

		private ChatCommandsManager chatHandler;

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
			this.apNetHandler = new ArchipelagoNetHandler(rewards);
			// Start the chat commands manager
			this.chatHandler = new ChatCommandsManager(apNetHandler);
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
					if (Main.netMode == 1) {
						NetMessage.SendData(21, -1, -1, null, number, 1f, 0f, 0f, 0, 0, 0);
					}
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
					} catch (InvalidCastException e) {
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
			string achievementRealName;
			if (AllAchievements.achievementNameDict.TryGetValue(achievementName, out achievementRealName)) {
				ShowChatMessage("Player has completed achievement \"" + achievementRealName + "\"", Color.Yellow);
			} else {
				ShowChatMessage("Player has completed achievement " + achievementName, Color.Yellow);
			}
			lock (apNetHandler) {
				apNetHandler.MarkLocationAsChecked(achievementName);
				apNetHandler.QueueLocationsCheckedPacket();
			}
		}
	}

	class ArchipelagoAchievementCondition {
		public string achievementType;
		public short[] idList;
		public string flagName;
		public int intGoal;
		public float floatGoal;
		public string statToCheck;
		public bool completed = false;

		public ArchipelagoAchievementCondition (string type, short[] idList) {
			this.achievementType = type;
			this.idList = idList;
		}

		public ArchipelagoAchievementCondition (string type, string flagName) {
			this.achievementType = type;
			this.flagName = flagName;
		}

		public ArchipelagoAchievementCondition (string type, string statistic, int qty) {
			this.achievementType = type;
			this.intGoal = qty;
			this.statToCheck = statistic;
		}

		public ArchipelagoAchievementCondition (string type, string statistic, float qty) {
			this.achievementType = type;
			this.floatGoal = qty;
			this.statToCheck = statistic;
		}
	}

	class ArchipelagoNetHandler {
		private ArchipelagoSession session = null;
		private Queue<ArchipelagoPacketBase> packetQueue = new Queue<ArchipelagoPacketBase>();
		private Queue<ArchipelagoPacketBase> packetsToSendQueue = new Queue<ArchipelagoPacketBase>();
		private Thread apConnectThread = null;
		private Thread apSendPacketsThread = null;
		bool ignoreNextItemPacket = false;

		private int itemIndex = 0;
		private List<TerrariaReward> rewards = new List<TerrariaReward>();
		private List<string> locationsChecked = new List<string>();

		private string url = "localhost";
		private string username = "YourName1";
		private string pwd = "";
		private string uuid = null;
		private bool debug = false;

		// Properties

		public string Url { set => url = value; }
		public string Username { set => url = value; }
		public string Pwd { set => pwd = value; }
		public string Uuid { set => uuid = value; }
		public bool Debug { set => debug = value; }

		public List<string> LocationsChecked { get => locationsChecked; }

		// Methods

		public ArchipelagoNetHandler (List<TerrariaReward> rewards, bool debug = false) {
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
			// Connect to server
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

		public void PacketReceived(ArchipelagoPacketBase packet) {
			ClientLogger.LogMessage("INFO: Packet Received:");
			ClientLogger.LogMessage("\t" + packet.ToString());
			if (ShouldRespondToPacketAutomatically(packet.PacketType)) {
				switch (packet.PacketType) {
					case ArchipelagoPacketType.ReceivedItems:
						List<NetworkItem> netItems = ((ReceivedItemsPacket)packet).Items;
						int netIndex = ((ReceivedItemsPacket)packet).Index;
						if (ignoreNextItemPacket) {
							ignoreNextItemPacket = false;
							itemIndex = netItems.Count;
							return;
						}
						if (netIndex == itemIndex) {
							foreach (NetworkItem item in netItems) {
								if (item.Item - 73000 > 0) {
									ArchipelagoTerrariaClient.AwardItems(rewards[item.Item - 73001]);
								} else if (item.Item == 0) {
									ArchipelagoTerrariaClient.ShowChatMessage("You win!", Color.RoyalBlue);
								} else {
									ClientLogger.LogMessage("ERROR: Unknown Item " + item.Item.ToString() + " received.");
								}
							}
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
				lock (packetQueue) {
					this.packetQueue.Enqueue(packet);
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
							indicesChecked.Add(i);
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
					string achievementName = AllAchievements.achievementList[locationIndex].achievementName;
					if (!locationsChecked.Contains(achievementName)) {
						locationsChecked.Add(achievementName);
					}
				}
			}
		}

		public void TrySendPackets() {
			while (true) {
				Thread.Sleep(100);
				lock (packetsToSendQueue) {
					if (packetsToSendQueue.Count > 0) {
						session.SendMultiplePackets(packetsToSendQueue.ToArray());
						packetsToSendQueue.Clear();
					}
				}
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
			var roomInfoPacket = WaitForPacket();
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
			ignoreNextItemPacket = true;
			SendConnectInfo();
			var connectResponsePacket = WaitForPacket();
			if (connectResponsePacket.PacketType == ArchipelagoPacketType.Connected) {
				ClientLogger.LogMessage("INFO: Received connection response packet!");
				ClientLogger.LogMessage("INFO: Connection established!");
				InterpretLocationsCheckedPacket(((ConnectedPacket)connectResponsePacket).ItemsChecked);
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

		public ArchipelagoPacketBase WaitForPacket() {
			ArchipelagoPacketBase packet = null;
			while (packet == null) {
				if (packetQueue.Count > 0) {
					packet = packetQueue.Dequeue();
					break;
				}
				Thread.Sleep(300);
			}
			return packet;
		}

		public void SendConnectInfo() {
			string playerName = Main.player[Main.myPlayer].name;
			var connectPacket = new ConnectPacket();

			connectPacket.Game = "Terraria";
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

			connectPacket.Version = new Version(0, 1, 0);
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
		}

		// TODO
		public void Close() {
			this.AbortThreads();
        }

		public void CheckConnection() {
			// If we are not trying to connect, and we haven't already entered a session, begin the connection process
			if ((apConnectThread == null || !apConnectThread.IsAlive) && (session == null || session.Connected == false)) {
				apConnectThread = new Thread(() => TryConnect());
				apConnectThread.IsBackground = true;
				apConnectThread.Start();
			}
		}

		public void QueueLocationsCheckedPacket() {
			packetsToSendQueue.Enqueue(GetLocationsCheckedPacket());
		}

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
	}

	class ClientLogger {
		private static Semaphore loggingSemaphore = new Semaphore(1, 1);

		// Thread safe
		// Uses static semaphore so that only one thread writes to file at a time.
		// Other threads will wait their turn if they cannot do so.
		public static void LogMessage(string str, bool showInChat = true) {
			loggingSemaphore.WaitOne();
			StreamWriter sw = new StreamWriter(Path.Combine(Logging.LogDir, "ArchipelagoLog.txt"), append: true);
			sw.WriteLine(str);
			if (showInChat /*|| debug*/) {
				Color col = Color.White;
				if (str.Contains("WARNING")) {
					col = Color.Yellow;
				}
				if (str.Contains("ERROR")) {
					col = Color.Red;
				}
				ArchipelagoTerrariaClient.ShowChatMessage(str, col);
			}
			sw.Flush();
			sw.Close();
			loggingSemaphore.Release();
		}
	}

	class AchievementData {
		public List<ArchipelagoAchievementCondition> achievementConditions = new List<ArchipelagoAchievementCondition>();
		public string achievementName;

		public AchievementData (string name, string category) {
			this.achievementName = name;
		}

		public void AddCondition (string conditionType, short[] conditionIds, bool achieveAll = false) {
			if (achieveAll) {
				foreach (short id in conditionIds) {
					this.achievementConditions.Add(new ArchipelagoAchievementCondition(conditionType, new short[] { id }));
				}
			} else {
				this.achievementConditions.Add(new ArchipelagoAchievementCondition(conditionType, conditionIds));
			}

		}

		public void AddCondition (string conditionType, string flagName) {
			this.achievementConditions.Add(new ArchipelagoAchievementCondition(conditionType, flagName));
		}

		public void AddCondition (string conditionType, string statistic, int qty) {
			this.achievementConditions.Add(new ArchipelagoAchievementCondition(conditionType, statistic, qty));
		}

		public void AddCondition (string conditionType, string statistic, float qty) {
			this.achievementConditions.Add(new ArchipelagoAchievementCondition(conditionType, statistic, qty));
		}

	}

	class AllAchievements {
		// Token: 0x06001D7F RID: 7551 RVA: 0x0042F4AC File Offset: 0x0042D6AC
		public static List<AchievementData> achievementList = new List<AchievementData>();
		public static Dictionary<string, string> achievementNameDict = new Dictionary<string, string>() {
			{"TIMBER", "Timber!!"},
			{"NO_HOBO", "No Hobo"},
			{"OBTAIN_HAMMER", "Stop! Hammer Time!"},
			{"OOO_SHINY", "Ooo! Shiny!"},
			{"HEART_BREAKER", "Heart Breaker"},
			{"HEAVY_METAL", "Heavy Metal"},
			{"I_AM_LOOT", "I Am Loot!"},
			{"STAR_POWER", "Star Power"},
			{"HOLD_ON_TIGHT", "Hold on Tight!"},
			{"EYE_ON_YOU", "Eye on You"},
			{"SMASHING_POPPET", "Smashing, Poppet!"},
			{"WORM_FODDER", "Worm Fodder"},
			{"MASTERMIND", "Mastermind"},
			{"WHERES_MY_HONEY", "Where's My Honey?"},
			{"STING_OPERATION", "Sting Operation"},
			{"BONED", "Boned"},
			{"DUNGEON_HEIST", "Dungeon Heist"},
			{"ITS_GETTING_HOT_IN_HERE", "It's Getting Hot in Here"},
			{"MINER_FOR_FIRE", "Miner for Fire"},
			{"STILL_HUNGRY", "Still Hungry"},
			{"ITS_HARD", "It's Hard!"},
			{"BEGONE_EVIL", "Begone, Evil!"},
			{"EXTRA_SHINY", "Extra Shiny!"},
			{"HEAD_IN_THE_CLOUDS", "Head in the Clouds"},
			{"LIKE_A_BOSS", "Like a Boss"},
			{"BUCKETS_OF_BOLTS", "Buckets of Bolts"},
			{"DRAX_ATTAX", "Drax Attax"},
			{"PHOTOSYNTHESIS", "Photosynthesis"},
			{"GET_A_LIFE", "Get a Life"},
			{"THE_GREAT_SOUTHERN_PLANTKILL", "The Great Southern Plantkill"},
			{"TEMPLE_RAIDER", "Temple Raider"},
			{"LIHZAHRDIAN_IDOL", "Lihzahrdian Idol"},
			{"ROBBING_THE_GRAVE", "Robbing the Grave"},
			{"BIG_BOOTY", "Big Booty"},
			{"FISH_OUT_OF_WATER", "Fish Out of Water"},
			{"OBSESSIVE_DEVOTION", "Obsessive Devotion"},
			{"STAR_DESTROYER", "Star Destroyer"},
			{"CHAMPION_OF_TERRARIA", "Champion of Terraria"},
			{"BLOODBATH", "Bloodbath"},
			{"SIPPERY_SHINOBI", "Slippery Shinobi"},
			{"GOBLIN_PUNTER", "Goblin Punter"},
			{"WALK_THE_PLANK", "Walk the Plank"},
			{"KILL_THE_SUN", "Kill the Sun"},
			{"DO_YOU_WANT_TO_SLAY_A_SNOWMAN", "Do You Want to Slay a Snowman?"},
			{"TIN_FOIL_HATTER", "Tin-Foil Hatter"},
			{"BALEFUL_HARVEST", "Baleful Harvest"},
			{"ICE_SCREAM", "Ice Scream"},
			{"STICKY_SITUATION", "Sticky Situation"},
			{"REAL_ESTATE_AGENT", "Real Estate Agent"},
			{"NOT_THE_BEES", "Not the Bees!"},
			{"JEEPERS_CREEPERS", "Jeepers Creepers"},
			{"FUNKYTOWN", "Funkytown"},
			{"INTO_ORBIT", "Into Orbit"},
			{"ROCK_BOTTOM", "Rock Bottom"},
			{"MECHA_MAYHEM", "Mecha Mayhem"},
			{"GELATIN_WORLD_TOUR", "Gelatin World Tour"},
			{"FASHION_STATEMENT", "Fashion Statement"},
			{"VEHICULAR_MANSLAUGHTER", "Vehicular Manslaughter"},
			{"BULLDOZER", "Bulldozer"},
			{"THERE_ARE_SOME_WHO_CALL_HIM", "There are Some Who Call Him..."},
			{"DECEIVER_OF_FOOLS", "Deceiver of Fools"},
			{"SWORD_OF_THE_HERO", "Sword of the Hero"},
			{"LUCKY_BREAK", "Lucky Break"},
			{"THROWING_LINES", "Throwing Lines"},
			{"DYE_HARD", "Dye Hard"},
			{"SICK_THROW", "Sick Throw"},
			{"FREQUENT_FLYER", "The Frequent Flyer"},
			{"THE_CAVALRY", "The Cavalry"},
			{"COMPLETELY_AWESOME", "Completely Awesome"},
			{"TIL_DEATH", "Til Death..."},
			{"ARCHAEOLOGIST", "Archaeologist"},
			{"PRETTY_IN_PINK", "Pretty in Pink"},
			{"RAINBOWS_AND_UNICORNS", "Rainbows and Unicorns"},
			{"YOU_AND_WHAT_ARMY", "You and What Army?"},
			{"PRISMANCER", "Prismancer"},
			{"IT_CAN_TALK", "It Can Talk?!"},
			{"WATCH_YOUR_STEP", "Watch Your Step!"},
			{"MARATHON_MEDALIST", "Marathon Medalist"},
			{"GLORIOUS_GOLDEN_POLE", "Glorious Golden Pole"},
			{"SERVANT_IN_TRAINING", "Servant-in-Training"},
			{"GOOD_LITTLE_SLAVE", "Good Little Slave"},
			{"TROUT_MONKEY", "Trout Monkey"},
			{"FAST_AND_FISHIOUS", "Fast and Fishious"},
			{"SUPREME_HELPER_MINION", "Supreme Helper Minion!"},
			{"TOPPED_OFF", "Topped Off"},
			{"SLAYER_OF_WORLDS", "Slayer of Worlds"},
			{"YOU_CAN_DO_IT", "You Can Do It!"},
			{"MATCHING_ATTIRE", "Matching Attire"}
		};

		public static void Load () {
			AchievementData achievement = new AchievementData("TIMBER", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				9,
				619,
				2504,
				620,
				2503,
				2260,
				621,
				911,
				1729
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("NO_HOBO", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				8
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("OBTAIN_HAMMER", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				2775,
				2746,
				3505,
				654,
				3517,
				7,
				3493,
				2780,
				1513,
				2516,
				660,
				3481,
				657,
				922,
				3511,
				2785,
				3499,
				3487,
				196,
				367,
				104,
				797,
				2320,
				787,
				1234,
				1262,
				3465,
				204,
				217,
				1507,
				3524,
				3522,
				3525,
				3523,
				1305
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("OOO_SHINY", "Explorer");
			achievement.AddCondition("TileDestroyedCondition", new short[]
			{
				7,
				6,
				9,
				8,
				166,
				167,
				168,
				169,
				22,
				204,
				58,
				107,
				108,
				111,
				221,
				222,
				223,
				211
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("HEART_BREAKER", "Explorer");
			achievement.AddCondition("TileDestroyedCondition", new short[]
			{
				12
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("HEAVY_METAL", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				35,
				716
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("I_AM_LOOT", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Peek");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("STAR_POWER", "Collector");
			achievement.AddCondition("CustomFlagCondition", "Use");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("HOLD_ON_TIGHT", "Collector");
			achievement.AddCondition("CustomFlagCondition", "Equip");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("EYE_ON_YOU", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				4
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("SMASHING_POPPET", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				7
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("WORM_FODDER", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				13,
				14,
				15
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("MASTERMIND", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				266
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("WHERES_MY_HONEY", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Reach");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("STING_OPERATION", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				222
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("BONED", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				35
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("DUNGEON_HEIST", "Explorer");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				327
			});
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				19
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("ITS_GETTING_HOT_IN_HERE", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Reach");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("MINER_FOR_FIRE", "Collector");
			achievement.AddCondition("ItemCraftCondition", new short[] {
				122
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("STILL_HUNGRY", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				113,
				114
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("ITS_HARD", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				9
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("BEGONE_EVIL", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[] {
				6
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("EXTRA_SHINY", "Explorer");
			achievement.AddCondition("TileDestroyedCondition", new short[]
			{
				107,
				108,
				111,
				221,
				222,
				223
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("HEAD_IN_THE_CLOUDS", "Collector");
			achievement.AddCondition("CustomFlagCondition", "Equip");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("LIKE_A_BOSS", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				1133,
				1331,
				1307,
				267,
				1293,
				557,
				544,
				556,
				560,
				43,
				70
			});
			AllAchievements.achievementList.Add(achievement);

			// WARNING: FIXME
			// Twins might be tracked separately by mistake!!!
			achievement = new AchievementData("BUCKETS_OF_BOLTS", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				125,
				126,
				127,
				134
			}, true);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("DRAX_ATTAX", "Collector");
			achievement.AddCondition("ItemCraftCondition", new short[]
			{
				579,
				990
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("PHOTOSYNTHESIS", "Explorer");
			achievement.AddCondition("TileDestroyedCondition", new short[]
			{
				211
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("GET_A_LIFE", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Use");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("THE_GREAT_SOUTHERN_PLANTKILL", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				262
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("TEMPLE_RAIDER", "Collector");
			achievement.AddCondition("TileDestroyedCondition", new short[]
			{
				226
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("LIHZAHRDIAN_IDOL", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				245
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("ROBBING_THE_GRAVE", "Explorer");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				1513,
				938,
				963,
				977,
				1300,
				1254,
				1514,
				679,
				759,
				1446,
				1445,
				1444,
				1183,
				1266,
				671
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("BIG_BOOTY", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				20
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("FISH_OUT_OF_WATER", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				370
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("OBSESSIVE_DEVOTION", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				439
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("STAR_DESTROYER", "Slayer");
			achievement.AddCondition("NPCKilledCondition", new short[]
			{
				517,
				422,
				507,
				493
			}, true);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("CHAMPION_OF_TERRARIA", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				398
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("BLOODBATH", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				5
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("SLIPPERY_SHINOBI", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				50
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("GOBLIN_PUNTER", "Slayer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				10
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("WALK_THE_PLANK", "Slayer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				11
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("KILL_THE_SUN", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				3
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("DO_YOU_WANT_TO_SLAY_A_SNOWMAN", "Slayer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				12
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("TIN_FOIL_HATTER", "Slayer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				13
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("BALEFUL_HARVEST", "Slayer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				15
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("ICE_SCREAM", "Slayer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				14
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("STICKY_SITUATION", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				16
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("REAL_ESTATE_AGENT", "Challenger");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				17
			});

			AllAchievements.achievementList.Add(achievement);
			achievement = new AchievementData("NOT_THE_BEES", "Challenger");
			achievement.AddCondition("CustomFlagCondition", "Use");

			AllAchievements.achievementList.Add(achievement);
			achievement = new AchievementData("JEEPERS_CREEPERS", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Reach");

			AllAchievements.achievementList.Add(achievement);
			achievement = new AchievementData("FUNKYTOWN", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Reach");

			AllAchievements.achievementList.Add(achievement);
			achievement = new AchievementData("INTO_ORBIT", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Reach");

			AllAchievements.achievementList.Add(achievement);
			achievement = new AchievementData("ROCK_BOTTOM", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Reach");

			AllAchievements.achievementList.Add(achievement);
			achievement = new AchievementData("MECHA_MAYHEM", "Challenger");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				21
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("GELATIN_WORLD_TOUR", "Challenger");
			achievement.AddCondition("NPCKilledCondition", new short[]
			{
				-5,
				-6,
				1,
				81,
				71,
				-3,
				147,
				138,
				-10,
				50,
				59,
				16,
				-7,
				244,
				-8,
				-1,
				-2,
				184,
				204,
				225,
				-9,
				141,
				183,
				-4
			}, true);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("FASHION_STATEMENT", "Collector");
			achievement.AddCondition("CustomFlagCondition", "Equip");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("VEHICULAR_MANSLAUGHTER", "Slayer");
			achievement.AddCondition("CustomFlagCondition", "Hit");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("BULLDOZER", "Challenger");
			achievement.AddCondition("CustomIntCondition", "Pick", 10000);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("THERE_ARE_SOME_WHO_CALL_HIM", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				45
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("DECEIVER_OF_FOOLS", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				196
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("SWORD_OF_THE_HERO", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				757
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("LUCKY_BREAK", "Challenger");
			achievement.AddCondition("CustomFlagCondition", "Hit");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("THROWING_LINES", "Challenger");
			achievement.AddCondition("CustomFlagCondition", "Use");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("DYE_HARD", "Challenger");
			achievement.AddCondition("CustomFlagCondition", "Equip");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("SICK_THROW", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				3389
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("FREQUENT_FLYER", "Challenger");
			achievement.AddCondition("CustomFloatCondition", "Pay", 10000f);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("THE_CAVALRY", "Collector");
			achievement.AddCondition("CustomFlagCondition", "Equip");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("COMPLETELY_AWESOME", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				98
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("TIL_DEATH", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				53
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("ARCHAEOLOGIST", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				52
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("PRETTY_IN_PINK", "Slayer");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				-4
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("RAINBOWS_AND_UNICORNS", "Challenger");
			achievement.AddCondition("CustomFlagCondition", "Use");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("YOU_AND_WHAT_ARMY", "Challenger");
			achievement.AddCondition("CustomFlagCondition", "Spawn");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("PRISMANCER", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				495
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("IT_CAN_TALK", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				18
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("WATCH_YOUR_STEP", "Explorer");
			achievement.AddCondition("CustomFlagCondition", "Hit");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("MARATHON_MEDALIST", "Challenger");
			achievement.AddCondition("CustomFloatCondition", "Move", 1106688f);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("GLORIOUS_GOLDEN_POLE", "Collector");
			achievement.AddCondition("ItemPickupCondition", new short[]
			{
				2294
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("SERVANT_IN_TRAINING", "Challenger");
			achievement.AddCondition("CustomFlagCondition", "Finish");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("GOOD_LITTLE_SLAVE", "Challenger");
			achievement.AddCondition("CustomIntCondition", "Finish", 10);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("TROUT_MONKEY", "Challenger");
			achievement.AddCondition("CustomIntCondition", "Finish", 25);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("FAST_AND_FISHIOUS", "Challenger");
			achievement.AddCondition("CustomIntCondition", "Finish", 50);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("SUPREME_HELPER_MINION", "Challenger");
			achievement.AddCondition("CustomIntCondition", "Finish", 200);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("TOPPED_OFF", "Challenger");
			achievement.AddCondition("CustomFlagCondition", "Use");
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("SLAYER_OF_WORLDS", "Challenger");
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				13,
				14,
				15
			});
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				113,
				114
			});
			achievement.AddCondition("NpcKilledCondition", new short[]
			{
				125,
				126
			});
			achievement.AddCondition("NPCKilledCondition", new short[]
			{
				4,
				35,
				50,
				222,
				113,
				134,
				127,
				262,
				245,
				439,
				398,
				370
			}, true);
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("YOU_CAN_DO_IT", "Explorer");
			achievement.AddCondition("ProgressionEventCondition", new short[]
			{
				1
			});
			AllAchievements.achievementList.Add(achievement);

			achievement = new AchievementData("MATCHING_ATTIRE", "Collector");
			achievement.AddCondition("CustomFlagCondition", "Equip");
			AllAchievements.achievementList.Add(achievement);
		}
	}
}