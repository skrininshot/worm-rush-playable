using System;

namespace WormRush.Core
{
    /// <summary>
    /// Bot steering, ported from the reference: pick the nearest of a few random pellets, retarget on a
    /// timer, head for the centre when close to the edge. The scripted victim overrides all of it once
    /// the delay after the first tap has passed.
    /// </summary>
    public static class BotBrain
    {
        public static void Think(WormSim sim, Worm bot, float dt)
        {
            SimConfig config = sim.Config;
            Worm player = sim.Player;
            Vec2 head = bot.Head;

            bool wasScripted = bot.VictimScriptActive;
            bool scripted = bot.IsVictim
                            && sim.Started
                            && player.Alive
                            && sim.Time - sim.StartTime > config.VictimDelaySeconds;
            bot.VictimScriptActive = scripted;

            if (scripted)
            {
                // Aim at a point on the player's body so the climax is guaranteed. The script wins over
                // edge avoidance: the player's body may lie in the outer ring.
                int index = (int)(player.SegmentCount * config.VictimTargetBodyFraction);
                index = Math.Min(player.SegmentCount - 1, index);
                bot.Target = player.Segments[index];
                bot.HasTarget = true;

                float chase = sim.Time - sim.StartTime - config.VictimDelaySeconds;
                bot.Speed = Math.Min(config.VictimMaxSpeed, config.VictimSpeed + config.VictimSpeedRamp * chase);
            }
            else
            {
                // Player died mid-script: the victim goes back to being an ordinary bot.
                if (wasScripted) bot.Speed = config.BotSpeed;

                bot.ThinkTimer -= dt;
                if (bot.ThinkTimer <= 0f)
                {
                    bot.ThinkTimer = sim.Random.Range(config.BotRetargetMin, config.BotRetargetMax);
                    // The victim loiters a fixed distance from the player so the climax does not depend on
                    // where it wandered, yet it is never so close that the kill lands the moment it is scripted.
                    if (bot.IsVictim && config.VictimLoiterDistance > 0f)
                        PickFood(sim, bot, player.Head, config.VictimLoiterDistance);
                    else
                        PickFood(sim, bot, head, 0f);
                }

                if (bot.IsVictim && config.VictimLoiterDistance > 0f)
                    KeepOut(bot, head, player.Head, config.VictimLoiterDistance * config.VictimKeepOutFraction, config.VictimLoiterDistance);

                float edgeLimit = config.ArenaRadius - config.BotEdgeMargin;
                if (head.X * head.X + head.Y * head.Y > edgeLimit * edgeLimit)
                {
                    bot.Target = new Vec2(0f, 0f);
                    bot.HasTarget = true;
                }
            }

            if (bot.HasTarget)
                bot.WantAngle = MathF.Atan2(bot.Target.Y - head.Y, bot.Target.X - head.X);
        }

        /// <summary>Too close to the player: aim at a point <paramref name="awayDistance"/> out along the line from the player.</summary>
        static void KeepOut(Worm bot, Vec2 head, Vec2 playerHead, float minDistance, float awayDistance)
        {
            float dx = head.X - playerHead.X;
            float dy = head.Y - playerHead.Y;
            float d2 = dx * dx + dy * dy;
            if (d2 >= minDistance * minDistance) return;

            float d = MathF.Sqrt(d2);
            if (d < 1e-3f)
            {
                dx = 1f;
                dy = 0f;
                d = 1f;
            }
            bot.Target = new Vec2(playerHead.X + dx / d * awayDistance, playerHead.Y + dy / d * awayDistance);
            bot.HasTarget = true;
        }

        /// <summary>
        /// Of a handful of random pellets, the one whose distance from <paramref name="anchor"/> is closest to
        /// <paramref name="wantedDistance"/> (0 = the nearest). Keeps the old target if there is no food.
        /// </summary>
        static void PickFood(WormSim sim, Worm bot, Vec2 anchor, float wantedDistance)
        {
            int count = sim.FoodCount;
            if (count == 0) return;

            float bestScore = float.MaxValue;
            for (int k = 0; k < sim.Config.BotFoodSamples; k++)
            {
                Food food = sim.FoodItems[sim.Random.Next(count)];
                float dx = food.X - anchor.X;
                float dy = food.Y - anchor.Y;
                float score = wantedDistance > 0f
                    ? Math.Abs(MathF.Sqrt(dx * dx + dy * dy) - wantedDistance)
                    : dx * dx + dy * dy;
                if (score >= bestScore) continue;

                bestScore = score;
                bot.Target = new Vec2(food.X, food.Y);
                bot.HasTarget = true;
            }
        }
    }
}
