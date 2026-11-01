using Verse;

namespace Bernael_Xenotype
{
    public class CompProperties_EquippedSoulLanternGlow : CompProperties
    {
        public CompProperties_EquippedSoulLanternGlow()
        {
            compClass = typeof(CompEquippedSoulLanternGlow);
            SoulLanternRenderBootstrap.EnsurePatched();
        }
    }
}
