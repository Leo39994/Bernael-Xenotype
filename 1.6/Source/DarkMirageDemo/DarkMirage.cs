using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Bernael.DarkMirageDemo
{
    public sealed class MirageSettings : DefModExtension
    {
        public int durationTicks = 600;
        public float burstRadius = 2.4f;
        public float frostbiteDamage = 4f;
        public float targetPriority = 2.5f;
    }

    public sealed class CompProperties_DarkMirage : CompProperties_AbilityEffect
    {
        public ThingDef mirageDef;
        public CompProperties_DarkMirage() { compClass = typeof(CompAbilityEffect_DarkMirage); }
    }

    public sealed class CompAbilityEffect_DarkMirage : CompAbilityEffect
    {
        private CompProperties_DarkMirage Settings => (CompProperties_DarkMirage)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!MirageVisuals.ShaderReady(Settings.mirageDef))
            {
                if (throwMessages) Messages.Message("BX_MirageShaderMissing".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            bool valid = parent.pawn.Spawned && parent.pawn.Faction != null &&
                MirageUtility.CanSpawnAt(target.Cell, parent.pawn.Map);
            if (!valid && throwMessages)
                Messages.Message("BX_MirageBlocked".Translate(), MessageTypeDefOf.RejectInput, false);
            return valid;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // Recheck after warmup; never let GenSpawn wipe a newly occupied cell.
            if (!Valid(target, true)) return;
            base.Apply(target, dest);
            MirageUtility.Spawn(parent.pawn, target.Cell, Settings.mirageDef);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (target.Cell.InBounds(parent.pawn.Map))
                GenDraw.DrawRadiusRing(target.Cell, Settings.mirageDef.GetModExtension<MirageSettings>().burstRadius);
        }
    }

    public static class MirageUtility
    {
        public static bool CanSpawnAt(IntVec3 cell, Map map)
        {
            return map != null && cell.InBounds(map) && !cell.Fogged(map) && cell.Standable(map) &&
                cell.GetEdifice(map) == null && !cell.GetThingList(map).Any(t =>
                    t is Pawn || t is Building || t is Fire || t is Blueprint || t is Frame);
        }

        public static DarkMirage Spawn(Pawn caster, IntVec3 cell, ThingDef def)
        {
            if (caster == null || !caster.Spawned || caster.Faction == null || !CanSpawnAt(cell, caster.Map)) return null;
            // One mirage per caster across all loaded maps. Recasting dismisses without damage.
            foreach (Map map in Find.Maps)
                foreach (DarkMirage old in map.listerThings.ThingsOfDef(def).OfType<DarkMirage>().ToList())
                    if (old.Caster == caster) old.Dismiss();
            var mirage = (DarkMirage)ThingMaker.MakeThing(def);
            mirage.Caster = caster;
            mirage.SetFaction(caster.Faction);
            GenSpawn.Spawn(mirage, cell, caster.Map, caster.Rotation);
            return mirage;
        }
    }

    public sealed class DarkMirage : Building, IAttackTarget
    {
        public Pawn Caster;
        private int createdTick;
        private int expiresTick;
        private string snapshotPng;
        private Vector4 eyeA;
        private Vector4 eyeB;
        private MirageVisuals visuals;
        private bool visualFailed;
#if MIRAGE_QA
        public int QaDrawCalls;
#endif
        private bool suppressBurst;
        private bool burstDone;
        private MirageSettings Settings => def.GetModExtension<MirageSettings>();
        Thing IAttackTarget.Thing => this;
        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;
        public float TargetPriorityFactor => Settings.targetPriority;
        public bool ThreatDisabled(IAttackTargetSearcher disabledFor) => !Spawned || Destroyed;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                createdTick = Find.TickManager.TicksGame;
                expiresTick = createdTick + Settings.durationTicks;
            }
            if (!respawningAfterLoad) EnsureVisuals();
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Spawned) return;
            // Owner loss or faction change is cleanup, not a free offensive detonation.
            if (Caster == null || Caster.Dead || !Caster.Spawned || Caster.Map != Map || Caster.Faction != Faction)
            {
                Dismiss();
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (now >= expiresTick)
            {
                Destroy();
                return;
            }
        }

        private void EnsureVisuals()
        {
            if (visuals != null || visualFailed || Caster == null) return;
            try
            {
                visuals = new MirageVisuals();
                visuals.Initialize(this, ref snapshotPng, ref eyeA, ref eyeB);
            }
            catch (System.Exception error)
            {
                visualFailed = true;
                visuals?.Dispose();
                visuals = null;
                Log.Error("[Dark Mirage] Caster capture failed: " + error);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
#if MIRAGE_QA
            QaDrawCalls++;
#endif
            int now = Find.TickManager.TicksGame;
            // Spawn is revealed by the shader; retain the existing 36-tick fade-out.
            float opacity = Mathf.Clamp01((expiresTick - now) / 36f);
            EnsureVisuals();
            visuals?.Draw(drawLoc, now - createdTick, opacity);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            visuals?.Dispose();
            visuals = null;
            base.DeSpawn(mode);
        }

        public void ExportVisualPreview(string directory)
        {
            EnsureVisuals();
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"caster.png"),System.Convert.FromBase64String(snapshotPng));
            for (int i=0; i<4; i++) visuals.ExportPreview(System.IO.Path.Combine(directory,"mirage-"+i+".png"), i*0.35f);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"anchors.txt"),"Facing="+Rotation+"\nEyeA="+eyeA+"\nEyeB="+eyeB);
        }

        public void Dismiss()
        {
            suppressBurst = true;
            Destroy();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Destroyed) return;
            // Deconstruction, map removal and replacement must never deal damage.
            bool detonate = !suppressBurst && !burstDone && Spawned &&
                (mode == DestroyMode.Vanish || mode == DestroyMode.KillFinalize);
            burstDone = true;
            if (detonate)
            {
                MirageVisuals.Burst(Position, Map, Settings.burstRadius);
                foreach (Pawn victim in Map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (victim.Dead || victim.Downed || !victim.RaceProps.IsFlesh || !victim.HostileTo(this) ||
                        !victim.Position.InHorDistOf(Position, Settings.burstRadius) ||
                        !GenSight.LineOfSight(Position, victim.Position, Map)) continue;
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Frostbite, Settings.frostbiteDamage,
                        instigator: Caster));
                }
            }
            // No corpse, rubble, fire spreading, construction resources, or kill rewards.
            base.Destroy(DestroyMode.Vanish);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref Caster, "mirageCaster");
            Scribe_Values.Look(ref createdTick, "mirageCreatedTick");
            Scribe_Values.Look(ref expiresTick, "mirageExpiresTick");
            Scribe_Values.Look(ref burstDone, "mirageBurstDone");
            Scribe_Values.Look(ref snapshotPng, "mirageSnapshotPng");
            // Vector4.ToString rounds to two decimals: use scalar serialization to keep subpixel anchors.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref eyeA, "mirageEyeA");
                Scribe_Values.Look(ref eyeB, "mirageEyeB");
            }
            Scribe_Values.Look(ref eyeA.x,"mirageEyeAX",eyeA.x,true);
            Scribe_Values.Look(ref eyeA.y,"mirageEyeAY",eyeA.y,true);
            Scribe_Values.Look(ref eyeA.z,"mirageEyeAZ",eyeA.z,true);
            Scribe_Values.Look(ref eyeA.w,"mirageEyeAW",eyeA.w,true);
            Scribe_Values.Look(ref eyeB.x,"mirageEyeBX",eyeB.x,true);
            Scribe_Values.Look(ref eyeB.y,"mirageEyeBY",eyeB.y,true);
            Scribe_Values.Look(ref eyeB.z,"mirageEyeBZ",eyeB.z,true);
            Scribe_Values.Look(ref eyeB.w,"mirageEyeBW",eyeB.w,true);
        }

        public override string GetInspectString()
        {
            float seconds = Mathf.Max(0, expiresTick - Find.TickManager.TicksGame) / 60f;
            return "BX_MirageInspect".Translate(Caster?.LabelShortCap ?? "?", seconds.ToString("0.0"));
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(Position, Settings.burstRadius);
        }
    }
}
