﻿using System;
using System.Collections.Generic;
using System.Linq;
using jesuisFiora.Properties;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;
using ItemData = LeagueSharp.Common.Data.ItemData;

namespace jesuisFiora
{
    internal static class Program
    {
        public static Orbwalking.Orbwalker Orbwalker;
        public static Spell Q, W, E, R;
        public static Menu Menu;

        private static readonly List<string> FioraPassive = new List<string>
        {
            "Fiora_Base_Passive_NE.troy",
            "Fiora_Base_Passive_SE.troy",
            "Fiora_Base_Passive_NW.troy",
            "Fiora_Base_Passive_SW.troy",
            "Fiora_Base_Passive_NE_Timeout.troy",
            "Fiora_Base_Passive_SE_Timeout.troy",
            "Fiora_Base_Passive_NW_Timeout.troy",
            "Fiora_Base_Passive_SW_Timeout.troy"
        };

        public static readonly List<string> FioraUltPassive = new List<string>
        {
            "Fiora_Base_R_Mark.troy",
            "Fiora_Base_R.troy"
        };

        public static List<GameObject> FioraUltPassiveObjects
        {
            get
            {
                return
                    ObjectManager.Get<GameObject>()
                        .Where(obj => obj.IsValid && FioraUltPassive.Contains(obj.Name))
                        .ToList();
            }
        }

        public static List<GameObject> FioraPassiveObjects
        {
            get
            {
                return
                    ObjectManager.Get<GameObject>()
                        .Where(obj => obj.IsValid && FioraPassive.Contains(obj.Name))
                        .ToList();
            }
        }

        private static Obj_AI_Hero Player
        {
            get { return ObjectManager.Player; }
        }

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (Player.ChampionName != "Fiora")
            {
                return;
            }

            Q = new Spell(SpellSlot.Q, 400 + 175);
            Q.SetSkillshot(.25f, 0, 500, false, SkillshotType.SkillshotLine);

            W = new Spell(SpellSlot.W, 750);
            W.SetSkillshot(0.5f, 95, 500, false, SkillshotType.SkillshotLine);

            E = new Spell(SpellSlot.E);

            R = new Spell(SpellSlot.R, 500);
            R.SetTargetted(.066f, 500);


            Menu = new Menu("jesuisFiora", "jesuisFiora", true);

            Orbwalker = Menu.AddOrbwalker();

            Menu.AddTargetSelector();

            var comboMenu = Menu.AddMenu("Combo", "Combo");
            comboMenu.AddBool("QCombo", "Use Q");
            comboMenu.AddBool("WCombo", "Use W");
            comboMenu.AddBool("ECombo", "Use E");
            comboMenu.AddBool("RCombo", "Use R");
            comboMenu.AddBool("RComboSelected", "Use R Selected Only");
            comboMenu.AddBool("ItemsCombo", "Use Items");

            var harassMenu = Menu.AddMenu("Harass", "Harass");
            harassMenu.AddBool("QHarass", "Use Q");
            harassMenu.AddBool("WHarass", "Use W");
            harassMenu.AddBool("EHarass", "Use E");
            harassMenu.AddBool("ItemsHarass", "Use Items");
            harassMenu.AddSlider("ManaHarass", "Min Mana Percent", 40);

            var farmMenu = Menu.AddMenu("Farm", "Farm");
            farmMenu.AddBool("QLastHit", "Q Last Hit (Only Killable)");
            farmMenu.AddBool("QLaneClear", "Q LaneClear (All)");
            farmMenu.AddSlider("QFarmMana", "Q Min Mana Percent", 40);
            farmMenu.AddBool("ELaneClear", "Use E LaneClear");
            farmMenu.AddBool("ItemsLaneClear", "Use Items");

            var miscMenu = Menu.AddMenu("Misc", "Misc");

            var qMisc = miscMenu.AddMenu("Q", "Q");
            qMisc.AddBool("QGapClose", "Q Flee on Gapclose");

            var wMisc = miscMenu.AddMenu("W", "W");
            wMisc.AddBool("WSpells", "W Incoming Spells");

            var rMisc = miscMenu.AddMenu("R", "R (SOON)");
            rMisc.AddBool("RKill", "Duelist Mode (SOON)");
            rMisc.AddSlider("RKillVital", "Duelist Mode Min Vitals", 1, 0, 4);

