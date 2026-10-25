using System;
using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.World
{
    // Base de tudo que reage visualmente ao Ti. O progresso é medido em escala log,
    // porque o Ti cresce em ordens de grandeza.
    public abstract class TerraformReactor : MonoBehaviour
    {
        [SerializeField] double tiStart = 1;
        [SerializeField] double tiEnd = 1e9;
        [Tooltip("Velocidade da transição suave, em unidades de progresso (0-1) por segundo.")]
        [SerializeField, Min(0.001f)] float blendSpeed = 0.05f;
        [Tooltip("Saltos maiores que isso (ex.: carregar um save) são aplicados na hora.")]
        [SerializeField, Range(0f, 1f)] float snapThreshold = 0.25f;

        float target;
        float current;

        public static float LogProgress(double ti, double start, double end)
        {
            if (ti <= start) return 0f;
            if (ti >= end) return 1f;
            double a = Math.Log10(Math.Max(start, 1e-9));
            double b = Math.Log10(end);
            return Mathf.Clamp01((float)((Math.Log10(ti) - a) / (b - a)));
        }

        protected virtual void Start()
        {
            var tm = TerraformManager.Instance;
            if (tm == null)
            {
                enabled = false;
                return;
            }
            tm.TiChanged += OnTiChanged;
            target = current = LogProgress(tm.Ti, tiStart, tiEnd);
            Apply(current);
        }

        protected virtual void OnDestroy()
        {
            if (TerraformManager.Instance != null) TerraformManager.Instance.TiChanged -= OnTiChanged;
        }

        void OnTiChanged(double ti)
        {
            target = LogProgress(ti, tiStart, tiEnd);
            if (Mathf.Abs(target - current) > snapThreshold)
            {
                current = target;
                Apply(current);
            }
        }

        void Update()
        {
            if (Mathf.Approximately(current, target)) return;
            current = Mathf.MoveTowards(current, target, blendSpeed * Time.deltaTime);
            Apply(current);
        }

        protected abstract void Apply(float progress);
    }
}
