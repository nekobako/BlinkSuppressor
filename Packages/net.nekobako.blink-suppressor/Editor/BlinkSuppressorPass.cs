#if BS_VRCSDK3_AVATARS

using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.vrchat;
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
            var descriptor = context.VRChatAvatarDescriptor();
            var renderer = descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
            var shapes = descriptor.customEyeLookSettings.eyelidsBlendshapes;
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
                var blinkBlendShapeAffectionsPerVertex = new bool[vertices.Length];
                var blinkBlendShapeAffectionsPerPrimitive = new bool[primitives.Length];
                for (var i = 0; i < blinkBlendShape.FrameCount; i++)
                {
                    var blinkBlendShapeFrame = blendShapeFrames[blinkBlendShape.FrameIndex + i];
                    for (var j = 0; j < blinkBlendShapeFrame.DeltaCount; j++)
                    {
                        var blinkBlendShapeDelta = blendShapeDeltas[blinkBlendShapeFrame.DeltaIndex + j];
                        blinkBlendShapeAffectionsPerVertex[j] |= blinkBlendShapeDelta.Position.sqrMagnitude >= component.BlendShapeThreshold * component.BlendShapeThreshold;
                    }
                }
                for (var i = 0; i < subMeshes.Length; i++)
                {
                    var subMesh = subMeshes[i];
                    for (var j = 0; j < subMesh.PrimitiveCount; j++)
                    {
                        var primitive = primitives[subMesh.PrimitiveIndex + j];
                        blinkBlendShapeAffectionsPerPrimitive[subMesh.PrimitiveIndex + j] =
                            subMesh.TopologySize > 0 && blinkBlendShapeAffectionsPerVertex[primitive.Index0] ||
                            subMesh.TopologySize > 1 && blinkBlendShapeAffectionsPerVertex[primitive.Index1] ||
                            subMesh.TopologySize > 2 && blinkBlendShapeAffectionsPerVertex[primitive.Index2] ||
                            subMesh.TopologySize > 3 && blinkBlendShapeAffectionsPerVertex[primitive.Index3];
                    }
                }

                var vertexIndexMap = new int[vertices.Length];
                var newVertexIndex = 0;
                var newBoneWeightIndex = 0;
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertexIndexMap[i] = newVertexIndex;
                    newVertexIndex += blinkBlendShapeAffectionsPerVertex[i] ? 2 : 1;
                }

                var newVertices = new NativeArray<MeshData.Vertex>(newVertexIndex, Allocator.Temp);
                var newVerticesSpan = newVertices.AsSpan();
                newVertexIndex = 0;
                newBoneWeightIndex = 0;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var vertex = vertices[i];
                    vertex.BoneWeightIndex = newBoneWeightIndex;
                    newVerticesSpan[newVertexIndex] = vertex;
                    newVertexIndex++;
                    newBoneWeightIndex += vertex.BoneWeightCount;

                    if (blinkBlendShapeAffectionsPerVertex[i])
                    {
                        vertex = vertices[i];
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
                    boneWeights.Slice(vertex.BoneWeightIndex, vertex.BoneWeightCount).CopyTo(newBoneWeightsSpan[newBoneWeightIndex..]);
                    newBoneWeightIndex += vertex.BoneWeightCount;

                    if (blinkBlendShapeAffectionsPerVertex[i])
                    {
                        vertex = vertices[i];
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
                        newPrimitiveCount += blinkBlendShapeAffectionsPerPrimitive[subMesh.PrimitiveIndex + j] ? 2 : 1;
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
                        primitive.Index0 = vertexIndexMap[primitive.Index0];
                        primitive.Index1 = vertexIndexMap[primitive.Index1];
                        primitive.Index2 = vertexIndexMap[primitive.Index2];
                        primitive.Index3 = vertexIndexMap[primitive.Index3];
                        newPrimitivesSpan[newPrimitiveIndex] = primitive;
                        newPrimitiveIndex++;

                        if (blinkBlendShapeAffectionsPerPrimitive[subMesh.PrimitiveIndex + j])
                        {
                            primitive = primitives[subMesh.PrimitiveIndex + j];
                            primitive.Index0 = vertexIndexMap[primitive.Index0] + (blinkBlendShapeAffectionsPerVertex[primitive.Index0] ? 1 : 0);
                            primitive.Index1 = vertexIndexMap[primitive.Index1] + (blinkBlendShapeAffectionsPerVertex[primitive.Index1] ? 1 : 0);
                            primitive.Index2 = vertexIndexMap[primitive.Index2] + (blinkBlendShapeAffectionsPerVertex[primitive.Index2] ? 1 : 0);
                            primitive.Index3 = vertexIndexMap[primitive.Index3] + (blinkBlendShapeAffectionsPerVertex[primitive.Index3] ? 1 : 0);
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
                AddBlendShapeFrame(newBlendShapeFrames, ref newBlendShapeFrameIndex, ref newBlendShapeDeltaIndex, newVerticesSpan.Length, 50.0f);
                AddBlendShapeFrame(newBlendShapeFrames, ref newBlendShapeFrameIndex, ref newBlendShapeDeltaIndex, newVerticesSpan.Length, BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(50.0f) + 1));
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
                            newBlendShapeDeltasSpan[newBlendShapeDeltaIndex] = blendShapeDelta;
                            newBlendShapeDeltaIndex++;

                            if (blinkBlendShapeAffectionsPerVertex[k])
                            {
                                blendShapeDelta = blendShapeDeltas[blendShapeFrame.DeltaIndex + k];
                                newBlendShapeDeltasSpan[newBlendShapeDeltaIndex] = i == blinkBlendShapeIndex ? default : blendShapeDelta;
                                newBlendShapeDeltaIndex++;
                            }
                        }
                    }
                }
                AddBlendShapeDelta(newBlendShapeDeltas, ref newBlendShapeDeltaIndex, blinkBlendShapeAffectionsPerVertex, Vector3.zero, Vector3.positiveInfinity);
                AddBlendShapeDelta(newBlendShapeDeltas, ref newBlendShapeDeltaIndex, blinkBlendShapeAffectionsPerVertex, Vector3.positiveInfinity, Vector3.zero);
                AddBlendShapeDelta(newBlendShapeDeltas, ref newBlendShapeDeltaIndex, blinkBlendShapeAffectionsPerVertex, Vector3.positiveInfinity, Vector3.zero);

                static void AddBlendShapeDelta(Span<MeshData.BlendShapeDelta> blendShapeDeltas, ref int blendShapeDeltaIndex, bool[] blinkBlendShapeAffectionsPerVertex, Vector3 positionForAffectedVertex, Vector3 positionForSuppressedVertex)
                {
                    foreach (var blinkBlendShapeAffection in blinkBlendShapeAffectionsPerVertex)
                    {
                        if (blinkBlendShapeAffection)
                        {
                            blendShapeDeltas[blendShapeDeltaIndex] = new() { Position = positionForAffectedVertex };
                            blendShapeDeltaIndex++;
                            blendShapeDeltas[blendShapeDeltaIndex] = new() { Position = positionForSuppressedVertex };
                            blendShapeDeltaIndex++;
                        }
                        else
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
                renderer.SetBlendShapeWeight(newBlendShapes.Length - 1, 50.0f);

                var asc = context.Extension<AnimatorServicesContext>();
                var ecb = EditorCurveBinding.FloatCurve(asc.ObjectPathRemapper.GetVirtualPathForObject(component.transform), component.GetType(), nameof(BlinkSuppressor.SuppressBlink));
                asc.AnimationIndex.EditClipsByBinding(new[] { ecb }, clip =>
                {
                    var curve = clip.GetFloatCurve(ecb);
                    clip.SetFloatCurve(ecb, null);
                    clip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(asc.ObjectPathRemapper.GetVirtualPathForObject(renderer.transform), renderer.GetType(), $"blendShape.{newBlendShapes[^1].Name}"),
                        new(Array.ConvertAll(curve.keys, x => { x.value = x.value < 0.5f ? 50.0f : 100.0f; return x; })));
                    clip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(asc.ObjectPathRemapper.GetVirtualPathForObject(renderer.transform), renderer.GetType(), "m_UpdateWhenOffscreen"),
                        new(Array.ConvertAll(curve.keys, x => { x.value = 0.0f; return x; })));
                });

                newMeshData.Dispose();
            }

            Object.DestroyImmediate(component);
        }
    }
}

#endif
