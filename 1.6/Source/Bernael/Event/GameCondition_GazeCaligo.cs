using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public class GameCondition_GazeCaligo : GameCondition
    {
        [StaticConstructorOnStartup]
	    private class GazeCaligoOverlay : SkyOverlay
	    {
	    	private static readonly Material GlowSporeOverlayWorld = MatLoader.LoadMat("Weather/GlowSporeOverlayWorld");
	    	private static readonly ComplexCurve speedCurve = new ComplexCurve(new UnityEngine.Keyframe(0f, 0f), new UnityEngine.Keyframe(1f, 1f), new UnityEngine.Keyframe(2f, 1.1f), new UnityEngine.Keyframe(3f, 1.2f));
	    	private TexturePannerSpeedCurve panner0 = new TexturePannerSpeedCurve(GlowSporeOverlayWorld, "_MainTex", speedCurve, new Vector2(-1f, -0.2f), 0.0001f);
	    	private TexturePannerSpeedCurve panner1 = new TexturePannerSpeedCurve(GlowSporeOverlayWorld, "_MainTex2", speedCurve, new Vector2(0.35f, -1f), 5E-05f);

	    	public override void SetOverlayColor(Color color)
	    	{
	    		GlowSporeOverlayWorld.color = color;
	    	}

	    	public override void DrawOverlay(Map map)
	    	{
	    		SkyOverlay.DrawWorldOverlay(map, GlowSporeOverlayWorld);
	    	}

	    	public override void TickOverlay(Map map, float lerpFactor)
	    	{
	    		panner0.Tick();
	    		panner1.Tick();
	    	}
	    }

        private int curColorIndex = -1;
	    private int prevColorIndex = -1;
	    private float curColorTransition;

        private Vector3 currentPupilPos = Vector3.zero;
        private Vector3 targetPupilPos = Vector3.zero;
        private int nextPupilMoveTick = -1;
        private const int MoveIntervalTicks = 3000; // 50s or 1 in-game hour
        private const float MinPupilScaleLimit = 0.75f;
        private const float MoveSpeedPerTick = 0.05f;
        private const float DiameterMapFraction = 0.33f;

        // or AltitudeLayer.Weather.AltitudeFor() - 0.03658537f
        private static readonly float PupilAltitude = AltitudeLayer.Weather.AltitudeFor() + 0.03658537f;
        private static readonly MaterialPropertyBlock glowPropertyBlock = new MaterialPropertyBlock();
        private static readonly Material GlowCircleMaterial = MaterialPool.MatFrom("Other/BX_GlowCircle", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock pupilPropertyBlock = new MaterialPropertyBlock();
        private static readonly Material PupilMaterial = MaterialPool.MatFrom("Other/BX_Pupil", ShaderDatabase.Transparent, Color.white);

        private int nextBlinkTick = -1;
        private int blinkEndTick = -1;
        private bool isBlinking = false;
        private IntRange BlinkIntervalTicks = new IntRange(600, 1800); // 10s ~ 30s
        private const int BlinkHoldTicks = 60;
        private const int BlinkFadeOutTicks = 30;   // 0.5s
        private const int BlinkFadeInTicks = 30;    // 0.5s

        private const float MaxSkyLerpFactor = 0.5f;
        private const float Glow = 0.85f;
        private const float BaseBrightness = 0.73f;
	    private static readonly GazeCaligoOverlay gazeCaligoOverlay = new GazeCaligoOverlay();
	    private static readonly List<SkyOverlay> overlays = new List<SkyOverlay> { gazeCaligoOverlay };
	    private static readonly Color[] Colors = new Color[5]
	    {
	    	new Color(0.2f, 0.4f, 1f),
	    	new Color(0.3f, 0.6f, 1f),
	    	new Color(0.5f, 0.7f, 1f),
	    	new Color(0.5f, 0.5f, 1f),
	    	new Color(0.3f, 0.3f, 1f)
	    };
        private static readonly Color ShadowColorTint = new ColorInt(20, 40, 60).ToColor;
	    public Color CurrentColor => Color.Lerp(Colors[prevColorIndex], Colors[curColorIndex], curColorTransition);
        private int TransitionDurationTicks => 280;

        private const int CheckInterval = 3251;
        private const int HediffDuration = 3256;

        private Sustainer sustainer;
        private Sustainer sustainerMoving;

        public override void Init()
        {
            base.Init();
		    curColorIndex = Rand.Range(0, Colors.Length);
		    prevColorIndex = curColorIndex;
		    curColorTransition = 1f;

            int currentTick = Find.TickManager.TicksGame;
            nextPupilMoveTick = -1;
            ScheduleNextBlink(currentTick);

            SoundDefOf.Thunder_OnMap.PlayOneShotOnCamera();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextBlinkTick, "nextBlinkTick", -1);
            Scribe_Values.Look(ref blinkEndTick, "blinkEndTick", -1);
            Scribe_Values.Look(ref isBlinking, "isBlinking", false);
        }

        private void ScheduleNextBlink(int currentTick)
        {
            nextBlinkTick = currentTick + BlinkIntervalTicks.RandomInRange;
            blinkEndTick = nextBlinkTick + BlinkHoldTicks;
        }

        public override void GameConditionTick()
        {
            List<Map> maps = AffectedMaps;
            int currentTick = Find.TickManager.TicksGame;

		    curColorTransition += 1f / TransitionDurationTicks;
		    if (curColorTransition >= 1f)
		    {
		    	prevColorIndex = curColorIndex;
		    	curColorIndex = GetNewColorIndex();
		    	curColorTransition = 0f;
		    }

            if (sustainer == null || sustainer.Ended)
            {
                sustainer = BernaelDefOf.BX_Ambient_Gaze.TrySpawnSustainer(SoundInfo.OnCamera(MaintenanceType.PerTick));
            }
            else
            {
                sustainer?.Maintain();
            }

            // Update blinking state machine
            if (!isBlinking && currentTick >= nextBlinkTick)
            {
                isBlinking = true;
                BernaelDefOf.BX_Gaze_Blink.PlayOneShotOnCamera();
            }
            else if (isBlinking && currentTick >= blinkEndTick)
            {
                isBlinking = false;
                ScheduleNextBlink(currentTick);
            }

            if (currentTick % CheckInterval == 0)
            {
                for (int m = 0; m < maps.Count; m++)
                {
                    Map map = maps[m];
                    if (map == null) continue;

                    IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
                    for (int i = 0; i < allPawns.Count; i++)
                    {
                        Pawn pawn = allPawns[i];
                        if (pawn.kindDef.immuneToGameConditionEffects) continue;

                        // Check Psylink
                        if (pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier))
                        {
                            GiveOrUpdateHediff(pawn, BernaelDefOf.BX_GazeCaligoBuff);
                        }
                        else
                        {
                            GiveOrUpdateHediff(pawn, BernaelDefOf.BX_GazeCaligoDebuff);
                        }
                    }
                }
            }

            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (HiddenByOtherCondition(map)) continue;

                gazeCaligoOverlay.TickOverlay(map, 1f);
                MovePupil(map, currentTick);
            }
        }

        public override void GameConditionDraw(Map map)
        {
            if (!HiddenByOtherCondition(map))
		    {
		    	gazeCaligoOverlay.DrawOverlay(map);
                DrawPupil(map);
                DrawGlow(map);
		    }
        }

        public override void End()
        {
            List<Map> maps = AffectedMaps;

            sustainer?.End();
            sustainer = null;

            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (map == null) continue;

                IReadOnlyList<Pawn> allPawns = map?.mapPawns?.AllPawnsSpawned;
                int psyCasterCount = 0;
                int totalPsylinkLevel = 0;

                if (allPawns != null)
                {
                    for (int i = 0; i < allPawns.Count; i++)
                    {
                        Pawn pawn = allPawns[i];
                        if (pawn == null || pawn.Dead || !pawn.IsColonist) continue;

                        // Check Psylink
                        if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicAmplifier) is Hediff_Psylink psylink)
                        {
                            psyCasterCount++;
                            totalPsylinkLevel += psylink.level;
                        }
                    }
                }

                // Defining the thresholds for high and low resonance.
                bool isHighResonance = (psyCasterCount >= 3) || (totalPsylinkLevel >= 3);

                IncidentDef selectedIncident = isHighResonance 
                    ? GetRandomHighResonanceIncident() 
                    : GetRandomLowResonanceIncident();

                OutcomeIncident(selectedIncident, map);
            }

            SoundDefOf.Thunder_OffMap.PlayOneShotOnCamera();
            base.End();
        }

        public override float SkyTargetLerpFactor(Map map) => GameConditionUtility.LerpInOutValue(this, TransitionTicks, MaxSkyLerpFactor);

        public override SkyTarget? SkyTarget(Map map)
        {
            if (map.GameConditionManager.IsAlwaysDarkOutside)
		    {
		    	return null;
		    }
		    Color currentColor = CurrentColor;
            return new SkyTarget(Mathf.Max(GenCelestial.CurCelestialSunGlow(map), Glow), new SkyColorSet(currentColor * Blink() * Brightness(map), ShadowColorTint * Blink(), currentColor * Brightness(map), 1.25f), 4.5f, 0.8f);
        }

        public override List<SkyOverlay> SkyOverlays(Map map)
	    {
	    	return overlays;
	    }

        private int GetNewColorIndex()
	    {
	    	return (from x in Enumerable.Range(0, Colors.Length)
	    		where x != curColorIndex
	    		select x).RandomElement();
	    }

	    private void GiveOrUpdateHediff(Pawn target, HediffDef hediffDef)
	    {
            if (target?.health?.hediffSet == null) return;
            Hediff hediff = target.health.hediffSet.GetFirstHediffOfDef(hediffDef) ?? target.health.AddHediff(hediffDef);
            HediffComp_Disappears comp = hediff?.TryGetComp<HediffComp_Disappears>();
            if (comp != null) comp.ticksToDisappear = HediffDuration;
        }

        private void MovePupil(Map map, int currentTick)
        {
            Vector3 mapCenter = new Vector3(map.Size.x * 0.5f, PupilAltitude, map.Size.z * 0.5f);

            if (nextPupilMoveTick <= 0)
            {
                currentPupilPos = mapCenter;
                targetPupilPos = mapCenter;
                nextPupilMoveTick = currentTick + MoveIntervalTicks / 10;
            }
            else if (currentTick >= nextPupilMoveTick)
            {
                targetPupilPos = NewPupilTargetPos(map, MinPupilScaleLimit);
                targetPupilPos.y = PupilAltitude;
                nextPupilMoveTick = currentTick + MoveIntervalTicks;
            }

            // Pupil Position Step Calculation
            Vector3 toTargetPos = targetPupilPos - currentPupilPos;
            toTargetPos.y = 0f;

            float distanceToTargetPos = toTargetPos.magnitude;
            if (distanceToTargetPos > 0f)
            {
                float step = Mathf.Min(MoveSpeedPerTick, distanceToTargetPos);
                currentPupilPos += toTargetPos / distanceToTargetPos * step;
                if (sustainerMoving == null || sustainerMoving.Ended)
                {
                    sustainerMoving = BernaelDefOf.BX_Gaze_Move.TrySpawnSustainer(SoundInfo.OnCamera(MaintenanceType.PerTick));
                }
                else
                {
                    sustainerMoving?.Maintain();
                }
            }
            else
            {
                sustainerMoving?.End();
                sustainerMoving = null;
            }
        }

        private void DrawPupil(Map map)
        {
            Vector3 mapCenter = new Vector3(map.Size.x * 0.5f, PupilAltitude, map.Size.z * 0.5f);

            float minMapSize = Mathf.Min(map.Size.x, map.Size.z);
            float diameter = minMapSize * DiameterMapFraction;
            float pupilRadius = diameter * 0.5f;
            float maxValidRadius = Mathf.Max(0f, (minMapSize * 0.5f) - pupilRadius);

            // pupil distort
            Vector3 offsetFromCenter = currentPupilPos - mapCenter;
            float distFromCenter = offsetFromCenter.magnitude;
            float maxOffsetRadius = Mathf.Max(0.01f, maxValidRadius);
            float normalizedDist = Mathf.Clamp01(distFromCenter / maxOffsetRadius);
            float circleArc = Mathf.Sqrt(1f - (normalizedDist * normalizedDist));
            float radialScale = Mathf.Lerp(MinPupilScaleLimit, 1f, circleArc);

            float scaleX = diameter * radialScale;

            Quaternion rotation = Quaternion.identity;
            if (distFromCenter > 0.0001f)
            {
                float angleDeg = Mathf.Atan2(offsetFromCenter.z, offsetFromCenter.x) * Mathf.Rad2Deg;
                rotation = Quaternion.AngleAxis(-angleDeg, Vector3.up); 
            }

            Matrix4x4 pupilMatrix = Matrix4x4.TRS(currentPupilPos, rotation, new Vector3(scaleX, 1f, diameter));
            Color pupilColor = new Color(1f, 1f, 1f, 1f * Blink());
            pupilPropertyBlock.SetColor("_Color", pupilColor);
            Graphics.DrawMesh(MeshPool.plane10, pupilMatrix, PupilMaterial, 0, null, 0, pupilPropertyBlock);
        }

        private void DrawGlow(Map map)
        {
            Vector3 mapCenter = new Vector3(map.Size.x * 0.5f, PupilAltitude, map.Size.z * 0.5f);

            Vector3 glowScale = new Vector3(map.Size.x, 1f, map.Size.z);
            Matrix4x4 glowMatrix = default;
            glowMatrix.SetTRS(mapCenter, Quaternion.identity, glowScale);
            Color glowColor = new Color(0.6f, 0.8f, 1f, 1f * Blink());
            glowPropertyBlock.SetColor("_Color", glowColor);
            Graphics.DrawMesh(MeshPool.plane10, glowMatrix, GlowCircleMaterial, 0, null, 0, glowPropertyBlock);
        }

        private Vector3 NewPupilTargetPos(Map map, float MinPupilScaleLimit)
        {
            float minMapSize = Mathf.Min(map.Size.x, map.Size.z);
            float pupilRadius = minMapSize * DiameterMapFraction * 0.5f;
            Vector3 mapCenter = new Vector3(map.Size.x * 0.5f, PupilAltitude, map.Size.z * 0.5f);
            float maxValidRadius = Mathf.Max(0f, (minMapSize * 0.5f) - (pupilRadius * MinPupilScaleLimit));

            float randomAngle = Random.Range(0f, Mathf.PI * 2f);
            float randomDistance = Random.Range(maxValidRadius / 4f, maxValidRadius);

            Vector3 offset = new Vector3(Mathf.Cos(randomAngle) * randomDistance, 0f, Mathf.Sin(randomAngle) * randomDistance);
            Vector3 newTarget = mapCenter + offset;
            Vector3 displacementFromCenter = newTarget - mapCenter;
            displacementFromCenter.y = 0f;

            if (displacementFromCenter.magnitude > maxValidRadius)
            {
                newTarget = mapCenter + displacementFromCenter.normalized * maxValidRadius;
            }

            newTarget.y = PupilAltitude;
            return newTarget;
        }

        private float Blink()
        {
            if (!isBlinking)
            {
                return 1.0f;
            }

            int currentTick = Find.TickManager.TicksGame;
            int elapsed = currentTick - nextBlinkTick;

            if (elapsed < BlinkFadeOutTicks)
            {
                float progress = (float)elapsed / BlinkFadeOutTicks;
                return Mathf.Lerp(1.0f, 0.01f, progress);
            }

            if (currentTick >= blinkEndTick - BlinkFadeInTicks)
            {
                int fadeInElapsed = currentTick - (blinkEndTick - BlinkFadeInTicks);
                float progress = (float)fadeInElapsed / BlinkFadeInTicks;
                return Mathf.Lerp(0.01f, 1.0f, progress);
            }

            return 0.0f;
        }

	    private float Brightness(Map map)
	    {
	    	return Mathf.Max(BaseBrightness, GenCelestial.CurCelestialSunGlow(map));
	    }

        // ====================== Outcome event ======================

        private IncidentDef GetRandomHighResonanceIncident()
        {
            IncidentDef[] highIncidents = {
                BernaelDefOf.BX_AstralAwakening,
                BernaelDefOf.BX_EthersBlessing,
                BernaelDefOf.BX_PsycasterPilgrimage,
                BernaelDefOf.BX_PsychicOvercharge
            };
            return highIncidents[Rand.Range(0, highIncidents.Length)];
        }

        private IncidentDef GetRandomLowResonanceIncident()
        {
            IncidentDef[] lowIncidents = {
                BernaelDefOf.BX_PsychicBurnout,
                BernaelDefOf.BX_PsychicRupture
            };
            return lowIncidents[Rand.Range(0, lowIncidents.Length)];
        }

	    private void OutcomeIncident(IncidentDef incidentDef, Map map)
        {
            if (incidentDef == null) return;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
            incidentDef.Worker.TryExecute(parms);
        }
    }
}