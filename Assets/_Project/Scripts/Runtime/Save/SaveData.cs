using System;
using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Save
{
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public double[] terraform;
        public List<SlotData> inventory = new();
        public List<PlacedData> placed = new();
        public List<string> collectedNodes = new();
        public Vector3 playerPosition;
        public float playerYaw;
        public float oxygen;
        public float water;
        public float food;
    }

    [Serializable]
    public class SlotData
    {
        public int slot;
        public string itemId;
        public int amount;
    }

    [Serializable]
    public class PlacedData
    {
        public string guid;
        public string buildableId;
        public Vector3 position;
        public Quaternion rotation;
    }
}
