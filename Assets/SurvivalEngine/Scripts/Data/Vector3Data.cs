﻿using UnityEngine;
using System;
using System.Collections;
using Unity.Netcode;

namespace SurvivalEngine
{

    /// <summary>
    /// Just a serializable version of a Vector3
    /// Useful for the file system
    /// </summary>

    [System.Serializable]
    public struct Vector3Data : INetworkSerializable
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data(float iX, float iY, float iZ)
        {
            x = iX;
            y = iY;
            z = iZ;
        }

        public override string ToString()
        {
            return String.Format("[{0}, {1}, {2}]", x, y, z);
        }

        //Convert to real vector
        public static implicit operator Vector3(Vector3Data rValue)
        {
            return new Vector3(rValue.x, rValue.y, rValue.z);
        }

        public static implicit operator Vector3Data(Vector3 rValue)
        {
            return new Vector3Data(rValue.x, rValue.y, rValue.z);
        }


        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref z);
        }

        public static Vector3Data zero { get { return new Vector3Data(0f, 0f, 0f); } }
    }

    [System.Serializable]
    public struct QuaternionData : INetworkSerializable
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public QuaternionData(float iX, float iY, float iZ, float iW)
        {
            x = iX;
            y = iY;
            z = iZ;
            w = iW;
        }

        public override string ToString()
        {
            return String.Format("[{0}, {1}, {2}, {3}]", x, y, z, w);
        }

        //Convert to real vector
        public static implicit operator Quaternion(QuaternionData rValue)
        {
            return new Quaternion(rValue.x, rValue.y, rValue.z, rValue.w);
        }

        public static implicit operator QuaternionData(Quaternion rValue)
        {
            return new QuaternionData(rValue.x, rValue.y, rValue.z, rValue.w);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref z);
            serializer.SerializeValue(ref w);
        }

        public static QuaternionData zero { get { return new QuaternionData(0f, 0f, 0f, 0f); } }
    }

}