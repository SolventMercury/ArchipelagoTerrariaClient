using System;
using Terraria.ModLoader;
using System.Reflection;
using Terraria;
using Terraria.Achievements;
using Terraria.GameContent.Achievements;
using System.Collections.Generic;
using System.IO;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour.HookGen;

namespace ArchipelagoTerrariaClient {
	public class ArchipelagoTerrariaClient : Mod {
		public List<string> completedAchievements = new List<string>();

		public override void Load() {
			//Main.Achievements.OnAchievementCompleted += this.OnAchievementCompleted;
			//ReportAchievementProgress();
			// Hook our custom stuff into the achievement function.
			On.Terraria.Achievements.AchievementCondition.Complete += AchievementCompletionHook;
			AllAchievements.Load();
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

		/*public void LogAchievement(Achievement achievement) {
			string achievementLogLine = "Archipelago: Achievement '" + achievement.Name + "' was completed.";
			LogMessage(achievementLogLine);
		}*/

		private void LogMessage(string str) {
			string logPath = Path.Combine(Logging.LogDir, "ArchipelagoLog.txt");
			StreamWriter sw = new StreamWriter(logPath, append: true);
			sw.WriteLine(str);
			sw.Close();
		}

		/*public void ReportAchievementProgress() {
			AchievementManager achieveManager = Main.Achievements;
			List<Achievement> achievementList = achieveManager.CreateAchievementsList();
			LogMessage("Number of achievements: " + achievementList.Count.ToString());
			foreach (Achievement achieve in achievementList) {
				if (achieve.IsCompleted) {
					LogMessage("Achievement already obtained: " + achieve.Name + ".");
					//if (!completedAchievements.Contains(achieve.Name)) {
					//	completedAchievements.Add(Name);
					//}
				}
			}
		}*/

		// Example condition name:
		// ITEM_PICKUP_9
		// (Pick up item "Wood")
		private void AchievementCompletionHook (On.Terraria.Achievements.AchievementCondition.orig_Complete orig, AchievementCondition ac) {
			// Thanks to Black Sliver for this piece of code.
			// Really remarkable, could not have gotten this working without their help.
			Type typeOfAchvCond = null;
			typeOfAchvCond = ac.GetType();
			var achvEvent = typeOfAchvCond.BaseType.GetField("OnComplete", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic).GetValue(ac);
			if (achvEvent == null) {
				LogMessage("WARNING: Event handler not found - event likely has no subscribers.");
				return;
			}

			Terraria.Achievements.AchievementCondition.AchievementUpdate handler = (Terraria.Achievements.AchievementCondition.AchievementUpdate)achvEvent;
			var delegates = handler.GetInvocationList();
			if (delegates == null) {
				LogMessage("WARNING: Handler has no delegates.");
				return;
			}

			string achievementName = null;
			foreach (var d in delegates) {
				if (d == null) LogMessage("WARNING: Delegate is null (unexpected).");
				else if (d.Target == null) LogMessage("WARNING: Target is null (static method).");
				else if (d.Target.GetType() == null) LogMessage("WARNING: Target type is null (unexpected).");
				else {
					achievementName = ((Terraria.Achievements.Achievement)d.Target).Name;
					LogMessage("INFO: Target is Achievement named " + achievementName);
				}
			}

			// Check off this condition in our achievement name.
			if (achievementName != null && !completedAchievements.Contains(achievementName)) {
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
										LogMessage("ERROR: INVALID ACHIEVEMENT CONDITION DATA");
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

			
			orig(ac);
		}

		public void AwardAchievement (string achievementName) {
			completedAchievements.Add(achievementName);
			LogMessage("INFO: Player has completed achievement " + achievementName);
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