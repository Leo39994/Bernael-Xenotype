using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public static class VanquishBootstrap
    {
        static VanquishBootstrap()
        {
            new Harmony("Font.BernaelXenotype.Vanquish").PatchAll(typeof(VanquishBootstrap).Assembly);
        }
    }

    public class CompProperties_AbilityVanquish : CompProperties_AbilityEffect
    {
        public float healthThreshold = 0.5f;
        public float baseDamageMultiplier = 3f;
        public float damageMultiplier = 5f;
        public float psyfocusGain = 0.2f;
        public string requiredWeaponDefName;

        public CompProperties_AbilityVanquish()
        {
            compClass = typeof(CompAbilityEffect_Vanquish);
        }
    }

    public class Ability_Vanquish : Ability
    {
        public Ability_Vanquish()
        {
        }

        public Ability_Vanquish(Pawn pawn)
            : base(pawn)
        {
        }

        public Ability_Vanquish(Pawn pawn, AbilityDef def)
            : base(pawn, def)
        {
        }

        public Ability_Vanquish(Pawn pawn, Precept sourcePrecept, AbilityDef def)
            : base(pawn, sourcePrecept, def)
        {
        }

        internal void Notify_KilledTarget()
        {
            ResetCooldown();
            Pawn_PsychicEntropyTracker entropy = pawn.psychicEntropy;
            if (entropy != null)
            {
                CompAbilityEffect_Vanquish effect = CompOfType<CompAbilityEffect_Vanquish>();
                entropy.OffsetPsyfocusDirectly(effect?.Props.psyfocusGain ?? 0.2f);
                entropy.RemoveAllEntropy();
            }
        }
    }

    public class Verb_CastAbilityVanquish : Verb_CastAbilityTouch
    {
        public override void WarmupComplete()
        {
            Pawn caster = CasterPawn;
            if (caster?.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                caster.stances.SetStance(new Stance_Mobile());
            }

            base.WarmupComplete();
        }
    }

    public class CompAbilityEffect_Vanquish : CompAbilityEffect
    {
        public new CompProperties_AbilityVanquish Props => (CompProperties_AbilityVanquish)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;
            if (victim == null || victim.Dead || !victim.HostileTo(caster))
            {
                if (throwMessages)
                {
                    Messages.Message("BX_VanquishHostilePawnOnly".Translate(), MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon == null || weapon.def.defName != Props.requiredWeaponDefName)
            {
                if (throwMessages)
                {
                    Messages.Message("BX_VanquishRequiresLifeVanquisher".Translate(), MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            return FindWeaponVerb(caster, weapon) != null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;
            ThingWithComps weapon = caster.equipment?.Primary;
            if (victim == null || victim.Dead || weapon == null || weapon.def.defName != Props.requiredWeaponDefName)
            {
                return;
            }

            Verb verb = FindWeaponVerb(caster, weapon);
            if (verb == null)
            {
                return;
            }

            float multiplier = victim.health.summaryHealth.SummaryHealthPercent < Props.healthThreshold
                ? Props.damageMultiplier
                : Props.baseDamageMultiplier;

            bool attacked;
            using (VanquishDamageContext.Push(verb, multiplier))
            {
                attacked = caster.meleeVerbs.TryMeleeAttack(victim, verb, true);
            }

            if (attacked && victim.Dead)
            {
                (parent as Ability_Vanquish)?.Notify_KilledTarget();
            }
        }

        internal static Verb FindWeaponVerb(Pawn caster, ThingWithComps weapon)
        {
            List<VerbEntry> verbs = caster.meleeVerbs.GetUpdatedAvailableVerbsList(false);
            Verb bestVerb = null;
            float bestDamage = float.MinValue;

            for (int index = 0; index < verbs.Count; index++)
            {
                Verb verb = verbs[index].verb;
                if (verb is not Verb_MeleeAttackDamage || verb.EquipmentSource != weapon)
                {
                    continue;
                }

                float damage = verb.verbProps.AdjustedMeleeDamageAmount(verb, caster);
                if (damage <= bestDamage)
                {
                    continue;
                }

                bestVerb = verb;
                bestDamage = damage;
            }

            return bestVerb;
        }

    }

    internal static class VanquishDamageContext
    {
        [ThreadStatic]
        private static Verb activeVerb;

        [ThreadStatic]
        private static float activeMultiplier;

        public static Scope Push(Verb verb, float multiplier)
        {
            return new Scope(verb, multiplier);
        }

        public static bool TryGetMultiplier(Verb verb, out float multiplier)
        {
            multiplier = activeMultiplier;
            return verb == activeVerb && multiplier > 1f;
        }

        internal readonly struct Scope : IDisposable
        {
            private readonly Verb previousVerb;
            private readonly float previousMultiplier;

            public Scope(Verb verb, float multiplier)
            {
                previousVerb = activeVerb;
                previousMultiplier = activeMultiplier;
                activeVerb = verb;
                activeMultiplier = multiplier;
            }

            public void Dispose()
            {
                activeVerb = previousVerb;
                activeMultiplier = previousMultiplier;
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    public static class VanquishDamagePatch
    {
        public static void Postfix(Verb_MeleeAttackDamage __instance, ref IEnumerable<DamageInfo> __result)
        {
            if (!VanquishDamageContext.TryGetMultiplier(__instance, out float multiplier))
            {
                return;
            }

            __result = ScaleDamage(__result, multiplier);
        }

        private static IEnumerable<DamageInfo> ScaleDamage(IEnumerable<DamageInfo> damageInfos, float multiplier)
        {
            foreach (DamageInfo damageInfo in damageInfos)
            {
                DamageInfo scaledDamage = damageInfo;
                scaledDamage.SetAmount(damageInfo.Amount * multiplier);
                scaledDamage.SetIgnoreArmor(true);
                yield return scaledDamage;
            }
        }
    }
}
