#if BS_VRCSDK3_AVATARS

using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Object = UnityEngine.Object;

namespace net.nekobako.BlinkSuppressor.Editor
{
    using Runtime;

    internal class BlinkSuppressorPass : Pass<BlinkSuppressorPass>
    {
        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<BlinkSuppressor>(true);
            if (components.Length == 0)
            {
                return;
            }
            if (components.Length > 1)
            {
                throw new("There are multiple BlinkSuppressor components.");
            }

            var component = components[0];
            var renderer = context.AvatarDescriptor.customEyeLookSettings.eyelidsSkinnedMesh;
            var shapes = context.AvatarDescriptor.customEyeLookSettings.eyelidsBlendshapes;
            if (renderer != null && shapes.Length > 0)
            {
                var mesh = Object.Instantiate(renderer.sharedMesh);
                var meshData = new MeshData(mesh, Allocator.Temp);
                var vertices = meshData.Vertices;
                var boneWeights = meshData.BoneWeights;
                var subMeshes = meshData.SubMeshes;
                var primitives = meshData.Primitives;
                var blendShapes = meshData.BlendShapes;
                var blendShapeFrames = meshData.BlendShapeFrames;
                var blendShapeDeltas = meshData.BlendShapeDeltas;

                var blinkBlendShapeIndex = shapes[0];
                var blinkBlendShape = blendShapes[blinkBlendShapeIndex];

                // 瞬きブレンドシェイプで動く頂点
                //  → 瞬き許可時に表示するものと、瞬き不許可時に表示するものの 2 個に増やす
                // 瞬きブレンドシェイプで動かないが、瞬きブレンドシェイプで動く頂点と同じプリミティブに含まれる頂点
                //  → 瞬き許可時に表示するものと、瞬き不許可時に表示するものと、無関係のプリミティブを維持するためのものの 3 個に増やす
                // 瞬きブレンドシェイプで動く頂点を含むプリミティブ
                //  → 瞬き許可時に表示するものと、瞬き不許可時に表示するものの 2 個に増やす
                var newVertexCounts = new int[vertices.Length];
                var newPrimitiveCounts = new int[primitives.Length];
                for (var i = 0; i < blinkBlendShape.FrameCount; i++)
                {
                    var blinkBlendShapeFrame = blendShapeFrames[blinkBlendShape.FrameIndex + i];
                    for (var j = 0; j < blinkBlendShapeFrame.DeltaCount; j++)
                    {
                        var blinkBlendShapeDelta = blendShapeDeltas[blinkBlendShapeFrame.DeltaIndex + j];
                        if (newVertexCounts[j] != 2)
                        {
                            newVertexCounts[j] = blinkBlendShapeDelta.Position.sqrMagnitude >= component.BlendShapeThreshold * component.BlendShapeThreshold ? 2 : 1;
                        }
                    }
                }
                for (var i = 0; i < subMeshes.Length; i++)
                {
                    var subMesh = subMeshes[i];
                    for (var j = 0; j < subMesh.PrimitiveCount; j++)
                    {
                        var primitive = primitives[subMesh.PrimitiveIndex + j];
                        if (subMesh.TopologySize > 0 && newVertexCounts[primitive.Index0] == 2 ||
                            subMesh.TopologySize > 1 && newVertexCounts[primitive.Index1] == 2 ||
                            subMesh.TopologySize > 2 && newVertexCounts[primitive.Index2] == 2 ||
                            subMesh.TopologySize > 3 && newVertexCounts[primitive.Index3] == 2)
                        {
                            if (subMesh.TopologySize > 0 && newVertexCounts[primitive.Index0] == 1)
                            {
                                newVertexCounts[primitive.Index0] = 3;
                            }
                            if (subMesh.TopologySize > 1 && newVertexCounts[primitive.Index1] == 1)
                            {
                                newVertexCounts[primitive.Index1] = 3;
                            }
                            if (subMesh.TopologySize > 2 && newVertexCounts[primitive.Index2] == 1)
                            {
                                newVertexCounts[primitive.Index2] = 3;
                            }
                            if (subMesh.TopologySize > 3 && newVertexCounts[primitive.Index3] == 1)
                            {
                                newVertexCounts[primitive.Index3] = 3;
                            }
                            newPrimitiveCounts[subMesh.PrimitiveIndex + j] = 2;
                        }
                        else
                        {
                            newPrimitiveCounts[subMesh.PrimitiveIndex + j] = 1;
                        }
                    }
                }

                var vertexIndexMap = new int[vertices.Length];
                var newVertexIndex = 0;
                var newBoneWeightIndex = 0;
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertexIndexMap[i] = newVertexIndex;
                    newVertexIndex += newVertexCounts[i];
                }

