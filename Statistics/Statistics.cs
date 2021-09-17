using RoR2;
using RoR2.Stats;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using BepInEx.Logging;

namespace Pyro
{
    [BepInPlugin("com.Pyro.Statistics", "Statistics", "1.0.0")]
    public class Statistics : BaseUnityPlugin
    {
        private ulong currentAvgDPS;

        private ulong currentTotalDamage;

        private ulong lastTotalDamage;

        private int N = 30;

        private Vector3 statsPosition = new Vector3(Screen.width * 0.1f, Screen.height * 0.35f, 0f);

        private Vector2 statsSize = new Vector2(250f, 250f);

        private LinkedList<ulong> lastNCombatDPS;

        private StatDef damageDealt;

        private LocalUser player;

        private StatsDisplay statsDisplay;

        public static ManualLogSource logger;
        
        private ConfigEntry<KeyboardShortcut> ShowStats { get; set; }

        public Statistics()
        {
            ShowStats = Config.AddSetting("Hotkeys", "Statistics mod - Show stats", new KeyboardShortcut(KeyCode.P, KeyCode.LeftShift));
        }

        public void Awake()
        {
            On.RoR2.CharacterBody.RecalculateStats += OnRecalculateStats;

            On.RoR2.Run.Start += OnRunStart;

            On.RoR2.Run.OnDestroy += OnRunEnd;

            On.RoR2.ArenaMissionController.EndRound += OnArenaRoundEnd;

            RoR2.GlobalEventManager.onClientDamageNotified += CheckDamage;
        }
        private void Update()
        {
            if (ShowStats.Value.IsDown() && statsDisplay)
            {
                statsDisplay.enabled = !statsDisplay.enabled;
                statsDisplay.GenericNotification.GetComponent<RectTransform>().sizeDelta = (statsDisplay.enabled) ? statsSize : new Vector2(0, 0);
                statsDisplay.Root.transform.position = (statsDisplay.enabled) ? statsPosition : new Vector3(0, 0);
            }
        }

        private void RollingStats()
        {
            ulong deltaDamage = currentTotalDamage - lastTotalDamage;

            if (deltaDamage <= 0) return;
            System.Console.WriteLine($"delta: {deltaDamage}");
            lastNCombatDPS.AddLast(deltaDamage);
            if (lastNCombatDPS.Count > N) lastNCombatDPS.RemoveFirst();  // Keep only last N values

            StringBuilder debugStr = new StringBuilder("", 200);
            ulong sum = 0;
            foreach (ulong dps in lastNCombatDPS)
            {
                debugStr.Append($" {dps}");
                sum += dps;
            }
            System.Console.WriteLine(debugStr.ToString());

            currentAvgDPS = sum / (ulong)lastNCombatDPS.Count;

            lastTotalDamage = currentTotalDamage;
        }

        public void CheckDamage(DamageDealtMessage damageDealtMessage)
        {
            if (damageDealtMessage.attacker && damageDealtMessage.attacker == player.cachedBodyObject)
            {
                currentTotalDamage += (ulong)damageDealtMessage.damage;

                System.Console.WriteLine($"Last: {lastTotalDamage}, Current: {currentTotalDamage}");
            }
        }

        private void OnArenaRoundEnd(On.RoR2.ArenaMissionController.orig_EndRound orig, RoR2.ArenaMissionController self)
        {
            orig(self);

            if (self.clearedEffect != null)
            {
                System.Console.WriteLine("clearedEffect is uninitialized");
            } 
            else
            {
                System.Console.WriteLine("Toggling clearedEffect");
                self.clearedEffect.SetActive(true);
            }
        }

        private void OnRunStart(On.RoR2.Run.orig_Start orig, RoR2.Run self)
        {
            orig(self);

            damageDealt = RoR2.Stats.StatDef.Find("totalDamageDealt");

            currentAvgDPS = 0;

            currentTotalDamage = 0;

            lastTotalDamage = 0;

            lastNCombatDPS = new LinkedList<ulong>();

            player = LocalUserManager.GetFirstLocalUser();

            InvokeRepeating("RollingStats", 1f, 1f);

            System.Console.WriteLine("Statistics plugin initialized");
        }

        private void OnRunEnd(On.RoR2.Run.orig_OnDestroy orig, RoR2.Run self)
        {
            CancelInvoke("RollingStats");

            orig(self);
        }

        private void OnRecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, RoR2.CharacterBody self)
        {
            orig(self);

            LocalUser player = LocalUserManager.GetFirstLocalUser();

            if (self != player.cachedBody) return;  // Skip anyone besides the local player

            if(statsDisplay == null && Run.instance)  // Create a new stats display if it hasn't been created
            {
                statsDisplay = player.cachedBody.gameObject.AddComponent<StatsDisplay>();
                statsDisplay.transform.SetParent(player.cachedBody.transform);
                statsDisplay.Root.transform.position = statsPosition;
                statsDisplay.Title = () => "Stats";
                statsDisplay.Body = () => "";
                statsDisplay.GenericNotification.fadeTime = 1f;
                statsDisplay.GenericNotification.duration = 86400f;
                statsDisplay.GenericNotification.GetComponent<RectTransform>().sizeDelta = statsSize;
            }
            if (statsDisplay)
            {
                float zeroBasedLevel = self.level - 1;
                StringBuilder bodyStr = new StringBuilder("", 200);

                float armorPercent = (self.armor >= 0f) ? (self.armor / (self.armor + 100f)) * 100 : (100f / (100f - self.armor) - 1) * 100;
                float baseArmor = self.baseArmor + self.levelArmor * zeroBasedLevel;
                float baseArmorPercent = (baseArmor >= 0f) ? (baseArmor / (baseArmor + 100f)) * 100 : (100f / (100f - baseArmor) - 1) * 100;

                // These calculations mirror the intermediate ones found in CharacterBody.RecalculateStats
                bodyStr.Append(String.Join(
                    Environment.NewLine,
                    "<style=cIsUtility>General</style>",
                    $"Move Spd: {self.moveSpeed} ({self.baseMoveSpeed + self.levelMoveSpeed * zeroBasedLevel})",
                    $"Regen: {self.regen} ({self.baseRegen + self.levelRegen * zeroBasedLevel})",
                    $"Armor: {Math.Round(armorPercent)}% ({Math.Round(baseArmor)}%)",
                    "<style=cIsUtility><color=\"orange\">Attack</color></style>",
                    $"Damage: {self.damage} ({self.baseDamage + self.levelDamage * zeroBasedLevel})",
                    $"Atk Spd: {self.attackSpeed} ({self.baseAttackSpeed + self.levelAttackSpeed * zeroBasedLevel})",
                    $"Crit: {self.crit} ({self.baseCrit + self.levelCrit * zeroBasedLevel}){Environment.NewLine}"
                    ));

                //bodyStr.Append(String.Join(
                //    Environment.NewLine,
                //    "<style=cIsUtility><color=\"blue\">Luck</color></style>",
                //    //$"Combat: {!self.outOfCombat}",
                //    $"Luck: {self.",
                //    ));

                bodyStr.Append(String.Join(
                    Environment.NewLine,
                    "<style=cIsUtility><color=\"red\">DPS</color></style>",
                    //$"Combat: {!self.outOfCombat}",
                    $"Total Dmg: {player.cachedStatsComponent.currentStats.GetStatDisplayValue(damageDealt)}",
                    $"Avg DPS: {currentAvgDPS}"
                    ));

                statsDisplay.Body = () => bodyStr.ToString();
            }
        }
        private void OnDestroy()
        {
            lastNCombatDPS = null;
        }
    }
}
