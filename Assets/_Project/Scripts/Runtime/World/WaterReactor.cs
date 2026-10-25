using UnityEngine;

namespace Planetcraft.World
{
    // Um único plano de água que sobe com o Ti. É o relevo que forma os lagos.
    public class WaterReactor : TerraformReactor
    {
        [SerializeField] float minHeight = -2f;
        [SerializeField] float maxHeight = 16f;
        [SerializeField] AnimationCurve heightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        protected override void Apply(float t)
        {
            var p = transform.position;
            p.y = Mathf.Lerp(minHeight, maxHeight, heightCurve.Evaluate(t));
            transform.position = p;
        }
    }
}
