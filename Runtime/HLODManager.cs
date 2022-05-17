using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.HLODSystem
{
    public class HLODManager
    {
        private static HLODManager s_instance = null;
        private bool IsSRP => GraphicsSettings.renderPipelineAsset != null;

        public static HLODManager Instance
        {
            get
            {
                if ( s_instance == null )
                    s_instance = new HLODManager();
                return s_instance;
            }
        }
    
        public void Register(Streaming.HLODControllerBase controller)
        {
            if (m_activeControllers == null)
            {
                m_activeControllers = new List<Streaming.HLODControllerBase>();
                if (IsSRP)
                    RenderPipelineManager.beginCameraRendering += OnPreCull;
                else
                    Camera.onPreCull += OnPreCull;
            }
            m_activeControllers.Add(controller);
        }
        public void Unregister(Streaming.HLODControllerBase controller)
        {
            if ( m_activeControllers != null)
                m_activeControllers.Remove(controller);
        }

        private List<Streaming.HLODControllerBase> m_activeControllers = null;

        private void OnPreCull(ScriptableRenderContext context, Camera cam)
        {
            OnPreCull(cam);
        }

        public void OnPreCull(Camera cam)
        {
            if (cam == null)
                return;

#if UNITY_EDITOR
            Transform camTrans = null;

            if (EditorApplication.isPlaying == false)
            {
                if (SceneView.currentDrawingSceneView == null)
                    return;
                if (cam != SceneView.currentDrawingSceneView.camera)
                    return;
            }
            else
            {
                if (cam != HLODCameraRecognizer.RecognizedCamera)
                    return;
                else
                    camTrans = HLODCameraRecognizer.RecognizedCameraTrans;
            }
#else
            if (cam != HLODCameraRecognizer.RecognizedCamera)
                return;
#endif

            if (m_activeControllers == null)
                return;

            if (camTrans == null)
                camTrans = cam.GetComponent<Transform>();

            for (int i = 0; i < m_activeControllers.Count; ++i)
            {
                m_activeControllers[i].UpdateCull(cam, camTrans);
            }
        }

        public bool IsLoadDone()
        {
            if (m_activeControllers == null ||
                m_activeControllers.Count <= 0)
                return true;

            for (int i = 0; i < m_activeControllers.Count; ++i)
            {
                if(!m_activeControllers[i].IsLoadDone())
                {
                    return false;
                }
            }
            return true;
        }
    }
}