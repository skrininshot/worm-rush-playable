using System;

namespace WormRush.Core
{
    /// <summary>
    /// Bot steering. Stage 1 holds only the scripted victim; every other bot keeps its heading.
    /// Food seeking, retargeting and edge avoidance arrive in stage 3.
    /// </summary>
    public static class BotBrain
    {
        public static void Think(WormSim sim, Worm bot, float dt)
        {
            SimConfig config = sim.Config;
            Worm player = sim.Player;

            bool scripted = bot.IsVictim
                            && sim.Started
                            && player.Alive
                            && sim.Time - sim.StartTime > config.VictimDelaySeconds;
            bot.VictimScriptActive = scripted;

            if (scripted)
            {
                // Aim at a point on the player's body so the climax is guaranteed.
                int index = (int)(player.SegmentCount * config.VictimTargetBodyFraction);
                index = Math.Min(player.SegmentCount - 1, index);
                bot.Target = player.Segments[index];
                bot.HasTarget = true;
                bot.Speed = config.VictimSpeed;
            }

            if (bot.HasTarget)
            {
                Vec2 head = bot.Head;
                bot.WantAngle = MathF.Atan2(bot.Target.Y - head.Y, bot.Target.X - head.X);
            }
        }
    }
}
