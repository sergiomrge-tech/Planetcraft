using Planetcraft.Building;
using UnityEngine;

namespace Planetcraft.Combat
{
    // Acesso ao material de efeitos (FxSoft) para quem precisa de traços e raios luminosos além das partículas.
    public static class CombatFxAccess
    {
        public static Material Material()
        {
            var m = new Material(MachineFx.FxMaterial());
            // Só a cor do vértice importa: textura branca (o disco suave afinaria a linha).
            m.SetTexture("_MainTex", Texture2D.whiteTexture);
            return m;
        }
    }
}
