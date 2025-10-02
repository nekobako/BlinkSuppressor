using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace net.nekobako.BlinkSuppressor.Editor
{
    internal class MeshData : IDisposable
    {
        public struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Tangent;
            public Color32 Color;
            public Vector4 Uv0;
            public Vector4 Uv1;
            public Vector4 Uv2;
            public Vector4 Uv3;
            public Vector4 Uv4;
            public Vector4 Uv5;
            public Vector4 Uv6;
            public Vector4 Uv7;
            public int BoneWeightIndex;
            public int BoneWeightCount;
        }

        public struct SubMesh
        {
            public MeshTopology Topology;
            public byte TopologySize;
            public int PrimitiveIndex;
            public int PrimitiveCount;
        }

        public struct Primitive
        {
            public int Index0;
            public int Index1;
            public int Index2;
            public int Index3;
        }

        public struct BlendShape
        {
            public FixedString128Bytes Name;
            public int FrameIndex;
            public int FrameCount;
        }

        public struct BlendShapeFrame
        {
            public float Weight;
            public int DeltaIndex;
            public int DeltaCount;
        }

        public struct BlendShapeDelta
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector3 Tangent;
        }

        public ReadOnlySpan<Vertex> Vertices => m_Vertices;
        public ReadOnlySpan<BoneWeight1> BoneWeights => m_BoneWeights;
        public ReadOnlySpan<SubMesh> SubMeshes => m_SubMeshes;
        public ReadOnlySpan<Primitive> Primitives => m_Primitives;
        public ReadOnlySpan<BlendShape> BlendShapes => m_BlendShapes;
        public ReadOnlySpan<BlendShapeFrame> BlendShapeFrames => m_BlendShapeFrames;
        public ReadOnlySpan<BlendShapeDelta> BlendShapeDeltas => m_BlendShapeDeltas;

        private NativeArray<Vertex> m_Vertices;
        private NativeArray<BoneWeight1> m_BoneWeights;
        private NativeArray<SubMesh> m_SubMeshes;
        private NativeArray<Primitive> m_Primitives;
        private NativeArray<BlendShape> m_BlendShapes;
        private NativeArray<BlendShapeFrame> m_BlendShapeFrames;
        private NativeArray<BlendShapeDelta> m_BlendShapeDeltas;

        private delegate ref TResult RefFunc<T, TResult>(ref T arg);

        public MeshData(Mesh mesh, Allocator allocator)
        {
            ReadVertices(mesh, allocator);
            ReadSubMeshes(mesh, allocator);
            ReadBlendShapes(mesh, allocator);
        }

        public MeshData(NativeArray<Vertex> vertices, NativeArray<BoneWeight1> boneWeights, NativeArray<SubMesh> subMeshes, NativeArray<Primitive> primitives, NativeArray<BlendShape> blendShapes, NativeArray<BlendShapeFrame> blendShapeFrames, NativeArray<BlendShapeDelta> blendShapeDeltas)
        {
            m_Vertices = vertices;
            m_BoneWeights = boneWeights;
            m_SubMeshes = subMeshes;
            m_Primitives = primitives;
            m_BlendShapes = blendShapes;
            m_BlendShapeFrames = blendShapeFrames;
            m_BlendShapeDeltas = blendShapeDeltas;
        }

        private void ReadVertices(Mesh mesh, Allocator allocator)
        {
            m_Vertices = new(mesh.vertexCount, allocator);

            var vertices = m_Vertices.AsSpan();
            ReadValue(mesh, vertices, VertexAttribute.Position, (ref Vertex x) => ref x.Position, () => mesh.vertices);
            ReadValue(mesh, vertices, VertexAttribute.Normal, (ref Vertex x) => ref x.Normal, () => mesh.normals);
            ReadValue(mesh, vertices, VertexAttribute.Tangent, (ref Vertex x) => ref x.Tangent, () => mesh.tangents);
            ReadValue(mesh, vertices, VertexAttribute.Color, (ref Vertex x) => ref x.Color, () => mesh.colors32);

            static void ReadValue<T>(Mesh mesh, Span<Vertex> vertices, VertexAttribute attribute, RefFunc<Vertex, T> valueRefGetter, Func<T[]> valuesGetter)
            {
                if (mesh.HasVertexAttribute(attribute))
                {
                    var values = valuesGetter();
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        valueRefGetter(ref vertices[i]) = values[i];
                    }
                }
            }

            var uvBuffer = new List<Vector4>(vertices.Length);
            ReadUv(mesh, vertices, 0, (ref Vertex x) => ref x.Uv0, uvBuffer);
            ReadUv(mesh, vertices, 1, (ref Vertex x) => ref x.Uv1, uvBuffer);
            ReadUv(mesh, vertices, 2, (ref Vertex x) => ref x.Uv2, uvBuffer);
            ReadUv(mesh, vertices, 3, (ref Vertex x) => ref x.Uv3, uvBuffer);
            ReadUv(mesh, vertices, 4, (ref Vertex x) => ref x.Uv4, uvBuffer);
            ReadUv(mesh, vertices, 5, (ref Vertex x) => ref x.Uv5, uvBuffer);
            ReadUv(mesh, vertices, 6, (ref Vertex x) => ref x.Uv6, uvBuffer);
            ReadUv(mesh, vertices, 7, (ref Vertex x) => ref x.Uv7, uvBuffer);

            static void ReadUv(Mesh mesh, Span<Vertex> vertices, int channel, RefFunc<Vertex, Vector4> uvRefGetter, List<Vector4> uvBuffer)
            {
                if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0 + channel))
                {
                    mesh.GetUVs(channel, uvBuffer);
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        uvRefGetter(ref vertices[i]) = uvBuffer[i];
                    }
                }
            }

            if (mesh.HasVertexAttribute(VertexAttribute.BlendIndices))
            {
                var boneWeightIndex = 0;
                var boneWeightCounts = mesh.GetBonesPerVertex().AsReadOnlySpan();
                for (var i = 0; i < vertices.Length; i++)
                {
                    ref var vertex = ref vertices[i];
                    vertex.BoneWeightIndex = boneWeightIndex;
                    vertex.BoneWeightCount = boneWeightCounts[i];
                    boneWeightIndex += vertex.BoneWeightCount;
                }
            }

            m_BoneWeights = new(mesh.GetAllBoneWeights(), allocator);
        }

        private void ReadSubMeshes(Mesh mesh, Allocator allocator)
        {
            m_SubMeshes = new(mesh.subMeshCount, allocator);

            var subMeshes = m_SubMeshes.AsSpan();
            var primitiveIndex = 0;
            for (var i = 0; i < subMeshes.Length; i++)
            {
                ref var subMesh = ref subMeshes[i];
                subMesh.Topology = mesh.GetTopology(i);
                subMesh.TopologySize = subMesh.Topology switch
                {
                    MeshTopology.Points => 1,
                    MeshTopology.Lines => 2,
                    MeshTopology.Triangles => 3,
                    MeshTopology.Quads => 4,
                    _ => throw new("Target mesh has unsupported topology."),
                };
                subMesh.PrimitiveIndex = primitiveIndex;
                subMesh.PrimitiveCount = (int)mesh.GetIndexCount(i) / subMesh.TopologySize;
                primitiveIndex += subMesh.PrimitiveCount;
            }

            m_Primitives = new(primitiveIndex, allocator);

            var primitives = m_Primitives.AsSpan();
            for (var i = 0; i < subMeshes.Length; i++)
            {
                ref var subMesh = ref subMeshes[i];
                var indices = mesh.GetIndices(i);
                var indexIndex = 0;
                for (var j = 0; j < subMesh.PrimitiveCount; j++)
                {
                    ref var primitive = ref primitives[subMesh.PrimitiveIndex + j];
                    if (subMesh.TopologySize > 0)
                    {
                        primitive.Index0 = indices[indexIndex++];
                    }
                    if (subMesh.TopologySize > 1)
                    {
                        primitive.Index1 = indices[indexIndex++];
                    }
                    if (subMesh.TopologySize > 2)
                    {
                        primitive.Index2 = indices[indexIndex++];
                    }
                    if (subMesh.TopologySize > 3)
                    {
                        primitive.Index3 = indices[indexIndex++];
                    }
                }
            }
        }

        private void ReadBlendShapes(Mesh mesh, Allocator allocator)
        {
            m_BlendShapes = new(mesh.blendShapeCount, allocator);

            var blendShapes = m_BlendShapes.AsSpan();
            var blendShapeFrameIndex = 0;
            for (var i = 0; i < blendShapes.Length; i++)
            {
                ref var blendShape = ref blendShapes[i];
                blendShape.Name = mesh.GetBlendShapeName(i);
                blendShape.FrameIndex = blendShapeFrameIndex;
                blendShape.FrameCount = mesh.GetBlendShapeFrameCount(i);
                blendShapeFrameIndex += blendShape.FrameCount;
            }

            m_BlendShapeFrames = new(blendShapeFrameIndex, allocator);

            var blendShapeFrames = m_BlendShapeFrames.AsSpan();
            var blendShapeDeltaIndex = 0;
            for (var i = 0; i < blendShapes.Length; i++)
            {
                ref var blendShape = ref blendShapes[i];
                for (var j = 0; j < blendShape.FrameCount; j++)
                {
                    ref var blendShapeFrame = ref blendShapeFrames[blendShape.FrameIndex + j];
                    blendShapeFrame.Weight = mesh.GetBlendShapeFrameWeight(i, j);
                    blendShapeFrame.DeltaIndex = blendShapeDeltaIndex;
                    blendShapeFrame.DeltaCount = m_Vertices.Length;
                    blendShapeDeltaIndex += blendShapeFrame.DeltaCount;
                }
            }

            m_BlendShapeDeltas = new(blendShapeDeltaIndex, allocator);

            var blendShapeDeltas = m_BlendShapeDeltas.AsSpan();
            var deltaPositionBuffer = new Vector3[m_Vertices.Length];
            var deltaNormalBuffer = new Vector3[m_Vertices.Length];
            var deltaTangentBuffer = new Vector3[m_Vertices.Length];
            for (var i = 0; i < blendShapes.Length; i++)
            {
                ref var blendShape = ref blendShapes[i];
                for (var j = 0; j < blendShape.FrameCount; j++)
                {
                    ref var blendShapeFrame = ref blendShapeFrames[blendShape.FrameIndex + j];
                    mesh.GetBlendShapeFrameVertices(i, j, deltaPositionBuffer, deltaNormalBuffer, deltaTangentBuffer);
                    for (var k = 0; k < blendShapeFrame.DeltaCount; k++)
                    {
                        ref var blendShapeDelta = ref blendShapeDeltas[blendShapeFrame.DeltaIndex + k];
                        blendShapeDelta.Position = deltaPositionBuffer[k];
                        blendShapeDelta.Normal = deltaNormalBuffer[k];
                        blendShapeDelta.Tangent = deltaTangentBuffer[k];
                    }
                }
            }
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            WriteVertices(mesh);
            WriteSubMeshes(mesh);
            WriteBlendShapes(mesh);
        }

        private void WriteVertices(Mesh mesh)
        {
            var vertices = m_Vertices.AsSpan();
            WriteValue(mesh, vertices, VertexAttribute.Position, (ref Vertex x) => ref x.Position, x => mesh.vertices = x);
            WriteValue(mesh, vertices, VertexAttribute.Normal, (ref Vertex x) => ref x.Normal, x => mesh.normals = x);
            WriteValue(mesh, vertices, VertexAttribute.Tangent, (ref Vertex x) => ref x.Tangent, x => mesh.tangents = x);
            WriteValue(mesh, vertices, VertexAttribute.Color, (ref Vertex x) => ref x.Color, x => mesh.colors32 = x);

            static void WriteValue<T>(Mesh mesh, Span<Vertex> vertices, VertexAttribute attribute, RefFunc<Vertex, T> valueRefGetter, Action<T[]> valuesSetter)
            {
                if (mesh.HasVertexAttribute(attribute))
                {
                    var values = new T[vertices.Length];
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        values[i] = valueRefGetter(ref vertices[i]);
                    }
                    valuesSetter(values);
                }
            }

            var uvBuffer2 = new Vector2[vertices.Length];
            var uvBuffer3 = new Vector3[vertices.Length];
            var uvBuffer4 = new Vector4[vertices.Length];
            WriteUv(mesh, vertices, 0, (ref Vertex x) => ref x.Uv0, uvBuffer2, uvBuffer3, uvBuffer4);
            WriteUv(mesh, vertices, 1, (ref Vertex x) => ref x.Uv1, uvBuffer2, uvBuffer3, uvBuffer4);
            WriteUv(mesh, vertices, 2, (ref Vertex x) => ref x.Uv2, uvBuffer2, uvBuffer3, uvBuffer4);
            WriteUv(mesh, vertices, 3, (ref Vertex x) => ref x.Uv3, uvBuffer2, uvBuffer3, uvBuffer4);
            WriteUv(mesh, vertices, 4, (ref Vertex x) => ref x.Uv4, uvBuffer2, uvBuffer3, uvBuffer4);
            WriteUv(mesh, vertices, 5, (ref Vertex x) => ref x.Uv5, uvBuffer2, uvBuffer3, uvBuffer4);
            WriteUv(mesh, vertices, 6, (ref Vertex x) => ref x.Uv6, uvBuffer2, uvBuffer3, uvBuffer4);
            WriteUv(mesh, vertices, 7, (ref Vertex x) => ref x.Uv7, uvBuffer2, uvBuffer3, uvBuffer4);

            static void WriteUv(Mesh mesh, Span<Vertex> vertices, int channel, RefFunc<Vertex, Vector4> uvRefGetter, Vector2[] uvBuffer2, Vector3[] uvBuffer3, Vector4[] uvBuffer4)
            {
                if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0 + channel))
                {
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        uvBuffer2[i] = uvBuffer3[i] = uvBuffer4[i] = uvRefGetter(ref vertices[i]);
                    }
                    switch (mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0 + channel))
                    {
                        case 2: mesh.SetUVs(channel, uvBuffer2); break;
                        case 3: mesh.SetUVs(channel, uvBuffer3); break;
                        case 4: mesh.SetUVs(channel, uvBuffer4); break;
                    }
                }
            }

            if (mesh.HasVertexAttribute(VertexAttribute.BlendIndices))
            {
                var boneWeightCounts = new NativeArray<byte>(vertices.Length, Allocator.Temp);
                for (var i = 0; i < vertices.Length; i++)
                {
                    ref var vertex = ref vertices[i];
                    boneWeightCounts[i] = (byte)vertex.BoneWeightCount;
                }
                mesh.SetBoneWeights(boneWeightCounts, m_BoneWeights);
                boneWeightCounts.Dispose();
            }
        }

        private void WriteSubMeshes(Mesh mesh)
        {
            mesh.indexFormat = m_Vertices.Length < ushort.MaxValue ? IndexFormat.UInt16 : IndexFormat.UInt32;
            mesh.subMeshCount = m_SubMeshes.Length;

            var subMeshes = m_SubMeshes.AsSpan();
            var primitives = m_Primitives.AsSpan();
            for (var i = 0; i < subMeshes.Length; i++)
            {
                ref var subMesh = ref subMeshes[i];
                var indices = new int[subMesh.TopologySize * subMesh.PrimitiveCount];
                var indexIndex = 0;
                for (var j = 0; j < subMesh.PrimitiveCount; j++)
                {
                    ref var primitive = ref primitives[subMesh.PrimitiveIndex + j];
                    if (subMesh.TopologySize > 0)
                    {
                        indices[indexIndex++] = primitive.Index0;
                    }
                    if (subMesh.TopologySize > 1)
                    {
                        indices[indexIndex++] = primitive.Index1;
                    }
                    if (subMesh.TopologySize > 2)
                    {
                        indices[indexIndex++] = primitive.Index2;
                    }
                    if (subMesh.TopologySize > 3)
                    {
                        indices[indexIndex++] = primitive.Index3;
                    }
                }
                mesh.SetIndices(indices, subMesh.Topology, i, false);
            }
        }

        private void WriteBlendShapes(Mesh mesh)
        {
            var blendShapes = m_BlendShapes.AsSpan();
            var blendShapeFrames = m_BlendShapeFrames.AsSpan();
            var blendShapeDeltas = m_BlendShapeDeltas.AsSpan();
            var deltaPositionBuffer = new Vector3[m_Vertices.Length];
            var deltaNormalBuffer = new Vector3[m_Vertices.Length];
            var deltaTangentBuffer = new Vector3[m_Vertices.Length];
            for (var i = 0; i < blendShapes.Length; i++)
            {
                ref var blendShape = ref blendShapes[i];
                for (var j = 0; j < blendShape.FrameCount; j++)
                {
                    ref var blendShapeFrame = ref blendShapeFrames[blendShape.FrameIndex + j];
                    for (var k = 0; k < blendShapeFrame.DeltaCount; k++)
                    {
                        ref var blendShapeDelta = ref blendShapeDeltas[blendShapeFrame.DeltaIndex + k];
                        deltaPositionBuffer[k] = blendShapeDelta.Position;
                        deltaNormalBuffer[k] = blendShapeDelta.Normal;
                        deltaTangentBuffer[k] = blendShapeDelta.Tangent;
                    }
                    mesh.AddBlendShapeFrame(blendShape.Name.Value, blendShapeFrame.Weight, deltaPositionBuffer, deltaNormalBuffer, deltaTangentBuffer);
                }
            }
        }

        public void Dispose()
        {
            m_Vertices.Dispose();
            m_BoneWeights.Dispose();
            m_SubMeshes.Dispose();
            m_Primitives.Dispose();
            m_BlendShapes.Dispose();
            m_BlendShapeFrames.Dispose();
            m_BlendShapeDeltas.Dispose();
        }
    }
}
