using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MeleeCombatExpansion
{
    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            var harmony = new Harmony("MeleeCombatExpansion.Mod");
            harmony.PatchAll();
        }

        public static int GetMeleeSkillLevel(this Pawn pawn) => pawn.skills?.GetSkill(SkillDefOf.Melee)?.levelInt ?? 0;
        public static float DamageModifier(DamageInfo dinfo, Verb verb)
        {
            var pawn = verb.CasterPawn;
            if (pawn != null)
            {
                var level = pawn.GetMeleeSkillLevel();
                return damageModifierPerSkill.Evaluate(level);
            }
            return 1f;
        }

        public static float ManipulationDamageBoostFrom(Pawn pawn)
        {
            var level = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
            return 1 + Mathf.Max((level - 1) / 5f, 0f);
        }

        public static SimpleCurve damageModifierPerSkill = new SimpleCurve
        {
            new CurvePoint(0f, 0.1f),
            new CurvePoint(4f, 0.4f),
            new CurvePoint(8f, 1f),
            new CurvePoint(16f, 1.5f),
            new CurvePoint(20f, 2f),
        };

        public static SimpleCurve meleeCooldownModifierPerSkill = new SimpleCurve
        {
            new CurvePoint(0f, 0.6f),
            new CurvePoint(4f, 0.8f),
            new CurvePoint(8f, 1f),
            new CurvePoint(16f, 1.25f),
            new CurvePoint(20f, 1.50f),
        };
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    public static class Patch_DamageInfosToApply
    {
        private static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> __result, Verb __instance, LocalTargetInfo target)
        {
            foreach (var info in __result)
            {
                info.SetAmount(info.Amount * Core.DamageModifier(info, __instance) * Core.ManipulationDamageBoostFrom(__instance.CasterPawn));
                yield return info;
            }
        }
    }

    [HarmonyPatch(typeof(Tool), "AdjustedCooldown")]
    public static class AdjustedCooldownPatch
    {
        private static void Postfix(ref float __result, Thing ownerEquipment)
        {
            if (ownerEquipment != null && ownerEquipment.ParentHolder != null && ownerEquipment.ParentHolder.ParentHolder != null)
            {
                if (ownerEquipment.ParentHolder.ParentHolder is Pawn pawn && ownerEquipment.def != null && ownerEquipment.def.IsMeleeWeapon)
                {
                    __result *= Core.meleeCooldownModifierPerSkill.Evaluate(pawn.GetMeleeSkillLevel());
                }
            }
        }
    }
}
