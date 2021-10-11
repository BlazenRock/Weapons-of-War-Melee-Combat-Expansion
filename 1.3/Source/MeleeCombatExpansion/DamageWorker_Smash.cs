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
	public class DamageWorker_Smash : DamageWorker_Blunt
	{
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var result = base.Apply(dinfo, thing);
            if (thing is Pawn victim)
            {
                TryToKnockBack(dinfo.Instigator, victim, 1);
            }
            return result;
        }

        private void TryToKnockBack(Thing attacker, Pawn victim, float knockBackDistance)
        {
            float distanceDiff = attacker.Position.DistanceTo(victim.Position) < knockBackDistance ? attacker.Position.DistanceTo(victim.Position) : knockBackDistance;
            Predicate<IntVec3> validator = delegate (IntVec3 x)
            {
                if (x.DistanceTo(victim.Position) < knockBackDistance)
                {
                    return false;
                }
                if (!x.Walkable(victim.Map) || !GenSight.LineOfSight(victim.Position, x, victim.Map))
                {
                    return false;
                }
                var attackerToVictimDistance = attacker.Position.DistanceTo(victim.Position);
                var attackerToCellDistance = attacker.Position.DistanceTo(x);
                var victimToCellDistance = victim.Position.DistanceTo(x);

                if (attackerToVictimDistance > attackerToCellDistance)
                {
                    return false;
                }
                if (attackerToCellDistance > victimToCellDistance + (distanceDiff - 1))
                {
                    return true;
                }
                else if (attacker.Position == victim.Position)
                {
                    return true;
                }
                return false;
            };
            var cells = GenRadial.RadialCellsAround(victim.Position, knockBackDistance, true).Where(x => validator(x));
            if (cells.TryRandomElement(out var cell))
            {
                Log.Message("Smashing " + victim + " from " + victim.Position + " to " + cell);
                victim.Position = cell;
                victim.pather.StopDead();
                victim.jobs.StopAll();
            }
            else
            {
                Log.Message("Smashing (stun) " + victim);
                victim.stances.stunner.StunFor(60, attacker);
            }
        }
    }
}
