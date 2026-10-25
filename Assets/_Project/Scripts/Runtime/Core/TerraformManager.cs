using System;
using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Core
{
    // Fonte única da verdade do progresso do planeta. Simula em tick fixo (não a cada frame)
    // para que balanceamento e simulação offline sejam previsíveis.
    [DefaultExecutionOrder(-100)]
    public class TerraformManager : MonoBehaviour
    {
        public static TerraformManager Instance { get; private set; }

        static readonly List<ITerraformContributor> Contributors = new();

        [SerializeField] StageDatabase stages;
        [SerializeField, Min(0.05f)] float tickInterval = 1f;
        [Tooltip("Multiplicador global da simulação, útil para testar o balanceamento.")]
        [SerializeField, Min(0f)] float simulationSpeed = 1f;

        readonly double[] values = new double[TerraformParameters.Count];
        readonly double[] rates = new double[TerraformParameters.Count];
        float tickTimer;

        public event Action<double> TiChanged;
        public event Action<TerraformStage> StageReached;

        public StageDatabase Stages => stages;
        public int CurrentStageIndex { get; private set; } = -1;
        public TerraformStage CurrentStage => CurrentStageIndex >= 0 ? stages.Stages[CurrentStageIndex] : null;

        public TerraformStage NextStage =>
            stages != null && CurrentStageIndex + 1 < stages.Stages.Count ? stages.Stages[CurrentStageIndex + 1] : null;

        public float SimulationSpeed
        {
            get => simulationSpeed;
            set => simulationSpeed = Mathf.Max(0f, value);
        }

        public double Ti
        {
            get
            {
                double total = 0;
                foreach (var v in values) total += v;
                return total;
            }
        }

        public double Get(TerraformParameter p) => values[(int)p];
        public double GetRate(TerraformParameter p) => rates[(int)p];
        public double[] GetValues() => (double[])values.Clone();
        public bool HasReached(TerraformStage stage) => stage == null || Ti >= stage.requiredTi;

        public static void Register(ITerraformContributor c)
        {
            if (!Contributors.Contains(c)) Contributors.Add(c);
        }

        public static void Unregister(ITerraformContributor c) => Contributors.Remove(c);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Contributors.Clear();
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => RefreshStage(notify: false);

        void Update()
        {
            tickTimer += Time.deltaTime;
            if (tickTimer < tickInterval) return;
            float dt = tickTimer * simulationSpeed;
            tickTimer = 0f;
            Simulate(dt);
        }

        public void Simulate(float seconds)
        {
            Array.Clear(rates, 0, rates.Length);
            foreach (var c in Contributors) c.AccumulateRates(rates);
            for (int i = 0; i < values.Length; i++)
                values[i] = Math.Max(0d, values[i] + rates[i] * seconds);
            RefreshStage(notify: true);
            TiChanged?.Invoke(Ti);
        }

        // Usado pelo save: não dispara StageReached para estágios já vistos.
        public void SetValues(double[] newValues)
        {
            Array.Copy(newValues, values, Math.Min(newValues.Length, values.Length));
            RefreshStage(notify: false);
            TiChanged?.Invoke(Ti);
        }

        public void Add(TerraformParameter p, double amount)
        {
            values[(int)p] = Math.Max(0d, values[(int)p] + amount);
            RefreshStage(notify: true);
            TiChanged?.Invoke(Ti);
        }

        void RefreshStage(bool notify)
        {
            if (stages == null) return;
            int previous = CurrentStageIndex;
            CurrentStageIndex = stages.IndexFor(Ti);
            if (!notify) return;
            for (int i = previous + 1; i <= CurrentStageIndex; i++) StageReached?.Invoke(stages.Stages[i]);
        }
    }
}
