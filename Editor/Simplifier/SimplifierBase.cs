using System;
using System.Collections;
using System.Collections.Generic;
using Unity.HLODSystem.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.HLODSystem.Simplifier
{
    public abstract class SimplifierBase : ISimplifier
    {
        protected dynamic m_options;
        public SimplifierBase(SerializableDynamicObject simplifierOptions)
        {
            m_options = simplifierOptions;
        }
        public IEnumerator Simplify(HLODBuildInfo buildInfo)
        {
            for (int i = 0; i < buildInfo.WorkingObjects.Count; ++i)
            {
                Utils.WorkingMesh mesh = buildInfo.WorkingObjects[i].Mesh;

                int triangleCount = mesh.triangles.Length / 3;
                float maxQuality = Mathf.Min((float)m_options.SimplifyMaxPolygonCount / (float)triangleCount, (float)m_options.SimplifyPolygonRatio);
                float minQuality = Mathf.Max((float)m_options.SimplifyMinPolygonCount / (float)triangleCount, 0.0f);

                var ratio = maxQuality * Mathf.Pow((float)m_options.SimplifyPolygonRatio, buildInfo.Distances[i]);
                ratio = Mathf.Max(ratio, minQuality);

                
//                while (Cache.SimplifiedCache.IsGenerating(GetType(), mesh, ratio) == true)
//                {
//                    yield return null;
//                }
//                Mesh simplifiedMesh = Cache.SimplifiedCache.Get(GetType(), mesh, ratio);
//                if (simplifiedMesh == null)
//                {
//                    Cache.SimplifiedCache.MarkGenerating(GetType(), mesh, ratio);
                    yield return GetSimplifiedMesh(mesh, ratio, (m) =>
                    {
                        buildInfo.WorkingObjects[i].SetMesh(m);
                    });
//                    Cache.SimplifiedCache.Update(GetType(), mesh, simplifiedMesh, ratio);
                    
//                }

            }            
        }

        public void SimplifyImmidiate(HLODBuildInfo buildInfo)
        {
            
            IEnumerator routine = Simplify(buildInfo);
            CustomCoroutine coroutine = new CustomCoroutine(routine);
            while (coroutine.MoveNext())
            {
                
            }
            
        }

        protected abstract IEnumerator GetSimplifiedMesh(Utils.WorkingMesh origin, float quality, Action<Utils.WorkingMesh> resultCallback);

        protected static void OnGUIBase(SerializableDynamicObject simplifierOptions)
        {
            EditorGUI.indentLevel += 1;

            dynamic options = simplifierOptions;

            if (options.SimplifyPolygonRatio == null)
                options.SimplifyPolygonRatio = 0.8f;
            if (options.SimplifyMinPolygonCount == null)
                options.SimplifyMinPolygonCount = 10;
            if (options.SimplifyMaxPolygonCount == null)
                options.SimplifyMaxPolygonCount = 500;


            if (options.PreserveBorderEdges == null)
                options.PreserveBorderEdges = false;
            if (options.PreserveUVSeamEdges == null)
                options.PreserveUVSeamEdges = false;
            if (options.PreserveUVFoldoverEdges == null)
                options.PreserveUVFoldoverEdges = false;
            if (options.PreserveSurfaceCurvature == null)
                options.PreserveSurfaceCurvature = false;
            if (options.EnableSmartLink == null)
                options.EnableSmartLink = true;
            if (options.VertexLinkDistance == null)
                options.VertexLinkDistance = 4.94065645841247e-324;
            if (options.MaxIterationCount == null)
                options.MaxIterationCount = 1;
            if (options.Agressiveness == null)
                options.Agressiveness = 7;
            if (options.ManualUVComponentCount == null)
                options.ManualUVComponentCount = false;
            if (options.UVComponentCount == null)
                options.UVComponentCount = 2;


            options.SimplifyPolygonRatio = EditorGUILayout.Slider("Polygon Ratio", options.SimplifyPolygonRatio, 0.0f, 1.0f);
            EditorGUILayout.LabelField("Triangle Range");
            EditorGUI.indentLevel += 1;
            options.SimplifyMinPolygonCount = EditorGUILayout.IntSlider("Min", options.SimplifyMinPolygonCount, 10, 100);
            options.SimplifyMaxPolygonCount = EditorGUILayout.IntSlider("Max", options.SimplifyMaxPolygonCount, 10, 5000);

            options.PreserveBorderEdges = EditorGUILayout.Toggle("Preserve Border Edges", options.PreserveBorderEdges);
            options.PreserveUVSeamEdges = EditorGUILayout.Toggle("Preserve UV Seam Edges", options.PreserveUVSeamEdges);
            options.PreserveUVFoldoverEdges = EditorGUILayout.Toggle("Preserve UV Foldove rEdges", options.PreserveUVFoldoverEdges);
            options.PreserveSurfaceCurvature = EditorGUILayout.Toggle("Preserve Surface Curvature", options.PreserveSurfaceCurvature);
            options.EnableSmartLink = EditorGUILayout.Toggle("Enable Smart Link", options.EnableSmartLink);
            options.VertexLinkDistance = EditorGUILayout.DoubleField("Vertex Link Distance", options.VertexLinkDistance);
            options.MaxIterationCount = EditorGUILayout.IntField("Max Iteration Count", options.MaxIterationCount);
            options.Agressiveness = EditorGUILayout.FloatField("Agressiveness", options.Agressiveness);
            options.ManualUVComponentCount = EditorGUILayout.Toggle("Manual UV Component Count", options.ManualUVComponentCount);
            options.UVComponentCount = EditorGUILayout.IntField("UV Component Count", options.UVComponentCount);

            EditorGUI.indentLevel -= 1;

            EditorGUI.indentLevel -= 1;
        }
        
    }
}
