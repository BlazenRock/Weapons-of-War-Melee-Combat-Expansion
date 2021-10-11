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
	public class Tool_Adv : Tool
	{
		public float advancedMeleeReachBonus;
		public FloatRange apparelShredDamage;
		public float? stunChance;
		public IntRange stunDuration;
    }
}
