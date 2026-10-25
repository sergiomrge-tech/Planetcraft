using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Core
{
    [CreateAssetMenu(menuName = "Planetcraft/Stage Database")]
    public class StageDatabase : ScriptableObject
    {
        [SerializeField] List<TerraformStage> stages = new();

        public IReadOnlyList<TerraformStage> Stages => stages;

        public void SetStages(IEnumerable<TerraformStage> list)
        {
            stages = new List<TerraformStage>(list);
            Sort();
        }

        void OnValidate() => Sort();

        // Mantém ordenado por Ti; slots vazios vão para o fim (o inspector cria slots nulos ao adicionar).
        void Sort() => stages.Sort((a, b) =>
            a == null ? (b == null ? 0 : 1) :
            b == null ? -1 :
            a.requiredTi.CompareTo(b.requiredTi));

        // Índice do último estágio alcançado para o Ti informado (-1 se nenhum).
        public int IndexFor(double ti)
        {
            int index = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null || stages[i].requiredTi > ti) break;
                index = i;
            }
            return index;
        }
    }
}
