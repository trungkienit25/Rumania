using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetcodePlus
{
    public class ListTool : MonoBehaviour
    {
        public static void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
