using RimWorld;
using UnityEngine;
using Verse;
namespace Bernael_Xenotype
{
    public class Verb_CastAbilitySoul : Verb_CastAbility
    {
        public static new Color RadiusHighlightColor =>  new Color(0.3f, 0.8f, 1f);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ability, "ability");
        }

        public new AbilitySoul ability;
    }
}