                var newVertices = new NativeArray<MeshData.Vertex>(newVertexIndex, Allocator.Temp);
                var newVerticesSpan = newVertices.AsSpan();
                newVertexIndex = 0;
                newBoneWeightIndex = 0;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var vertex = vertices[i];
                    for (var j = 0; j < newVertexCounts[i]; j++)
                    {
                        vertex.BoneWeightIndex = newBoneWeightIndex;
                        newVerticesSpan[newVertexIndex] = vertex;
                        newVertexIndex++;
                        newBoneWeightIndex += vertex.BoneWeightCount;
                    }
                }

                var newBoneWeights = new NativeArray<BoneWeight1>(newBoneWeightIndex, Allocator.Temp);
                var newBoneWeightsSpan = newBoneWeights.AsSpan();
                newBoneWeightIndex = 0;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var vertex = vertices[i];
                    for (var j = 0; j < newVertexCounts[i]; j++)
                    {
                        boneWeights.Slice(vertex.BoneWeightIndex, vertex.BoneWeightCount).CopyTo(newBoneWeightsSpan[newBoneWeightIndex..]);
                        newBoneWeightIndex += vertex.BoneWeightCount;
                    }
                }

                var newSubMeshes = new NativeArray<MeshData.SubMesh>(subMeshes.Length, Allocator.Temp);
                var newSubMeshesSpan = newSubMeshes.AsSpan();
                var newSubMeshIndex = 0;
                var newPrimitiveIndex = 0;
                for (var i = 0; i < subMeshes.Length; i++)
                {
                    var subMesh = subMeshes[i];
                    var newPrimitiveCount = 0;
                    for (var j = 0; j < subMesh.PrimitiveCount; j++)
                    {
                        newPrimitiveCount += newPrimitiveCounts[subMesh.PrimitiveIndex + j];
                    }
                    subMesh.PrimitiveIndex = newPrimitiveIndex;
                    subMesh.PrimitiveCount = newPrimitiveCount;
                    newSubMeshesSpan[newSubMeshIndex] = subMesh;
                    newSubMeshIndex++;
                    newPrimitiveIndex += subMesh.PrimitiveCount;
                }

                var newPrimitives = new NativeArray<MeshData.Primitive>(newPrimitiveIndex, Allocator.Temp);
                var newPrimitivesSpan = newPrimitives.AsSpan();
                newPrimitiveIndex = 0;
                for (var i = 0; i < subMeshes.Length; i++)
                {
                    var subMesh = subMeshes[i];
                    for (var j = 0; j < subMesh.PrimitiveCount; j++)
                    {
                        var primitive = primitives[subMesh.PrimitiveIndex + j];
                        if (newPrimitiveCounts[subMesh.PrimitiveIndex + j] == 1)
                        {
                            primitive.Index0 = vertexIndexMap[primitive.Index0] + (newVertexCounts[primitive.Index0] == 3 ? 2 : 0);
                            primitive.Index1 = vertexIndexMap[primitive.Index1] + (newVertexCounts[primitive.Index1] == 3 ? 2 : 0);
                            primitive.Index2 = vertexIndexMap[primitive.Index2] + (newVertexCounts[primitive.Index2] == 3 ? 2 : 0);
                            primitive.Index3 = vertexIndexMap[primitive.Index3] + (newVertexCounts[primitive.Index3] == 3 ? 2 : 0);
                            newPrimitivesSpan[newPrimitiveIndex] = primitive;
                            newPrimitiveIndex++;
                        }
                        if (newPrimitiveCounts[subMesh.PrimitiveIndex + j] == 2)
                        {
                            primitive.Index0 = vertexIndexMap[primitive.Index0];
                            primitive.Index1 = vertexIndexMap[primitive.Index1];
                            primitive.Index2 = vertexIndexMap[primitive.Index2];
                            primitive.Index3 = vertexIndexMap[primitive.Index3];
                            newPrimitivesSpan[newPrimitiveIndex] = primitive;
                            newPrimitiveIndex++;
                            primitive.Index0++;
                            primitive.Index1++;
                            primitive.Index2++;
                            primitive.Index3++;
                            newPrimitivesSpan[newPrimitiveIndex] = primitive;
                            newPrimitiveIndex++;
                        }
                    }
                }

                var newBlendShapes = new NativeArray<MeshData.BlendShape>(blendShapes.Length + 1, Allocator.Temp);
                var newBlendShapesSpan = newBlendShapes.AsSpan();
                var newBlendShapeIndex = blendShapes.Length;
                var newBlendShapeFrameIndex = blendShapeFrames.Length;
                var newBlendShapeDeltaIndex = blendShapeDeltas.Length;
                blendShapes.CopyTo(newBlendShapesSpan);
                AddBlendShape(newBlendShapes, ref newBlendShapeIndex, ref newBlendShapeFrameIndex, 3, $"BlinkSuppressor_{GUID.Generate()}");

                static void AddBlendShape(Span<MeshData.BlendShape> blendShapes, ref int blendShapeIndex, ref int blendShapeFrameIndex, int blendShapeFrameCount, string blendShapeName)
                {
                    blendShapes[blendShapeIndex] = new()
                    {
                        Name = blendShapeName,
                        FrameIndex = blendShapeFrameIndex,
                        FrameCount = blendShapeFrameCount,
                    };
                    blendShapeIndex++;
                    blendShapeFrameIndex += blendShapeFrameCount;
                }

                var newBlendShapeFrames = new NativeArray<MeshData.BlendShapeFrame>(newBlendShapeFrameIndex, Allocator.Temp);
                var newBlendShapeFramesSpan = newBlendShapeFrames.AsSpan();
                newBlendShapeFrameIndex = 0;
                newBlendShapeDeltaIndex = 0;
                for (var i = 0; i < blendShapeFrames.Length; i++)
                {
                    var blendShapeFrame = blendShapeFrames[i];
                    blendShapeFrame.DeltaIndex = newBlendShapeDeltaIndex;
                    blendShapeFrame.DeltaCount = newVerticesSpan.Length;
                    newBlendShapeFramesSpan[newBlendShapeFrameIndex] = blendShapeFrame;
                    newBlendShapeFrameIndex++;
                    newBlendShapeDeltaIndex += blendShapeFrame.DeltaCount;
                }
                AddBlendShapeFrame(newBlendShapeFrames, ref newBlendShapeFrameIndex, ref newBlendShapeDeltaIndex, newVerticesSpan.Length, 1.0f);
                AddBlendShapeFrame(newBlendShapeFrames, ref newBlendShapeFrameIndex, ref newBlendShapeDeltaIndex, newVerticesSpan.Length, BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(1.0f) + 1));
                AddBlendShapeFrame(newBlendShapeFrames, ref newBlendShapeFrameIndex, ref newBlendShapeDeltaIndex, newVerticesSpan.Length, 100.0f);

                static void AddBlendShapeFrame(Span<MeshData.BlendShapeFrame> blendShapeFrames, ref int blendShapeFrameIndex, ref int blendShapeDeltaIndex, int blendShapeDeltaCount, float weight)
                {
                    blendShapeFrames[blendShapeFrameIndex] = new()
                    {
                        Weight = weight,
                        DeltaIndex = blendShapeDeltaIndex,
                        DeltaCount = blendShapeDeltaCount,
                    };
                    blendShapeFrameIndex++;
                    blendShapeDeltaIndex += blendShapeDeltaCount;
                }

                var newBlendShapeDeltas = new NativeArray<MeshData.BlendShapeDelta>(newBlendShapeDeltaIndex, Allocator.Temp);
                var newBlendShapeDeltasSpan = newBlendShapeDeltas.AsSpan();
                newBlendShapeDeltaIndex = 0;
                for (var i = 0; i < blendShapes.Length; i++)
                {
                    var blendShape = blendShapes[i];
                    for (var j = 0; j < blendShape.FrameCount; j++)
                    {
                        var blendShapeFrame = blendShapeFrames[blendShape.FrameIndex + j];
                        for (var k = 0; k < blendShapeFrame.DeltaCount; k++)
                        {
                            var blendShapeDelta = blendShapeDeltas[blendShapeFrame.DeltaIndex + k];
                            for (var l = 0; l < newVertexCounts[k]; l++)
                            {
                                newBlendShapeDeltasSpan[newBlendShapeDeltaIndex] = i == blinkBlendShapeIndex && l == 1 ? default : blendShapeDelta;
                                newBlendShapeDeltaIndex++;
                            }
                        }
                    }
                }
                AddBlendShapeDelta(newBlendShapeDeltas, ref newBlendShapeDeltaIndex, newVertexCounts, Vector3.zero, component.DeltaPosition);
                AddBlendShapeDelta(newBlendShapeDeltas, ref newBlendShapeDeltaIndex, newVertexCounts, component.DeltaPosition, Vector3.zero);
                AddBlendShapeDelta(newBlendShapeDeltas, ref newBlendShapeDeltaIndex, newVertexCounts, component.DeltaPosition, Vector3.zero);

                static void AddBlendShapeDelta(Span<MeshData.BlendShapeDelta> blendShapeDeltas, ref int blendShapeDeltaIndex, int[] vertexCounts, Vector3 positionForAffectedVertex, Vector3 positionForSuppressedVertex)
                {
                    foreach (var vertexCount in vertexCounts)
                    {
                        if (vertexCount != 1)
                        {
                            blendShapeDeltas[blendShapeDeltaIndex] = new() { Position = positionForAffectedVertex };
                            blendShapeDeltaIndex++;
                            blendShapeDeltas[blendShapeDeltaIndex] = new() { Position = positionForSuppressedVertex };
                            blendShapeDeltaIndex++;
                        }
                        if (vertexCount != 2)
                        {
                            blendShapeDeltas[blendShapeDeltaIndex] = default;
                            blendShapeDeltaIndex++;
                        }
                    }
                }

                meshData.Dispose();

                var newMeshData = new MeshData(newVertices, newBoneWeights, newSubMeshes, newPrimitives, newBlendShapes, newBlendShapeFrames, newBlendShapeDeltas);
                newMeshData.ApplyTo(mesh);

                renderer.sharedMesh = mesh;
                renderer.SetBlendShapeWeight(newBlendShapes.Length - 1, component.SuppressBlink ? 100.0f : 1.0f);

                var asc = context.Extension<AnimatorServicesContext>();
                var ecb = EditorCurveBinding.FloatCurve(asc.ObjectPathRemapper.GetVirtualPathForObject(component.transform), component.GetType(), nameof(BlinkSuppressor.SuppressBlink));
                asc.AnimationIndex.EditClipsByBinding(new[] { ecb }, clip =>
                {
                    var curve = clip.GetFloatCurve(ecb);
                    clip.SetFloatCurve(ecb, null);
                    clip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(asc.ObjectPathRemapper.GetVirtualPathForObject(renderer.transform), renderer.GetType(), $"blendShape.{newBlendShapes[^1].Name}"),
                        new(Array.ConvertAll(curve.keys, x => { x.value = x.value >= 0.5f ? 100.0f : 1.0f; return x; })));
                });

                newMeshData.Dispose();
            }

            Object.DestroyImmediate(component);
        }
    }
}

#endif
