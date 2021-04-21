﻿using System;
using System.Collections;
using System.Collections.Generic;
using Unity.HLODSystem.SpaceManager;
using Unity.HLODSystem.Streaming;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Unity.HLODSystem
{
    [Serializable]
    public class HLODTreeNode
    {
        [SerializeField] 
        private int m_level;
        [SerializeField]
        private Bounds m_bounds;
        
        [NonSerialized]
        private HLODTreeNodeContainer m_container;
        [SerializeField]
        private List<int> m_childTreeNodeIds = new List<int>();

        [SerializeField]
        private List<int> m_highObjectIds = new List<int>();
        [SerializeField]
        private List<int> m_lowObjectIds = new List<int>();

        private Dictionary<int, GameObject> m_highObjects = new Dictionary<int, GameObject>();
        private Dictionary<int, GameObject> m_lowObjects = new Dictionary<int, GameObject>();

        private Dictionary<int, GameObject> m_loadedHighObjects;
        private Dictionary<int, GameObject> m_loadedLowObjects;

        public int Level
        {
            set { m_level = value; }
            get { return m_level; }
        }
        public Bounds Bounds
        {
            set { m_bounds = value; }
            get { return m_bounds; }
        }

        public List<int> HighObjectIds
        {
            get { return m_highObjectIds; }
        }

        public List<int> LowObjectIds
        {
            get { return m_lowObjectIds; }
        }

        private State ExprectedState
        {
            get { return m_expectedState; }
        }

        enum State
        {
            Release,
            Low,
            High,
        }

        private FSM<State> m_fsm = new FSM<State>();
        private State m_expectedState = State.Release;

        private HLODControllerBase m_controller;
        private ISpaceManager m_spaceManager;
        private HLODTreeNode m_parent;

        private float m_boundsLength;
        private float m_distance;
        
        private bool m_isVisible;
        private bool m_isVisibleHierarchy;

        public HLODTreeNode()
        {
        }

        public void SetContainer(HLODTreeNodeContainer container)
        {
            m_container = container;
            
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.SetContainer(container);
            }
        }
        public void SetChildTreeNode(List<HLODTreeNode> childNodes)
        {
            ClearChildTreeNode();
            m_childTreeNodeIds.Capacity = childNodes.Count;

            for (int i = 0; i < childNodes.Count; ++i)
            {
                int id = m_container.Add(childNodes[i]);
                m_childTreeNodeIds.Add(id);
                childNodes[i].SetContainer(m_container);
            }
        }

        public int GetChildTreeNodeCount()
        {
            return m_childTreeNodeIds.Count;
        }

        public HLODTreeNode GetChildTreeNode(int index)
        {
            int id = m_childTreeNodeIds[index];
            return m_container.Get(id);
        }

        public void ClearChildTreeNode()
        {
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                m_container.Remove(m_childTreeNodeIds[i]);
            }
            m_childTreeNodeIds.Clear();
        }
        

        public void Initialize(HLODControllerBase controller, ISpaceManager spaceManager, HLODTreeNode parent)
        {

            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.Initialize(controller, spaceManager, this);
            }
            
            //set to initialize state
            m_fsm.ChangeState(State.Release);

            m_fsm.RegisterIsReadyToEnterFunction(State.Release, IsReadyToEnterRelease);
            m_fsm.RegisterEnteredFunction(State.Release, OnEnteredRelease);

            m_fsm.RegisterEnteringFunction(State.Low, OnEnteringLow);
            m_fsm.RegisterIsReadyToEnterFunction(State.Low, IsReadyToEnterLow);
            m_fsm.RegisterEnteredFunction(State.Low, OnEnteredLow);
            m_fsm.RegisterExitedFunction(State.Low, OnExitedLow);

            m_fsm.RegisterEnteringFunction(State.High, OnEnteringHigh);
            m_fsm.RegisterIsReadyToEnterFunction(State.High, IsReadyToEnterHigh);
            m_fsm.RegisterEnteredFunction(State.High, OnEnteredHigh);
            m_fsm.RegisterExitedFunction(State.High, OnExitedHigh);
            
            m_controller = controller;
            m_spaceManager = spaceManager;
            m_parent = parent;
            
            m_isVisible = true;
            m_isVisibleHierarchy = true;

            m_boundsLength = m_bounds.extents.x * m_bounds.extents.x + m_bounds.extents.z * m_bounds.extents.z;
        }

        public bool IsLoadDone()
        {
            if (m_parent == null && m_fsm.CurrentState == State.Release)
                return false;
            
            if (m_fsm.LastState != m_fsm.CurrentState)
                return false;

            if (m_fsm.CurrentState == State.High)
            {
                for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
                {
                    var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                    if ( childTreeNode.IsLoadDone() == false )
                        return false;
                }
                
                return m_highObjectIds.Count == m_highObjects.Count;
            }
            else if ( m_fsm.CurrentState == State.Low)
            {
                return m_lowObjectIds.Count == m_lowObjects.Count;
            }

            return true;
        }

        public bool IsNodeReadySelf()
        {
            return m_expectedState == m_fsm.CurrentState;
        }

        public int GetReadyNodeCount()
        {
            int readyNodeCount = 0;
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                readyNodeCount += childTreeNode.GetReadyNodeCount();
            }

            if (m_fsm.LastState == State.Release)
                return readyNodeCount + 1;
            if (m_fsm.LastState == State.Low)
            {
                if (IsReadyToEnterLow())
                    return readyNodeCount + 1;
                else
                    return readyNodeCount;
            }
            if ( m_fsm.CurrentState == State.High)
            {
                if (IsReadyToEnterHigh())
                    return readyNodeCount + 1;
                else
                    return readyNodeCount;
                
            }
            return readyNodeCount;
        }

        public void Cull(bool isCull)
        {
            if (isCull)
            {
                Release();
            }
            else
            {
                if (m_fsm.LastState == State.Release)
                {
                    m_fsm.ChangeState(State.Low);
                }
            }
        }

        #region FSM functions

        bool IsReadyToEnterRelease()
        {
            if (m_parent == null)
                return true;

            return m_parent.m_fsm.CurrentState != State.High;
        }
        
        void OnEnteredRelease()
        {
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.m_isVisible = false;
                childTreeNode.Release();
            }
        }

        void OnEnteringLow()
        {
            if ( m_loadedLowObjects == null ) 
                m_loadedLowObjects = new Dictionary<int, GameObject>();
            
            if (m_lowObjects.Count == m_lowObjectIds.Count)
                return;
            
            for (int i = 0; i < m_lowObjectIds.Count; ++i)
            {
                int id = m_lowObjectIds[i];

                m_controller.GetLowObject(id, Level, m_distance, o =>
                {
                    o.SetActive(false);
                    m_loadedLowObjects.Add(id, o);
                });
            }
        }
        bool IsReadyToEnterLow()
        {
            if (m_loadedLowObjects == null)
                return true;

            return m_loadedLowObjects.Count == m_lowObjectIds.Count;
        }
        
        
        void OnEnteredLow()
        {
            m_lowObjects = m_loadedLowObjects;
            m_loadedLowObjects = null;

            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.Release();
            }
            
        }

        void OnExitedLow()
        {
            foreach (var item in m_lowObjects)
            {
                item.Value.SetActive(false);
                m_controller.ReleaseLowObject(item.Key);
            }
            m_lowObjects.Clear();
        }

        void OnEnteringHigh()
        {
            //child low mesh should be load before change to high.
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.m_isVisible = false;
                childTreeNode.m_fsm.ChangeState(State.Low);
            }

            if ( m_loadedHighObjects == null )
                m_loadedHighObjects = new Dictionary<int, GameObject>();
            
            if (m_loadedHighObjects.Count == m_highObjectIds.Count)
                return;

            
            for (int i = 0; i < m_highObjectIds.Count; ++i)
            {
                int id = m_highObjectIds[i];

                m_controller.GetHighObject(id, Level, m_distance, (o =>
                {
                    o.SetActive(false);
                    m_loadedHighObjects.Add(id, o);
                }));
            }
        }

        bool IsReadyToEnterHigh()
        {
            if (m_loadedHighObjects == null)
                return true;

            if ( m_loadedHighObjects.Count != m_highObjectIds.Count )
                return false;
            
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                if (childTreeNode.m_fsm.CurrentState == State.Release)
                    return false;
            }

            return true;
        }
        void OnEnteredHigh()
        {
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.m_isVisible = true;
            }
            
            m_highObjects = m_loadedHighObjects;
            m_loadedHighObjects = null;
        }

        void OnExitedHigh()
        {
            foreach (var item in m_highObjects)
            {
                item.Value.SetActive(false);
                m_controller.ReleaseHighObject(item.Key);
            }
            m_highObjects.Clear();
            
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.Release();
                childTreeNode.m_isVisible = false;
            }
        }


        void Release()
        {
            m_fsm.ChangeState(State.Release);
        }
        #endregion
        

        public void Update(float lodDistance)
        {
            m_distance = m_spaceManager.GetDistanceSqure(m_bounds) - m_boundsLength;

            //Change state if a change to another state is needed immediately after changing the state.
            var beforeState = m_fsm.CurrentState;
            m_expectedState = m_spaceManager.IsHigh(lodDistance, m_bounds) ? State.High : State.Low;

            if ( m_parent != null)
            {
                if ( m_parent.ExprectedState == State.Release || m_parent.ExprectedState == State.Low)
                {
                    m_expectedState = State.Release;
                }
            }

            do
            {
                beforeState = m_fsm.CurrentState;
                if (m_fsm.LastState != State.Release)
                {
                    if (m_expectedState == State.High)
                    {
                        //if isVisible is false, it loaded from parent but not showing. 
                        //We have to wait for showing after then, change state to high.
                        if (m_fsm.CurrentState == State.Low &&
                            m_isVisible == true)
                        {
                            m_fsm.ChangeState(State.High);
                        }
                    }
                    else
                    {
                        m_fsm.ChangeState(State.Low);
                    }
                }

                m_fsm.Update();
            } while (beforeState != m_fsm.CurrentState);

            UpdateVisible();
            
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.Update(lodDistance);
            }
        }

        static Material lineMaterial;
        static void CreateLineMaterial()
        {
            if (!lineMaterial)
            {
                // Unity has a built-in shader that is useful for drawing
                // simple colored things.
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                lineMaterial = new Material(shader);
                lineMaterial.hideFlags = HideFlags.HideAndDontSave;
                // Turn on alpha blending
                lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // Turn backface culling off
                lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                // Turn off depth writes
                lineMaterial.SetInt("_ZWrite", 0);
            }
        }



        public void RenderBounds()
        {
            if (m_fsm.CurrentState == State.Release)
                return;

            for ( int i = 0; i < m_childTreeNodeIds.Count; ++i )
            {
                m_container.Get(m_childTreeNodeIds[i]).RenderBounds();
            }

            //if this node has a child node, skipping render.
            if (m_fsm.CurrentState == State.High && m_childTreeNodeIds.Count > 0)
                return;

            Color color = Color.white;

            if (m_fsm.CurrentState == State.Low)
                color = Color.yellow;
            else
                color = Color.green;

            Vector3 min = m_bounds.min;
            Vector3 max = m_bounds.max;

            Vector3[] vertices = new Vector3[8];
            vertices[0] = new Vector3(min.x, min.y, min.z);
            vertices[1] = new Vector3(min.x, min.y, max.z);
            vertices[2] = new Vector3(max.x, min.y, max.z);
            vertices[3] = new Vector3(max.x, min.y, min.z);
            
            vertices[4] = new Vector3(min.x, max.y, min.z);
            vertices[5] = new Vector3(min.x, max.y, max.z);
            vertices[6] = new Vector3(max.x, max.y, max.z);
            vertices[7] = new Vector3(max.x, max.y, min.z);

            CreateLineMaterial();
            // Apply the line material
            lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);

            GL.Color(color);

            //bottom
            GL.Vertex(vertices[0]); GL.Vertex(vertices[1]);
            GL.Vertex(vertices[1]); GL.Vertex(vertices[2]);
            GL.Vertex(vertices[2]); GL.Vertex(vertices[3]);
            GL.Vertex(vertices[3]); GL.Vertex(vertices[0]);

            //center
            GL.Vertex(vertices[0]); GL.Vertex(vertices[4]);
            GL.Vertex(vertices[1]); GL.Vertex(vertices[5]);
            GL.Vertex(vertices[2]); GL.Vertex(vertices[6]);
            GL.Vertex(vertices[3]); GL.Vertex(vertices[7]);

            //top
            GL.Vertex(vertices[4]); GL.Vertex(vertices[5]);
            GL.Vertex(vertices[5]); GL.Vertex(vertices[6]);
            GL.Vertex(vertices[6]); GL.Vertex(vertices[7]);
            GL.Vertex(vertices[7]); GL.Vertex(vertices[4]);

            GL.End();
            GL.PopMatrix();
        }        

        private void UpdateVisible()
        {
            if (m_parent != null)
            {
                m_isVisibleHierarchy = m_isVisible && m_parent.m_isVisibleHierarchy;
            }
            else
            {
                m_isVisibleHierarchy = m_isVisible;    
            }

            foreach (var item in m_highObjects)
            {
                item.Value.SetActive(m_isVisibleHierarchy);
            }

            foreach (var item in m_lowObjects)
            {
                item.Value.SetActive(m_isVisibleHierarchy);
            }
        }

    }

}