            miscMenu.AddBool("Sounds", "Sounds");

            var drawMenu = Menu.AddMenu("Drawing", "Drawing");
            drawMenu.AddBool("QDraw", "Draw Q");
            drawMenu.AddBool("WDraw", "Draw W");
            drawMenu.AddBool("RDraw", "Draw R");

            Menu.AddToMainMenu();

            if (miscMenu.Item("Sounds").GetValue<bool>())
            {
                var sound = new SoundObject(Resources.OnLoad);
                sound.Play();
            }

            Game.OnUpdate += Game_OnGameUpdate;
            Orbwalking.AfterAttack += AfterAttack;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Drawing.OnDraw += Drawing_OnDraw;
            Game.PrintChat("jesuisFiora Loaded!");
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }

            AutoUltMode();

            var target = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
            var mode = Orbwalker.ActiveMode;
            var combo = mode.Equals(Orbwalking.OrbwalkingMode.Combo) || mode.Equals(Orbwalking.OrbwalkingMode.Mixed);

            if (target == null || !target.IsValidTarget(W.Range))
            {
                return;
            }


            if (combo)
            {
                var comboMode = mode.Equals(Orbwalking.OrbwalkingMode.Combo) ? "Combo" : "Harass";
                var qCombo = Menu.Item("Q" + comboMode).IsActive();
                var wCombo = Menu.Item("W" + comboMode).IsActive();
                var rCombo = Menu.Item("R" + comboMode).IsActive();

                if (comboMode.Equals("Harass") && Player.ManaPercent < Menu.Item("ManaHarass").GetValue<Slider>().Value)
                {
                    return;
                }

                if (wCombo && !target.IsValidTarget(Q.Range) && CastW(target))
                {
                    return;
                }

                if (qCombo && CastQ(target))
                {
                    return;
                }

                if (Menu.Item("Items" + comboMode).IsActive() && CastItems())
                {
                    return;
                }

                if (wCombo && CastW(target))
                {
                    return;
                }

                if (mode.Equals(Orbwalking.OrbwalkingMode.Combo) && rCombo)
                {
                    if (Menu.Item("RComboSelected").IsActive())
                    {
                        if (Hud.SelectedUnit.Equals(target) && CastR(target))
                        {
                            return;
                        }
                        return;
                    }

                    CastR(target);
                }
            }

