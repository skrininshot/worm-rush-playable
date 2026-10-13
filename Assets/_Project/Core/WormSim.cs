using System;
using System.Collections.Generic;

namespace WormRush.Core
{
    /// <summary>
    /// The whole simulation: worms, food, collisions, deaths. No Unity, no physics, no allocation
    /// in the steady state. Deterministic for a given (config, seed, sequence of inputs and dts).
    /// The View drives it: StartPlay on the first tap, SetPlayerHeading from the pointer, Step each frame.
    /// </summary>
    public sealed class WormSim
    {
        // Worm index and segment index packed into one spatial-hash payload.
        const int SegmentBits = 20;
        const int SegmentMask = (1 << SegmentBits) - 1;

        public readonly SimConfig Config;

        /// <summary>Index 0 is the player, 1..N the bots. Dead worms stay in the list with Alive == false.</summary>
        public readonly List<Worm> Worms = new List<Worm>();

        /// <summary>Everything that happened in the last Step; cleared at the start of the next one.</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>();

        public float Time { get; private set; }
        public bool Started { get; private set; }
        public float StartTime { get; private set; }
        public int PlayerKills { get; private set; }

        /// <summary>
        /// While true the player's worm is steered by BotBrain like any other bot and SetPlayerHeading is ignored.
        /// The Flow turns it on under the end card, where the human no longer steers.
        /// </summary>
        public bool Autopilot;

        /// <summary>Valid entries: [0, FoodCount). The array object is replaced when it grows, so re-read it each frame.</summary>
        public Food[] FoodItems { get; private set; }
        public int FoodCount { get; private set; }

        readonly SimRandom _rng;
        readonly SpatialHash _foodHash;
        readonly SpatialHash _segmentHash;
        readonly List<int> _query = new List<int>(128);

        public WormSim(SimConfig config, int seed)
        {
            Config = config;
            _rng = new SimRandom(seed);
            float halfExtent = config.ArenaRadius + config.HashCellSize;
            _foodHash = new SpatialHash(config.HashCellSize, halfExtent, config.FoodTarget * 2);
            _segmentHash = new SpatialHash(config.HashCellSize, halfExtent, 1024);
            FoodItems = new Food[Math.Max(config.FoodTarget * 2, 64)];

            SpawnWorms();
            RefillFood();
        }

        /// <summary>The sim's seeded generator, shared with BotBrain so a seed still fixes the whole run.</summary>
        internal SimRandom Random
        {
            get { return _rng; }
        }

        public Worm Player
        {
            get { return Worms[0]; }
        }

        /// <summary>First tap. Starts the clock the victim script runs on and ends the player's invulnerability.</summary>
        public void StartPlay()
        {
            if (Started) return;
            Started = true;
            StartTime = Time;
        }

        /// <summary>Direction the player's worm should turn toward, in radians. Ignored until StartPlay.</summary>
        public void SetPlayerHeading(float angle)
        {
            if (Started && Player.Alive && !Autopilot) Player.WantAngle = angle;
        }

        public void Step(float dt)
        {
            Events.Clear();
            if (dt > Config.MaxStepSeconds) dt = Config.MaxStepSeconds;
            if (dt <= 0f) return;
            Time += dt;

            if (!Started && Player.Alive)
                Player.WantAngle += Config.IdleTurnRate * dt;

            if (Autopilot && Player.Alive) BotBrain.Think(this, Player, dt);
            for (int i = 1; i < Worms.Count; i++)
            {
                if (Worms[i].Alive) BotBrain.Think(this, Worms[i], dt);
            }

            for (int i = 0; i < Worms.Count; i++)
            {
                if (Worms[i].Alive) Worms[i].Advance(dt);
            }

            EatFood(dt);
            ResolveCollisions();
        }

        public void SpawnFood(float x, float y, int value, int colorId)
        {
            if (FoodCount == FoodItems.Length)
            {
                Food[] grown = FoodItems;
                Array.Resize(ref grown, FoodCount * 2);
                FoodItems = grown;
            }

            FoodItems[FoodCount++] = new Food
            {
                X = x,
                Y = y,
                Value = value,
                Radius = Config.FoodRadius(value),
                ColorId = colorId,
                Phase = _rng.Range(0f, MathF.PI * 2f),
            };
        }

