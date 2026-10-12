using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WormRush.View
{
    /// <summary>
    /// A layer of flat circles drawn with Graphics.RenderMeshInstanced: no GameObject per circle,
    /// one draw call per 1023 circles. Circles are drawn in the order they were added.
    /// Usage per frame: Add(...) many times, then Flush().
    /// </summary>
    public sealed class CircleBatch
    {
        const int ChunkSize = 1023;   // GPU instancing limit for one draw call

        static readonly int ColorProperty = Shader.PropertyToID("_Color");

        sealed class Chunk
        {
            public readonly Matrix4x4[] Matrices = new Matrix4x4[ChunkSize];
            public readonly Vector4[] Colors = new Vector4[ChunkSize];
            public int Count;
        }

        readonly List<Chunk> _chunks = new List<Chunk>();
        readonly Mesh _mesh;
        readonly Material _material;
        readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        readonly RenderParams _renderParams;
        readonly float _depth;
        int _current;

        /// <param name="depth">World Z of this layer; larger Z is farther from the camera and drawn first.</param>
        /// <param name="bounds">World bounds of everything the batch can draw (instancing culls against it).</param>
        public CircleBatch(Mesh mesh, Material material, float depth, Bounds bounds)
        {
            _mesh = mesh;
            _material = material;
            _depth = depth;
            _renderParams = new RenderParams(material)
            {
                worldBounds = bounds,
                matProps = _properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            _chunks.Add(new Chunk());
        }

        public int Count { get; private set; }

        public void Add(float x, float y, float radius, Vector4 color)
        {
            Chunk chunk = _chunks[_current];
            if (chunk.Count == ChunkSize)
            {
                _current++;
                if (_current == _chunks.Count) _chunks.Add(new Chunk());
                chunk = _chunks[_current];
            }

            // Only the fields below ever change, so the rest of the matrix stays zero from allocation.
            float diameter = radius * 2f;
            ref Matrix4x4 m = ref chunk.Matrices[chunk.Count];
            m.m00 = diameter;
            m.m11 = diameter;
            m.m22 = 1f;
            m.m33 = 1f;
            m.m03 = x;
            m.m13 = y;
            m.m23 = _depth;
            chunk.Colors[chunk.Count] = color;
            chunk.Count++;
            Count++;
        }

        /// <summary>Submits everything added since the last flush and empties the batch.</summary>
        public void Flush()
        {
            for (int i = 0; i <= _current; i++)
            {
                Chunk chunk = _chunks[i];
                if (chunk.Count > 0)
                {
                    _properties.SetVectorArray(ColorProperty, chunk.Colors);
                    Graphics.RenderMeshInstanced(_renderParams, _mesh, 0, chunk.Matrices, chunk.Count);
                }
                chunk.Count = 0;
            }
            _current = 0;
            Count = 0;
        }

        /// <summary>Unit quad centred on the origin with 0..1 UVs; the shader cuts the circle out of it.</summary>
        public static Mesh CreateQuad()
        {
            Mesh mesh = new Mesh { name = "WormRush.Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
