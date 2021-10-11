using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MeleeCombatExpansion
{
    [DefOf]
    public static class MCE_DefOf
    {
        public static StatDef WW_MCE_MeleeWeaponReachRange;
        public static StatDef WW_MCE_MeleeReachRange;
    }
    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            var harmony = new Harmony("MeleeCombatExpansion.Mod");
            harmony.PatchAll();

            var gotoCastPositionMethod = typeof(Toils_Combat).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                .FirstOrDefault(method => method.Name.Contains("<GotoCastPosition>") && method.ReturnType == typeof(void) && method.GetParameters().Length == 0);
            harmony.Patch(gotoCastPositionMethod, transpiler: new HarmonyMethod(AccessTools.Method(typeof(Core), nameof(GotoCastPositionTranspiler))));

            var tryFindShootLineFromToMethod = AccessTools.Method(typeof(Verb), "TryFindShootLineFromTo");
            harmony.Patch(tryFindShootLineFromToMethod, transpiler: new HarmonyMethod(AccessTools.Method(typeof(Core), nameof(TryFindShootLineFromToTranspiler))));

            Log.Message("Done");
            var applyMeleeDamageToTargetMethod = AccessTools.Method(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget");
            harmony.Patch(applyMeleeDamageToTargetMethod, transpiler: new HarmonyMethod(AccessTools.Method(typeof(Core), nameof(ApplyMeleeDamageToTargetTranspiler))));

            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (thingDef.IsWeapon && !thingDef.StatBaseDefined(MCE_DefOf.WW_MCE_MeleeWeaponReachRange))
                {
                    thingDef.SetStatBaseValue(MCE_DefOf.WW_MCE_MeleeWeaponReachRange, ShootTuning.MeleeRange);
                }
                else if (thingDef.race != null && !thingDef.StatBaseDefined(MCE_DefOf.WW_MCE_MeleeReachRange))
                {
                    thingDef.SetStatBaseValue(MCE_DefOf.WW_MCE_MeleeReachRange, ShootTuning.MeleeRange);
                }
            }
        }

        public static IEnumerable<CodeInstruction> GotoCastPositionTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4 && codes[i].OperandIs(ShootTuning.MeleeRange))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Job), "verbToUse"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Core), nameof(GetMeleeReachRange)));
                }
                else
                {
                    yield return codes[i];
                }
            }
        }
        public static IEnumerable<CodeInstruction> TryFindShootLineFromToTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var label = generator.DefineLabel();
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Brtrue_S && codes[i - 1].Calls(AccessTools.Method(typeof(VerbProperties), "get_IsMeleeAttack")))
                {
                    codes[i + 1].labels.Add(label);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, label);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Core), nameof(IsVanillaMeleeAttack)));
                    yield return new CodeInstruction(OpCodes.Brtrue_S, codes[i].operand);
                }
                else
                {
                    yield return codes[i];
                }
            }
        }
        public static IEnumerable<CodeInstruction> ApplyMeleeDamageToTargetTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            for (var i = 0; i < codes.Count; i++)
            {
                yield return codes[i];
                if (codes[i].opcode == OpCodes.Stloc_0 && codes[i - 1].Calls(AccessTools.Method(typeof(Thing), "TakeDamage")))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldarga_S, 1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LocalTargetInfo), "get_Thing"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Core), "DoAdditionalDamage"));
                }
            }
        }

        private static void DoAdditionalDamage(Verb verb, DamageInfo dinfo, DamageWorker.DamageResult damageResult, Thing thing)
        {
            if (thing is Pawn pawn && verb.tool is Tool_Adv tool)
            {
                if (tool.apparelShredDamage != null && tool.apparelShredDamage.max > 0 && damageResult.parts?.Count > 0)
                {
                    var apparels = damageResult.parts.SelectMany(part => pawn.apparel?.WornApparel?.Where(x => x.def.apparel.CoversBodyPart(part))).Distinct().ToList();
                    if (apparels != null)
                    {
                        foreach (var apparel in apparels)
                        {
                            apparel.TakeDamage(new DamageInfo(dinfo.Def, tool.apparelShredDamage.RandomInRange, dinfo.ArmorPenetrationInt, dinfo.Angle,
                                dinfo.Instigator, dinfo.HitPart, dinfo.Weapon, DamageInfo.SourceCategory.ThingOrUnknown));
                            Log.Message("Damaging " + apparel + " - " + apparel.HitPoints);
                            if (apparel.HitPoints <= 0)
                            {
                                apparel.Destroy();
                            }
                        }
                    }
                }
                if (thing is Pawn victim && tool.stunChance.HasValue && Rand.Chance(tool.stunChance.Value))
                {
                    Log.Message("Stunning " + victim);
                    victim.stances.stunner.StunFor(tool.stunDuration.RandomInRange, verb.caster);
                }
            }
        }

        public static bool IsVanillaMeleeAttack(Verb verb)
        {
            if (verb.Caster is Pawn pawn && pawn.GetMeleeReachRange(verb) > ShootTuning.MeleeRange)
            {
                return false;
            }
            return true;
        }

        public static float GetMeleeReachRange(this Pawn caster, Verb verb)
        {
            var equipment = verb?.EquipmentSource;
            if (equipment != null && equipment.def.IsMeleeWeapon)
            {
                var reach = Mathf.Max(caster.GetStatValue(MCE_DefOf.WW_MCE_MeleeReachRange), equipment.GetStatValue(MCE_DefOf.WW_MCE_MeleeWeaponReachRange));
                if (verb?.tool is Tool_Adv tool)
                {
                    reach += tool.advancedMeleeReachBonus;
                }
                return reach;
            }
            else if (equipment is null)
            {
                return caster.GetStatValue(MCE_DefOf.WW_MCE_MeleeReachRange);
            }
            return ShootTuning.MeleeRange;
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

    [HarmonyPatch(typeof(Toils_Combat), "FollowAndMeleeAttack", new Type[] { typeof(TargetIndex), typeof(TargetIndex), typeof(Action) })]
    public static class Patch_FollowAndMeleeAttack
    {
        private static bool Prefix(ref Toil __result, TargetIndex targetInd, TargetIndex standPositionInd, Action hitAction)
        {
            __result = FollowAndMeleeAttackModified(targetInd, standPositionInd, hitAction);
            return false;
        }

        public static Toil FollowAndMeleeAttackModified(TargetIndex targetInd, TargetIndex standPositionInd, Action hitAction)
        {
            Toil followAndAttack = new Toil();
            followAndAttack.tickAction = delegate
            {
                Pawn actor = followAndAttack.actor;
                Job curJob = actor.jobs.curJob;
                JobDriver curDriver = actor.jobs.curDriver;
                LocalTargetInfo target = curJob.GetTarget(targetInd);
                Thing thing = target.Thing;
                Pawn pawn = thing as Pawn;
                if (!thing.Spawned || (pawn != null && pawn.IsInvisible()))
                {
                    curDriver.ReadyForNextToil();
                }
                else
                {
                    var verbToUse = curJob.verbToUse ?? actor.meleeVerbs.curMeleeVerb ?? actor.meleeVerbs.TryGetMeleeVerb(thing);
                    var meleeReachRange = actor.GetMeleeReachRange(verbToUse);
                    if (actor.Position.DistanceTo(thing.Position) > meleeReachRange)
                    {
                        CastPositionRequest newReq = default(CastPositionRequest);
                        newReq.caster = followAndAttack.actor;
                        newReq.target = thing;
                        newReq.verb = verbToUse;
                        newReq.maxRangeFromTarget = meleeReachRange;
                        newReq.wantCoverFromTarget = false;
                        verbToUse.verbProps.range = meleeReachRange;
                        if (!CastPositionFinder.TryFindCastPosition(newReq, out var dest))
                        {
                            followAndAttack.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                        }
                        else
                        {
                            followAndAttack.actor.pather.StartPath(dest, PathEndMode.OnCell);
                            actor.Map.pawnDestinationReservationManager.Reserve(actor, curJob, dest);
                        }
                    }
                    else
                    {
                        if (pawn != null && pawn.Downed && !curJob.killIncappedTarget)
                        {
                            curDriver.ReadyForNextToil();
                        }
                        else
                        {
                            hitAction();
                        }
                    }
                }
            };
            followAndAttack.activeSkill = () => SkillDefOf.Melee;
            followAndAttack.defaultCompleteMode = ToilCompleteMode.Never;
            return followAndAttack;
        }
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

    [HarmonyPatch(typeof(Tool), "AdjustedCooldown", new Type[] { typeof(Thing)} )]
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
