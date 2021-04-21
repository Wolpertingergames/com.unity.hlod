﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.HLODSystem.SpaceManager
{
    public interface ISpaceSplitter
    {
        int CalculateTreeDepth(Bounds bounds, float chunkSize);
        /**
         * @return created root space tree
         */
        SpaceNode CreateSpaceTree(Bounds initBounds, float chunkSize, Vector3 rootPosition, List<GameObject> targetObjects, Action<float> onProgress);
    }

}