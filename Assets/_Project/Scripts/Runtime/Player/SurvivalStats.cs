using System;
using Planetcraft.Items;
using UnityEngine;

namespace Planetcraft.Player
{
    public class SurvivalStats : MonoBehaviour
    {
        [SerializeField] float maxOxygen = 100f;
        [SerializeField] float maxWater = 100f;
        [SerializeField] float maxFood = 100f;
        [Tooltip("Consumo por segundo.")]
        [SerializeField] float oxygenDrain = 0.6f;
        [SerializeField] float waterDrain = 0.12f;
        [SerializeField] float foodDrain = 0.08f;
        [SerializeField] float oxygenRefillInBase = 15f;
        [SerializeField] Transform respawnPoint;

        int pressurizedZones;

        public float Oxygen { get; private set; }
        public float Water { get; private set; }
        public float Food { get; private set; }
        public float OxygenNormalized => Oxygen / maxOxygen;
        public float WaterNormalized => Water / maxWater;
        public float FoodNormalized => Food / maxFood;
        public bool InPressurizedZone => pressurizedZones > 0;

        public event Action Died;

        void Awake()
        {
            Oxygen = maxOxygen;
            Water = maxWater;
            Food = maxFood;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Oxygen = InPressurizedZone
                ? Mathf.Min(maxOxygen, Oxygen + oxygenRefillInBase * dt)
                : Oxygen - oxygenDrain * dt;
            Water -= waterDrain * dt;
            Food -= foodDrain * dt;
            if (Oxygen <= 0f || Water <= 0f || Food <= 0f) Die();
        }

        public void Consume(ItemDefinition item)
        {
            Oxygen = Mathf.Min(maxOxygen, Oxygen + item.restoreOxygen);
            Water = Mathf.Min(maxWater, Water + item.restoreWater);
            Food = Mathf.Min(maxFood, Food + item.restoreFood);
        }

        public void SetValues(float oxygen, float water, float food)
        {
            Oxygen = Mathf.Clamp(oxygen, 1f, maxOxygen);
            Water = Mathf.Clamp(water, 1f, maxWater);
            Food = Mathf.Clamp(food, 1f, maxFood);
        }

        public void EnterPressurizedZone() => pressurizedZones++;
        public void ExitPressurizedZone() => pressurizedZones = Mathf.Max(0, pressurizedZones - 1);

        void Die()
        {
            Died?.Invoke();
            Oxygen = maxOxygen;
            Water = maxWater * 0.5f;
            Food = maxFood * 0.5f;
            if (respawnPoint != null && TryGetComponent<FirstPersonController>(out var fpc))
                fpc.Teleport(respawnPoint.position, respawnPoint.eulerAngles.y);
        }
    }
}