            if (mode.Equals(Orbwalking.OrbwalkingMode.LastHit) && Menu.Item("QLastHit").IsActive() &&
                Player.ManaPercent >= Menu.Item("QFarmMana").GetValue<Slider>().Value)
            {
                foreach (var obj in
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Where(
                            minion =>
                                minion.IsValidTarget(Q.Range) && MinionManager.IsMinion(minion) &&
                                minion.Health < Player.GetSpellDamage(minion, SpellSlot.Q)))
                {
                    Q.Cast(obj);
                }
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) {}
        }

        private static void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender == null || !sender.IsValid)
            {
                return;
            }

            if (sender.IsMe && args.SData.Name.Equals("FioraE"))
            {
                Orbwalking.ResetAutoAttackTimer();
                return;
            }

            var blockableTypes = new List<SpellDataTargetType>
            {
                SpellDataTargetType.SelfAndUnit,
                SpellDataTargetType.Unit
            };

            if (sender.IsEnemy && args.Target != null && args.Target.IsMe && Menu.Item("WSpell").IsActive() &&
                W.IsReady() && blockableTypes.Contains(args.SData.TargettingType))
            {
                W.Cast(sender);
            }
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (Menu.Item("QGapClose").IsActive() && Q.IsReady() && gapcloser.End.Distance(Player.ServerPosition) < 100)
            {
                //maybe cast w too
                Q.Cast(
                    gapcloser.End.Extend(
                        Player.ServerPosition, gapcloser.End.Distance(Player.ServerPosition) + Q.Range + 20));
            }
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }

            if (Menu.Item("QDraw").IsActive())
            {
                Render.Circle.DrawCircle(Player.Position, Q.Range, Color.Purple, 7);
            }

            if (Menu.Item("WDraw").IsActive())
            {
                Render.Circle.DrawCircle(Player.Position, W.Range, Color.DeepPink);
            }

            if (Menu.Item("RDraw").IsActive())
            {
                Render.Circle.DrawCircle(Player.Position, R.Range, Color.White, 7);
            }
        }

        private static void AfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (!unit.IsMe)
            {
                return;
            }

            var mode = Orbwalker.ActiveMode;

            if (mode.Equals(Orbwalking.OrbwalkingMode.None) || mode.Equals(Orbwalking.OrbwalkingMode.LastHit))
            {
                return;
            }

            var comboMode = mode.ToString().Equals("Mixed") ? "Harass" : mode.ToString();

            if (Menu.Item("E" + comboMode).IsActive() && E.IsReady())
            {
                E.Cast();
            }

            if (Menu.Item("Items" + comboMode).IsActive())
            {
                Utility.DelayAction.Add((int) (E.Delay * 1000f + Game.Ping / 2f + 20), () => CastItems());
            }
        }

        public static void AutoUltMode()
        {
            if (!Menu.Item("RKill").IsActive() || !R.IsReady() || Player.CountEnemiesInRange(R.Range) == 0)
            {
                return;
            }

            if (
                ObjectManager.Get<Obj_AI_Hero>()
                    .Where(obj => obj.IsValid && obj.IsValidTarget(R.Range))
                    .Any(
                        enemy =>
                            enemy.GetVitalCount() >= Menu.Item("RKillVital").GetValue<Slider>().Value &&
                            Player.GetDamageSpell(enemy, SpellSlot.R).CalculatedDamage >= enemy.Health &&
                            R.Cast(enemy).IsCasted()))
            {
                return;
            }
        }

        public static bool CastQ(Obj_AI_Base target)
        {
            if (!Q.IsReady() || !target.IsValidTarget(Q.Range))
            {
                return false;
            }

            if (CountPassive(target) == 0)
            {
                return Q.Cast(target).IsCasted();
            }

            var pos = GetPassivePosition(target);
            return pos.Equals(Vector3.Zero) ? Q.Cast(target).IsCasted() : Q.Cast(pos);
        }

        public static bool CastW(Obj_AI_Base target)
        {
            return W.IsReady() && target.IsValidTarget(W.Range) && W.Cast(target).IsCasted();
        }

        public static bool CastR(Obj_AI_Base target)
        {
            return R.IsReady() && target.IsValidTarget(R.Range) && R.Cast(target).IsCasted();
        }

        public static bool CastItems()
        {
            if (ItemData.Tiamat_Melee_Only.GetItem().IsReady() && ItemData.Tiamat_Melee_Only.GetItem().Cast())
            {
                return true;
            }

            if (ItemData.Ravenous_Hydra_Melee_Only.GetItem().IsReady() &&
                ItemData.Ravenous_Hydra_Melee_Only.GetItem().Cast())
            {
                return true;
            }

            return false;
        }

        public static int CountPassive(Obj_AI_Base target)
        {
            return FioraPassiveObjects.Count(obj => obj.Position.Distance(target.ServerPosition) <= 50) +
                   FioraUltPassiveObjects.Count(obj => obj.Position.Distance(target.ServerPosition) <= 50);
        }

        public static Vector3 GetPassivePosition(Obj_AI_Base target)
        {
            var ultPassive =
                FioraUltPassiveObjects.OrderBy(obj => obj.Position.Distance(target.ServerPosition)).FirstOrDefault();

            if (ultPassive != null)
            {
                return ultPassive.Position;
            }

            var passive = FioraPassiveObjects.FirstOrDefault(obj => obj.Position.Distance(target.ServerPosition) < 50);

            if (passive == null)
            {
                return new Vector3();
            }

            var pos = Prediction.GetPrediction(target, Q.Range).UnitPosition.To2D();

            if (passive.Name.Contains("NE"))
            {
                pos.Y += 200;
            }

            if (passive.Name.Contains("SE"))
            {
                pos.X -= 200;
            }

            if (passive.Name.Contains("NW"))
            {
                pos.X += 200;
            }

            if (passive.Name.Contains("SW"))
            {
                pos.Y -= 200;
            }

            return pos.To3D();
        }
    }
}