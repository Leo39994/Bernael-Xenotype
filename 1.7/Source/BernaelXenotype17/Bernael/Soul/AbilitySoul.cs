using System.Collections.Generic;
using RimWorld;
namespace Bernael_Xenotype
{
    public class AbilitySoul : Ability
    {
        public float SoulCost()
        {
            if (comps != null)
            {
                using (List<AbilityComp>.Enumerator enumerator = comps.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        CompAbilityEffect_SoulCost compAbilityEffect_HemogenCost;
                        if ((compAbilityEffect_HemogenCost = (enumerator.Current as CompAbilityEffect_SoulCost)) != null)
                        {
                            return compAbilityEffect_HemogenCost.Props.soulCost;
                        }
                    }
                }
            }
            return 0f;
        }
    }
}
