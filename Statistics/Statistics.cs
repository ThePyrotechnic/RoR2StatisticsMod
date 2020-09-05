using RoR2;
using RoR2.Stats;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using R2API.Utils;

namespace Pyro
{
    [BepInDependency("com.bepis.r2api")]
    //Change these
    [BepInPlugin("com.Pyro.Statistics", "Statistics", "1.0.0")]
    public class Statistics : BaseUnityPlugin
    {
        private ulong currentAvgDPS;

        private ulong lastTotalDamage;


        private const float outOfCombatDelay = 5f;

        private int N = 30;

        private Vector3 StatsPosition = new Vector3((Screen.width * 10f) / 100f, (Screen.height * 35f) / 100, 0f);

        private Vector2 StatsSize = new Vector2(250f, 250f);

        private LinkedList<ulong> LastNCombatDPS;

        private StatDef DamageDealt;

        private StatsDisplay StatsDisplay;

        private LocalUser Player;

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

            StatsPosition = new Vector3((Screen.width * 10f) / 100f, (Screen.height * 35f) / 100, 0f);
            StatsSize = new Vector2(250, 250);
        }
        private void Update()
        {
            if (ShowStats.Value.IsDown() && StatsDisplay)
            {
                StatsDisplay.enabled = !StatsDisplay.enabled;
                StatsDisplay.GenericNotification.GetComponent<RectTransform>().sizeDelta = (StatsDisplay.enabled) ? StatsSize : new Vector2(0, 0);
                StatsDisplay.Root.transform.position = (StatsDisplay.enabled) ? StatsPosition : new Vector3(0, 0);
            }
        }

        private void RollingStats()
        {
            ulong currentTotalDamage = Player.cachedStatsComponent.currentStats.GetStatValueULong(DamageDealt);
            ulong deltaDamage = currentTotalDamage - lastTotalDamage;

            if (deltaDamage <= 0) return;

            LastNCombatDPS.AddLast(deltaDamage);
            if (LastNCombatDPS.Count > N) LastNCombatDPS.RemoveFirst();  // Keep only last N values

            ulong sum = 0;
            foreach (ulong dps in LastNCombatDPS)
            {
                sum += dps;
            }
            currentAvgDPS = sum / (ulong)LastNCombatDPS.Count;

            lastTotalDamage = currentTotalDamage;
        }

        private void OnRunStart(On.RoR2.Run.orig_Start orig, RoR2.Run self)
        {
            orig(self);
            // Must reset all values since class is not re-awoken/instantiated

            DamageDealt = RoR2.Stats.StatDef.Find("totalDamageDealt");

            currentAvgDPS = 0;

            lastTotalDamage = 0;

            LastNCombatDPS = new LinkedList<ulong>();

            Player = LocalUserManager.GetFirstLocalUser();

            InvokeRepeating("RollingStats", 1f, 1f);
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

            if(StatsDisplay == null && Run.instance)  // Create a new stats display if it hasn't been created
            {
                StatsDisplay = player.cachedBody.gameObject.AddComponent<StatsDisplay>();
                StatsDisplay.transform.SetParent(player.cachedBody.transform);
                StatsDisplay.Root.transform.position = new Vector3((Screen.width * 10f) / 100f, (Screen.height * 35f) / 100, 0f);
                StatsDisplay.Title = () => "Stats";
                StatsDisplay.Body = () => "";
                StatsDisplay.GenericNotification.fadeTime = 1f;
                StatsDisplay.GenericNotification.duration = 86400f;
                StatsDisplay.GenericNotification.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 250);
            }
            if (StatsDisplay)
            {
                float zeroBasedLevel = self.level - 1;
                StringBuilder bodyStr = new StringBuilder("", 200);
                // These calculations mirror the intermediate ones found in CharacterBody.RecalculateStats
                bodyStr.Append(String.Join(
                    Environment.NewLine,
                    "<style=cIsUtility>General</style>",
                    $"Move Spd: {self.moveSpeed} ({self.baseMoveSpeed + self.levelMoveSpeed * zeroBasedLevel})",
                    $"Regen: {self.regen} ({self.baseRegen + self.levelRegen * zeroBasedLevel})",
                    "<style=cIsUtility><color=\"orange\">Attack</color></style>",
                    $"Damage: {self.damage} ({self.baseDamage + self.levelDamage * zeroBasedLevel})",
                    $"Atk Spd: {self.attackSpeed} ({self.baseAttackSpeed + self.levelAttackSpeed * zeroBasedLevel})",
                    $"Crit: {self.crit} ({self.baseCrit + self.levelCrit * zeroBasedLevel}){Environment.NewLine}"
                    ));
                //$"<style=cIsUtility><color=\"green\">Skills</color></style>{Environment.NewLine}"
                //if (self.skillLocator.primary)
                //{   // isBullets does not seem to do what I expect it to do (Captain's intra-shot cooldown is apparently not a bullet)
                //    if (self.skillLocator.primary.isBullets) bodyStr.Append($"Prim Cd: { self.skillLocator.primary.baseSkill.shootDelay}{Environment.NewLine})");
                //}
                //if (self.skillLocator.secondary)
                //{
                //    if (self.skillLocator.secondary.isBullets) bodyStr.Append($"Sec Cd: { self.skillLocator.secondary.baseSkill.shootDelay}{Environment.NewLine}");
                //}
                //if (self.skillLocator.special) bodyStr.Append($"Spcl Cd: {self.skillLocator.special.CalculateFinalRechargeInterval()}{Environment.NewLine}");

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
                    $"Total Dmg: {player.cachedStatsComponent.currentStats.GetStatDisplayValue(DamageDealt)}",
                    $"Avg DPS: {currentAvgDPS}"
                    ));

                StatsDisplay.Body = () => bodyStr.ToString();
            }
        }

        private void OnDestroy()
        {
            LastNCombatDPS = null;
        }
    }
}