        void SpawnWorms()
        {
            Worm player = new Worm(0, true, false, Config.PlayerColorId, Config.PlayerStartLength, Config);
            player.Speed = Config.PlayerSpeed;
            player.Place(0f, 0f, Config.PlayerStartAngle);
            Worms.Add(player);

            int botCount = Config.BotCount;
            for (int i = 0; i < botCount; i++)
            {
                bool victim = i == Config.VictimBotIndex;
                int colorId = Config.BotColorIds.Length > 0 ? Config.BotColorIds[i % Config.BotColorIds.Length] : 0;
                Worm bot = new Worm(i + 1, false, victim, colorId, Config.BotStartLengths[i], Config);
                bot.Speed = Config.BotSpeed;

                float a = (float)i / botCount * MathF.PI * 2f + Config.BotSpawnAngleOffset;
                float dist = victim
                    ? Config.VictimSpawnDistance
                    : _rng.Range(Config.BotSpawnDistanceMin, Config.BotSpawnDistanceMax);
                bot.Place(MathF.Cos(a) * dist, MathF.Sin(a) * dist, _rng.Range(0f, MathF.PI * 2f));
                Worms.Add(bot);
            }
        }

        void RefillFood()
        {
            while (FoodCount < Config.FoodTarget)
            {
                float a = _rng.Range(0f, MathF.PI * 2f);
                float d = MathF.Sqrt(_rng.NextFloat()) * (Config.ArenaRadius - Config.FoodSpawnMargin);
                SpawnFood(MathF.Cos(a) * d, MathF.Sin(a) * d, Config.FoodValueNormal, _rng.Next(Config.ColorCount));
            }
        }

        void EatFood(float dt)
        {
            _foodHash.Clear();
            for (int i = 0; i < FoodCount; i++)
                _foodHash.Insert(FoodItems[i].X, FoodItems[i].Y, i);

            // The magnet shifts pellets after they were hashed; pad the query by one step of pull.
            float slack = Config.MagnetSpeed * dt;
            bool anyEaten = false;

            for (int w = 0; w < Worms.Count; w++)
            {
                Worm worm = Worms[w];
                if (!worm.Alive) continue;

                Vec2 head = worm.Head;
                float radius = worm.Radius;
                float magnetReach = worm.IsPlayer ? radius + Config.MagnetRange : 0f;
                float reach = Math.Max(radius, magnetReach) + Config.MaxFoodRadius + slack;
                _foodHash.Query(head.X, head.Y, reach, _query);

                for (int q = 0; q < _query.Count; q++)
                {
                    ref Food food = ref FoodItems[_query[q]];
                    if (food.Eaten) continue;

                    float dx = head.X - food.X;
                    float dy = head.Y - food.Y;
                    float d2 = dx * dx + dy * dy;

                    if (worm.IsPlayer && d2 < magnetReach * magnetReach)
                    {
                        float d = MathF.Sqrt(d2);
                        if (d == 0f) d = 1f;
                        food.X += dx / d * Config.MagnetSpeed * dt;
                        food.Y += dy / d * Config.MagnetSpeed * dt;
                    }

                    float eatReach = radius + food.Radius;
                    if (d2 < eatReach * eatReach)
                    {
                        food.Eaten = true;
                        anyEaten = true;
                        worm.Grow(food.Value);
                        Events.Add(new SimEvent(SimEventType.Ate, worm.Id, -1, food.Value, food.X, food.Y));
                    }
                }
            }

            if (anyEaten)
            {
                int write = 0;
                for (int read = 0; read < FoodCount; read++)
                {
                    if (FoodItems[read].Eaten) continue;
                    if (write != read) FoodItems[write] = FoodItems[read];
                    write++;
                }
                FoodCount = write;
            }

            RefillFood();
        }

