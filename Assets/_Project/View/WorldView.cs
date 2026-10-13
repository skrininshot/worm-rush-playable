using UnityEngine;
using WormRush.Config;
using WormRush.Core;

namespace WormRush.View
{
    /// <summary>
    /// Draws a WormSim: arena, food, worms, and the burst particles on top. Four instanced layers, drawn back to
    /// front; the whole picture is a handful of draw calls regardless of how many segments exist.
    /// </summary>
    public sealed class WorldView
    {
        readonly CircleBatch _arenaBatch;
        readonly CircleBatch _foodBatch;
        readonly CircleBatch _wormBatch;
        readonly CircleBatch _fxBatch;
        readonly ArenaRenderer _arena;
        readonly FoodRenderer _food;
        readonly WormRenderer _worms;
        readonly ParticleRenderer _particles;
        readonly Mesh _mesh;
        readonly Material _material;

        /// <summary>Null when the shader cannot be found; the caller disables rendering then.</summary>
        public static WorldView Create(PlayableConfig config)
        {
            Shader shader = config.CircleShader != null ? config.CircleShader : Shader.Find("WormRush/Circle");
            if (shader == null)
            {
                Debug.LogError("WormRush/Circle shader not found. Assign it on PlayableConfig (Tools > Worm Rush > Ensure PlayableConfig).");
                return null;
            }

            return new WorldView(config, shader);
        }

        WorldView(PlayableConfig config, Shader shader)
        {
            _mesh = CircleBatch.CreateQuad();
            // A build keeps the shader's INSTANCING_ON variant only if some material asset uses it; this run-time
            // material does not count, Resources/CircleInstanced.mat does (made by Editor/WebBuild.cs).
            _material = new Material(shader) { name = "WormRush.Circle", enableInstancing = true };

            ShaderPalette palette = new ShaderPalette(config);
            float extent = config.Sim.ArenaRadius * 2f;
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(extent, extent, config.LayerDepthStep * 4f));

            // Larger Z is farther from the camera, so it is drawn first.
            float step = config.LayerDepthStep;
            _arenaBatch = new CircleBatch(_mesh, _material, step * 2f, bounds);
            _foodBatch = new CircleBatch(_mesh, _material, step, bounds);
            _wormBatch = new CircleBatch(_mesh, _material, 0f, bounds);
            _fxBatch = new CircleBatch(_mesh, _material, -step, bounds);

            _arena = new ArenaRenderer(config, palette, _arenaBatch);
            _food = new FoodRenderer(config, palette, _foodBatch);
            _worms = new WormRenderer(config, palette, _wormBatch);
            _particles = new ParticleRenderer(config, palette, _fxBatch);
        }

        /// <param name="juice">Source of the burst particles; null draws the world alone.</param>
        public void Draw(WormSim sim, in ViewBounds view, JuiceState juice = null)
        {
            _arena.Draw(view);
            _food.Draw(sim, view);
            _worms.Draw(sim, view);
            if (juice != null) _particles.Draw(juice, view);
            _arenaBatch.Flush();
            _foodBatch.Flush();
            _wormBatch.Flush();
            _fxBatch.Flush();
        }

        public void Dispose()
        {
            Object.Destroy(_material);
            Object.Destroy(_mesh);
        }
    }
}
