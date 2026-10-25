using System.Collections.Generic;
using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.World
{
    // Liga os filhos de `container` aos poucos entre tiStart e tiEnd (escala log),
    // em ordem aleatória fixa e com animação de crescimento: a vegetação "brota" em vez de surgir de uma vez.
    public class ProgressiveActivator : MonoBehaviour
    {
        [SerializeField] Transform container;
        [SerializeField] double tiStart = 1;
        [SerializeField] double tiEnd = 1e9;
        [Tooltip("Fração que já aparece assim que tiStart é alcançado.")]
        [SerializeField, Range(0f, 1f)] float initialFraction = 0.15f;
        [SerializeField] float activationsPerSecond = 25f;
        [SerializeField] float growDuration = 1.2f;
        [SerializeField] int seed = 1;

        GameObject[] items;
        Vector3[] fullScales;
        int activeCount;
        int targetCount;
        float budget;
        readonly List<(Transform t, Vector3 scale, float start)> growing = new();

        void Awake()
        {
            if (container == null) container = transform;
            int n = container.childCount;
            items = new GameObject[n];
            fullScales = new Vector3[n];
            for (int i = 0; i < n; i++) items[i] = container.GetChild(i).gameObject;

            var rng = new System.Random(seed);
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
            for (int i = 0; i < n; i++)
            {
                fullScales[i] = items[i].transform.localScale;
                items[i].SetActive(false);
            }
        }

        void Start()
        {
            var tm = TerraformManager.Instance;
            if (tm == null) return;
            tm.TiChanged += OnTiChanged;
            OnTiChanged(tm.Ti);
            // Ao carregar o jogo, já mostra tudo que deveria existir, sem animação.
            while (activeCount < targetCount) Activate(animate: false);
        }

        void OnDestroy()
        {
            if (TerraformManager.Instance != null) TerraformManager.Instance.TiChanged -= OnTiChanged;
        }

        void OnTiChanged(double ti)
        {
            if (ti < tiStart)
            {
                targetCount = 0;
                return;
            }
            float t = TerraformReactor.LogProgress(ti, tiStart, tiEnd);
            targetCount = Mathf.RoundToInt(items.Length * (initialFraction + (1f - initialFraction) * t));
        }

        void Update()
        {
            while (activeCount > targetCount)
            {
                activeCount--;
                items[activeCount].SetActive(false);
                items[activeCount].transform.localScale = fullScales[activeCount];
            }

            if (activeCount < targetCount)
            {
                budget += activationsPerSecond * Time.deltaTime;
                while (budget >= 1f && activeCount < targetCount)
                {
                    budget -= 1f;
                    Activate(animate: true);
                }
            }
            else
            {
                budget = 0f;
            }

            for (int i = growing.Count - 1; i >= 0; i--)
            {
                var g = growing[i];
                float k = Mathf.Clamp01((Time.time - g.start) / growDuration);
                if (g.t == null) { growing.RemoveAt(i); continue; }
                g.t.localScale = g.scale * (1f - (1f - k) * (1f - k));
                if (k >= 1f) growing.RemoveAt(i);
            }
        }

        void Activate(bool animate)
        {
            var go = items[activeCount];
            var full = fullScales[activeCount];
            activeCount++;
            go.SetActive(true);
            if (!animate)
            {
                go.transform.localScale = full;
                return;
            }
            go.transform.localScale = Vector3.zero;
            growing.Add((go.transform, full, Time.time));
        }
    }
}
