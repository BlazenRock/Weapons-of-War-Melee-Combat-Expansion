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
	public class DamageWorker_Pierce : DamageWorker_Stab
	{
        public static bool preventRecursion;
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var result = base.Apply(dinfo, thing);
            if (!preventRecursion && result.totalDamageDealt > 0 && dinfo.Instigator != null)
            {
                Log.Message("dinfo.Instigator: " + dinfo.Instigator);
                preventRecursion = true;
                var pos = thing.Position + dinfo.Instigator.Rotation.FacingCell;
                var secondaryThings = pos.GetThingList(thing.Map).Concat(thing.Position.GetThingList(thing.Map)).Where(x => (x is Pawn || x is Building) && x != thing).ToList();
                foreach (var t in secondaryThings)
                {
                    Log.Message("Main target: " + thing + ", piercing " + t);
                    t.TakeDamage(new DamageInfo(dinfo.Def, dinfo.Amount / 2, dinfo.ArmorPenetrationInt, dinfo.Angle, dinfo.Instigator, null, dinfo.Weapon, DamageInfo.SourceCategory.ThingOrUnknown));
                }
                preventRecursion = false;
            }
            return result;
        }
    }
}