        void ResolveCollisions()
        {
            _segmentHash.Clear();
            int skip = Config.BodyHitSkipSegments;
            for (int w = 0; w < Worms.Count; w++)
            {
                Worm worm = Worms[w];
                if (!worm.Alive) continue;
                for (int s = skip; s < worm.SegmentCount; s++)
                    _segmentHash.Insert(worm.Segments[s].X, worm.Segments[s].Y, (w << SegmentBits) | s);
            }

            float arenaRadius = Config.ArenaRadius;
            for (int w = 0; w < Worms.Count; w++)
            {
                Worm worm = Worms[w];
                if (!worm.Alive) continue;

                Vec2 head = worm.Head;
                float radius = worm.Radius;
                float distFromCenter = MathF.Sqrt(head.X * head.X + head.Y * head.Y);
                if (distFromCenter > arenaRadius - radius)
                {
                    bool playerSurvives = worm.IsPlayer && !(Config.PlayerEdgeDeath && Started);
                    if (playerSurvives || IsShieldedVictim(worm))
                    {
                        // The guaranteed victim, and the player before the first tap (or with PlayerEdgeDeath off),
                        // are pushed back from the edge, never killed by it.
                        float k = (arenaRadius - radius) / distFromCenter;
                        head = new Vec2(head.X * k, head.Y * k);
                        worm.Segments[0] = head;
                    }
                    else
                    {
                        Kill(worm, null);
                        continue;
                    }
                }

                // Before the first tap the player cannot die.
                if (worm.IsPlayer && !Started) continue;

                float hitRadius = radius * Config.HeadHitRadiusFactor;
                _segmentHash.Query(head.X, head.Y, hitRadius + Config.MaxWormRadius, _query);

                int killerIndex = -1;
                for (int q = 0; q < _query.Count; q++)
                {
                    int otherIndex = _query[q] >> SegmentBits;
                    if (otherIndex == w || (killerIndex >= 0 && otherIndex >= killerIndex)) continue;

                    Worm other = Worms[otherIndex];
                    if (!other.Alive || !CanHit(worm, other)) continue;

                    Vec2 seg = other.Segments[_query[q] & SegmentMask];
                    float reach = hitRadius + other.Radius;
                    if (Vec2.SqrDistance(head, seg) < reach * reach) killerIndex = otherIndex;
                }

                if (killerIndex >= 0) Kill(worm, Worms[killerIndex]);
            }
        }

        bool IsShieldedVictim(Worm worm)
        {
            return Config.VictimGuarantee && worm.IsVictim;
        }

        /// <summary>
        /// Whether <paramref name="attacker"/>'s head can die on <paramref name="body"/>. With VictimGuarantee the
        /// scripted victim is the director's tool: its body never kills the player, and its own head only dies
        /// on the player's body, and only once the script is active. Everyone else is plain head-versus-body.
        /// </summary>
        bool CanHit(Worm attacker, Worm body)
        {
            if (!Config.VictimGuarantee) return true;
            if (body.IsVictim && attacker.IsPlayer) return false;
            if (attacker.IsVictim) return body.IsPlayer && attacker.VictimScriptActive;
            return true;
        }

        void Kill(Worm victim, Worm killer)
        {
            victim.Alive = false;
            Vec2 head = victim.Head;

            float jitter = Config.RemainsJitter;
            for (int i = 0; i < victim.SegmentCount; i += Config.RemainsStride)
            {
                Vec2 p = victim.Segments[i];
                SpawnFood(
                    p.X + _rng.Range(-jitter, jitter),
                    p.Y + _rng.Range(-jitter, jitter),
                    Config.FoodValueRemains,
                    victim.ColorId);
            }

            int killerId = killer != null ? killer.Id : -1;
            Events.Add(new SimEvent(SimEventType.Killed, victim.Id, killerId, 0, head.X, head.Y));
            if (killer != null && killer.IsPlayer) PlayerKills++;
            if (victim.IsPlayer)
                Events.Add(new SimEvent(SimEventType.PlayerDied, victim.Id, killerId, 0, head.X, head.Y));
        }
    }
}
