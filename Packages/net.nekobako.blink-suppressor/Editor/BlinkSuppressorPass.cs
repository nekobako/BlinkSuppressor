#if BS_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.vrchat;
using UnityEditor.Animations;
using Object = UnityEngine.Object;

namespace net.nekobako.BlinkSuppressor.Editor
{
    using Runtime;

    internal class BlinkSuppressorPass : Pass<BlinkSuppressorPass>
    {
        protected override void Execute(BuildContext context)
        {
            var suppressors = context.AvatarRootObject.GetComponentsInChildren<BlinkSuppressor>(true);
            if (suppressors.Length == 0)
            {
                return;
            }
            if (suppressors.Length > 1)
            {
                throw new("There are multiple BlinkSuppressor components.");
            }

            var suppressor = suppressors[0];
            var descriptor = context.VRChatAvatarDescriptor();
            var renderer = descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
            var shapes = descriptor.customEyeLookSettings.eyelidsBlendshapes;
            if (renderer != null && shapes.Length > 0)
            {
                var mesh = Object.Instantiate(renderer.sharedMesh);
                var meshData = new MeshData(mesh, Allocator.Temp);
                var vertices = meshData.Vertices;
                var boneWeights = meshData.BoneWeights;
                var bindposes = meshData.Bindposes;
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
                        blinkBlendShapeAffectionsPerVertex[j] |= blinkBlendShapeDelta.Position.sqrMagnitude >= suppressor.BlendShapeThreshold * suppressor.BlendShapeThreshold;
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
                var blinkBlendShapeAffectedVertexCount = 0;
                var blinkBlendShapeAffectedVertexIndicesPerBone = new Dictionary<int, HashSet<int>>();
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertexIndexMap[i] = newVertexIndex;
                    newVertexIndex++;

                    if (blinkBlendShapeAffectionsPerVertex[i])
                    {
                        blinkBlendShapeAffectedVertexCount++;
                        newVertexIndex++;

                        var vertex = vertices[i];
                        for (var j = 0; j < vertex.BoneWeightCount; j++)
                        {
                            var boneWeight = boneWeights[vertex.BoneWeightIndex + j];
                            if (boneWeight.weight > 0.0f)
                            {
                                if (!blinkBlendShapeAffectedVertexIndicesPerBone.TryGetValue(boneWeight.boneIndex, out var indices))
                                {
                                    blinkBlendShapeAffectedVertexIndicesPerBone[boneWeight.boneIndex] = indices = new();
                                }
                                indices.Add(i);
                            }
                        }
                    }
                }

                var blinkBlendShapeAffectedVertexIndices = new HashSet<int>();
                var blinkBlendShapeAffectedVertexBoneIndexMap = new Dictionary<int, int>();
                foreach (var (boneIndex, vertexIndices) in blinkBlendShapeAffectedVertexIndicesPerBone.OrderByDescending(x => x.Value.Count))
                {
                    blinkBlendShapeAffectedVertexBoneIndexMap[boneIndex] = bindposes.Length + blinkBlendShapeAffectedVertexBoneIndexMap.Count * 2;

                    foreach (var vertexIndex in vertexIndices)
                    {
                        blinkBlendShapeAffectedVertexIndices.Add(vertexIndex);
                    }
                    if (blinkBlendShapeAffectedVertexIndices.Count == blinkBlendShapeAffectedVertexCount)
                    {
                        break;
                    }
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
                    if (blinkBlendShapeAffectionsPerVertex[i])
                    {
                        for (var j = 0; j < vertex.BoneWeightCount; j++)
                        {
                            var boneWeight = boneWeights[vertex.BoneWeightIndex + j];
                            if (blinkBlendShapeAffectedVertexBoneIndexMap.TryGetValue(boneWeight.boneIndex, out var boneIndex))
                            {
                                boneWeight.boneIndex = boneIndex + 0;
                            }
                            newBoneWeightsSpan[newBoneWeightIndex] = boneWeight;
                            newBoneWeightIndex++;
                        }
                        for (var j = 0; j < vertex.BoneWeightCount; j++)
                        {
                            var boneWeight = boneWeights[vertex.BoneWeightIndex + j];
                            if (blinkBlendShapeAffectedVertexBoneIndexMap.TryGetValue(boneWeight.boneIndex, out var boneIndex))
                            {
                                boneWeight.boneIndex = boneIndex + 1;
                            }
                            newBoneWeightsSpan[newBoneWeightIndex] = boneWeight;
                            newBoneWeightIndex++;
                        }
                    }
                    else
                    {
                        boneWeights.Slice(vertex.BoneWeightIndex, vertex.BoneWeightCount).CopyTo(newBoneWeightsSpan[newBoneWeightIndex..]);
                        newBoneWeightIndex += vertex.BoneWeightCount;
                    }
                }

                var newBindposes = new NativeArray<Matrix4x4>(bindposes.Length + blinkBlendShapeAffectedVertexBoneIndexMap.Count * 2, Allocator.Temp);
                var newBindposesSpan = newBindposes.AsSpan();
                bindposes.CopyTo(newBindposesSpan);
                foreach (var (srcBoneIndex, dstBoneIndex) in blinkBlendShapeAffectedVertexBoneIndexMap)
                {
                    newBindposesSpan[dstBoneIndex + 0] = bindposes[srcBoneIndex];
                    newBindposesSpan[dstBoneIndex + 1] = bindposes[srcBoneIndex];
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

                var newBlendShapes = new NativeArray<MeshData.BlendShape>(blendShapes.Length, Allocator.Temp);
                var newBlendShapesSpan = newBlendShapes.AsSpan();
                blendShapes.CopyTo(newBlendShapesSpan);

                var newBlendShapeFrames = new NativeArray<MeshData.BlendShapeFrame>(blendShapeFrames.Length, Allocator.Temp);
                var newBlendShapeFramesSpan = newBlendShapeFrames.AsSpan();
                var newBlendShapeFrameIndex = 0;
                var newBlendShapeDeltaIndex = 0;
                for (var i = 0; i < blendShapeFrames.Length; i++)
                {
                    var blendShapeFrame = blendShapeFrames[i];
                    blendShapeFrame.DeltaIndex = newBlendShapeDeltaIndex;
                    blendShapeFrame.DeltaCount = newVerticesSpan.Length;
                    newBlendShapeFramesSpan[newBlendShapeFrameIndex] = blendShapeFrame;
                    newBlendShapeFrameIndex++;
                    newBlendShapeDeltaIndex += blendShapeFrame.DeltaCount;
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

                meshData.Dispose();

                var newMeshData = new MeshData(newVertices, newBoneWeights, newBindposes, newSubMeshes, newPrimitives, newBlendShapes, newBlendShapeFrames, newBlendShapeDeltas);
                newMeshData.ApplyTo(mesh);

                newMeshData.Dispose();

                var asc = context.Extension<AnimatorServicesContext>();
                var controller = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
                var parameter = new AnimatorControllerParameter
                {
                    type = AnimatorControllerParameterType.Float,
                    name = "BlinkSuppressor/SuppressBlink",
                    defaultFloat = suppressor.SuppressBlink ? 1.0f : 0.0f,
                };
                controller.Parameters = controller.Parameters.SetItem(parameter.name, parameter);

                var path = asc.ObjectPathRemapper.GetVirtualPathForObject(suppressor.transform);
                var binding = EditorCurveBinding.FloatCurve(path, typeof(BlinkSuppressor), nameof(BlinkSuppressor.SuppressBlink));
                asc.AnimationIndex.EditClipsByBinding(new[] { binding }, clip =>
                {
                    clip.SetFloatCurve(EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), parameter.name), clip.GetFloatCurve(binding));
                    clip.SetFloatCurve(binding, null);
                });

                var allowBlinkClip = VirtualClip.Create("Allow_Blink");
                var disallowBlinkClip = VirtualClip.Create("Disallow_Blink");

                var blendTree = VirtualBlendTree.Create("BlinkSuppressor");
                blendTree.BlendType = BlendTreeType.Simple1D;
                blendTree.BlendParameter = parameter.name;
                blendTree.UseAutomaticThresholds = false;
                blendTree.Children = blendTree.Children.Add(new() { Motion = allowBlinkClip, Threshold = 0.0f });
                blendTree.Children = blendTree.Children.Add(new() { Motion = disallowBlinkClip, Threshold = float.Epsilon });

                var layer = controller.AddLayer(LayerPriority.Default, "BlinkSuppressor");
                layer.StateMachine.EntryPosition = new(0.0f, 0.0f);
                layer.StateMachine.ExitPosition = new(400.0f, 0.0f);
                layer.StateMachine.AnyStatePosition = new(0.0f, 60.0f);
                layer.StateMachine.AddState("BlinkSuppressor", blendTree, new(180.0f, 0.0f));

                var bones = renderer.bones;
                Array.Resize(ref bones, newBindposes.Length);

                var physBones = context.AvatarRootObject.GetComponentsInChildren<VRCPhysBone>(true);

                foreach (var (srcBoneIndex, dstBoneIndex) in blinkBlendShapeAffectedVertexBoneIndexMap)
                {
                    bones[dstBoneIndex + 0] = new GameObject($"Allow_Blink_{bones[srcBoneIndex].name}_{GUID.Generate()}").transform;
                    bones[dstBoneIndex + 1] = new GameObject($"Disallow_Blink_{bones[srcBoneIndex].name}_{GUID.Generate()}").transform;
                    bones[dstBoneIndex + 0].SetParent(bones[srcBoneIndex], false);
                    bones[dstBoneIndex + 1].SetParent(bones[srcBoneIndex], false);

                    foreach (var physBone in physBones)
                    {
                        if (bones[srcBoneIndex].IsChildOf(physBone.GetRootTransform()))
                        {
                            physBone.ignoreTransforms.Add(bones[dstBoneIndex + 0]);
                            physBone.ignoreTransforms.Add(bones[dstBoneIndex + 1]);
                        }
                    }

                    // Inactivate GameObjects before adding VRCScaleConstraint so that VRCScaleConstraint.TargetTransform works fine in Play Mode
                    bones[dstBoneIndex + 0].gameObject.SetActive(false);
                    bones[dstBoneIndex + 1].gameObject.SetActive(false);

                    AddScaleConstraint(bones[dstBoneIndex + 0], bones[dstBoneIndex + 0], 1.0f);
                    AddScaleConstraint(bones[dstBoneIndex + 0], bones[dstBoneIndex + 1], float.NaN);
                    AddScaleConstraint(bones[dstBoneIndex + 1], bones[dstBoneIndex + 0], float.NaN);
                    AddScaleConstraint(bones[dstBoneIndex + 1], bones[dstBoneIndex + 1], 1.0f);

                    static void AddScaleConstraint(Transform transform, Transform target, float weight)
                    {
                        var constraint = transform.gameObject.AddComponent<VRCScaleConstraint>();
                        constraint.TargetTransform = target;
                        constraint.Sources.Add(new(target.parent, weight));
                        constraint.Locked = true;
                        constraint.IsActive = true;
                    }

                    // Activate GameObjects after adding VRCScaleConstraint so that VRCScaleConstraint.TargetTransform works fine in Play Mode
                    bones[dstBoneIndex + 0].gameObject.SetActive(!suppressor.SuppressBlink);
                    bones[dstBoneIndex + 1].gameObject.SetActive(suppressor.SuppressBlink);

                    var rendererPath = asc.ObjectPathRemapper.GetVirtualPathForObject(renderer.transform);
                    var allowBlinkBonePath = asc.ObjectPathRemapper.GetVirtualPathForObject(bones[dstBoneIndex + 0].transform);
                    var disallowBlinkBonePath = asc.ObjectPathRemapper.GetVirtualPathForObject(bones[dstBoneIndex + 1].transform);
                    var rendererBinding = EditorCurveBinding.FloatCurve(rendererPath, typeof(SkinnedMeshRenderer), "m_UpdateWhenOffscreen");
                    var allowBlinkBoneBinding = EditorCurveBinding.FloatCurve(allowBlinkBonePath, typeof(GameObject), "m_IsActive");
                    var disallowBlinkBoneBinding = EditorCurveBinding.FloatCurve(disallowBlinkBonePath, typeof(GameObject), "m_IsActive");
                    var trueCurve = AnimationCurve.Constant(0.0f, 0.0f, 1.0f);
                    var falseCurve = AnimationCurve.Constant(0.0f, 0.0f, 0.0f);
                    allowBlinkClip.SetFloatCurve(rendererBinding, falseCurve);
                    allowBlinkClip.SetFloatCurve(allowBlinkBoneBinding, trueCurve);
                    allowBlinkClip.SetFloatCurve(disallowBlinkBoneBinding, falseCurve);
                    disallowBlinkClip.SetFloatCurve(rendererBinding, falseCurve);
                    disallowBlinkClip.SetFloatCurve(allowBlinkBoneBinding, falseCurve);
                    disallowBlinkClip.SetFloatCurve(disallowBlinkBoneBinding, trueCurve);
                }

                renderer.sharedMesh = mesh;
                renderer.bones = bones;
            }

            Object.DestroyImmediate(suppressor);
        }
    }
}

#endif
