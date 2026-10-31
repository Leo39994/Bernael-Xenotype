using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public class GeneGizmo_ResourceSoulStarve : GeneGizmo_Resource
    {
        private static readonly Texture2D SoulCostTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.462f, 0.34f, 0.57f));
        private const float TotalPulsateTime = 0.85f;
        private List<Pair<IGeneResourceDrain, float>> tmpDrainGenes = new List<Pair<IGeneResourceDrain, float>>();
        private static bool draggingBar;

        public GeneGizmo_ResourceSoulStarve(Gene_Resource gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor) : base(gene, drainGenes, barColor, barHighlightColor)
        {
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            float num = Mathf.Repeat(Time.time, 0.85f);
            float num2 = 1f;
            if (num < 0.1f)
            {
                num2 = num / 0.1f;
            }
            else if (num >= 0.25f)
            {
                num2 = 1f - (num - 0.25f) / 0.6f;
            }
            MainTabWindow_Inspect mainTabWindow_Inspect = (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
            if (MapGizmoUtility.LastMouseOverGizmo is Command_Ability command_Ability && gene.Max != 0f)
            {
                using (List<CompAbilityEffect>.Enumerator enumerator = command_Ability.Ability.EffectComps.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        CompAbilityEffect_SoulCost compAbilityEffect_SoulCost;
                        if ((compAbilityEffect_SoulCost = (enumerator.Current as CompAbilityEffect_SoulCost)) != null && compAbilityEffect_SoulCost.Props.soulCost > 1E-45f)
                        {
                            Rect rect = barRect.ContractedBy(3f);
                            float width = rect.width;
                            float num3 = gene.Value / gene.Max;
                            rect.xMax = rect.xMin + width * num3 + 30;
                            float num4 = Mathf.Min(compAbilityEffect_SoulCost.Props.soulCost / gene.Max, 1f);
                            rect.xMin = Mathf.Max(rect.xMin, rect.xMax - width * num4);
                            GUI.color = new Color(1f, 1f, 1f, num2 * 0.7f);
                            GenUI.DrawTextureWithMaterial(rect, SoulCostTex, null);
                            GUI.color = Color.white;
                            break;
                        }
                    }
                }
            }
            return result;
        }

        protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
        {
            Gene_Soul soulGene;
            if (IsDraggable && (soulGene = (gene as Gene_Soul)) != null)
            {
                headerRect.xMax -= 24f;
                Rect rect = new Rect(headerRect.xMax, headerRect.y, 24f, 24f);
                Widgets.DefIcon(rect, BernaelDefOf.BX_BottledSoul);
                GUI.DrawTexture(new Rect(rect.center.x, rect.y, rect.width / 2f, rect.height / 2f), soulGene.bottledSoulsAllowed ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                if (Widgets.ButtonInvisible(rect))
                {
                    soulGene.bottledSoulsAllowed = !soulGene.bottledSoulsAllowed;
                    if (soulGene.bottledSoulsAllowed)
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    }
                    else
                    {
                        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                    }
                }
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                    string onOff = (soulGene.bottledSoulsAllowed ? "On" : "Off").Translate().ToString().UncapitalizeFirst();
                    TooltipHandler.TipRegion(rect, () => "MB_AutoTakeSoulDesc".Translate(gene.pawn.Named("PAWN"), soulGene.PostProcessValue(soulGene.targetValue).Named("MIN"), onOff.Named("ONOFF")).Resolve(), 828267371);
                    mouseOverElement = true;
                }
            }
            base.DrawHeader(headerRect, ref mouseOverElement);
        }
        protected override bool DraggingBar
        {
            get
            {
                return draggingBar;
            }
            set
            {
                draggingBar = value;
            }
        }

        protected override string GetTooltip()
        {
            tmpDrainGenes.Clear();
            string text = string.Format("{0}: {1} / {2}\n", gene.ResourceLabel.CapitalizeFirst().Colorize(ColoredText.TipSectionTitleColor), gene.ValueForDisplay, gene.MaxForDisplay);
            if (gene.pawn.IsColonistPlayerControlled || gene.pawn.IsPrisonerOfColony)
            {
                if (gene.targetValue <= 0f)
                {
                    text += "BX_NeverConsumeSoul".Translate().ToString();
                }
                else
                {
                    text = text + ("BX_ConsumeSoulBelow".Translate() + ": ") + gene.PostProcessValue(gene.targetValue);
                }
            }
            if (!drainGenes.NullOrEmpty())
            {
                float num = 0f;
                foreach (IGeneResourceDrain geneResourceDrain in drainGenes)
                {
                    if (geneResourceDrain.CanOffset)
                    {
                        tmpDrainGenes.Add(new Pair<IGeneResourceDrain, float>(geneResourceDrain, geneResourceDrain.ResourceLossPerDay));
                        num += geneResourceDrain.ResourceLossPerDay;
                    }
                }
                if (num != 0f)
                {
                    string text2 = (num < 0f) ? "RegenerationRate".Translate() : "DrainRate".Translate();
                    text = string.Concat(text, "\n\n", text2, ": ", "PerDay".Translate(Mathf.Abs(gene.PostProcessValue(num))).Resolve());
                    foreach (Pair<IGeneResourceDrain, float> pair in tmpDrainGenes)
                    {
                        text = string.Concat(text, "\n  - ", pair.First.DisplayLabel.CapitalizeFirst(), ": ", "PerDay".Translate(gene.PostProcessValue(-pair.Second).ToStringWithSign()).Resolve());
                    }
                }
            }
            if (!gene.def.resourceDescription.NullOrEmpty())
            {
                text = text + "\n\n" + gene.def.resourceDescription.Formatted(gene.pawn.Named("PAWN")).Resolve();
            }
            return text;
        }



    }
}
