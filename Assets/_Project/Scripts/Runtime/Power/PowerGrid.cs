using System;
using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Power
{
    public interface IPowerNode
    {
        float PowerGeneration { get; }
        float PowerDemand { get; }
    }

    // Rede global sem fios: qualquer gerador alimenta qualquer máquina.
    // Se a demanda passar da geração, tudo que consome energia para.
    public static class PowerGrid
    {
        static readonly HashSet<IPowerNode> Nodes = new();

        public static float Generation { get; private set; }
        public static float Demand { get; private set; }
        public static bool IsPowered => Demand <= Generation;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Nodes.Clear();
            Generation = Demand = 0f;
            Changed = null;
        }

        public static void Register(IPowerNode node)
        {
            if (Nodes.Add(node)) Recalculate();
        }

        public static void Unregister(IPowerNode node)
        {
            if (Nodes.Remove(node)) Recalculate();
        }

        public static void Recalculate()
        {
            float generation = 0f, demand = 0f;
            foreach (var n in Nodes)
            {
                generation += n.PowerGeneration;
                demand += n.PowerDemand;
            }
            Generation = generation;
            Demand = demand;
            Changed?.Invoke();
        }
    }
}
