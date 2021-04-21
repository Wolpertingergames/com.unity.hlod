﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.HLODSystem.Streaming
{
    public class DefaultHLODController : HLODControllerBase
    {
        [SerializeField]
        private List<GameObject> m_gameObjectList = new List<GameObject>();
        [SerializeField]
        private List<GameObject> m_lowGameObjects = new List<GameObject>();

        public int AddHighObject(GameObject gameObject)
        {
            int id = m_gameObjectList.Count;
            m_gameObjectList.Add(gameObject);
            return id;
        }

        public int AddLowObject(GameObject gameObject)
        {
            int id = m_lowGameObjects.Count;
            m_lowGameObjects.Add(gameObject);
            return id;
        }
        public override int HighObjectCount { get => m_gameObjectList.Count; }
        public override int LowObjectCount { get => m_lowGameObjects.Count; }
        public override void GetHighObject(int id, int level, float distance, Action<GameObject> loadDoneCallback)
        {
            loadDoneCallback?.Invoke(m_gameObjectList[id]);            
        }

        public override void GetLowObject(int id, int level, float distance, Action<GameObject> loadDoneCallback)
        {
            loadDoneCallback?.Invoke(m_lowGameObjects[id]);
        }
        
        public override void OnStart()
        {

        }

        public override void OnStop()
        {

        }



        public override void Install()
        {
            for (int i = 0; i < m_gameObjectList.Count; ++i)
            {
                m_gameObjectList[i].SetActive(false);
            }
        }

        public override void ReleaseHighObject(int id)
        {
            m_gameObjectList[id].SetActive(false);
        }

        public override void ReleaseLowObject(int id)
        {
            m_lowGameObjects[id].SetActive(false);
        }
    }

}