using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace fishingWithStudy.UI
{
    public static class QuizResultMessage
    {
        public static void DrawResultOverlay(SpriteBatch b, Rectangle bounds,
            string message, Color color, string? subMessage = null)
        {
            b.Draw(Game1.fadeToBlackRect, bounds, Color.Black * 0.3f);

            Vector2 msgSize = Game1.dialogueFont.MeasureString(message);
            float scale = Math.Min(1f, (bounds.Width - 40) / Math.Max(msgSize.X, 1));
            b.DrawString(Game1.dialogueFont, message,
                new Vector2(bounds.Center.X - msgSize.X * scale / 2,
                    bounds.Center.Y - msgSize.Y * scale / 2 - (subMessage != null ? 30 : 0)),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);

            if (subMessage != null)
            {
                Vector2 subSize = Game1.smallFont.MeasureString(subMessage);
                float subScale = Math.Min(1f, (bounds.Width - 40) / Math.Max(subSize.X, 1));
                b.DrawString(Game1.smallFont, subMessage,
                    new Vector2(bounds.Center.X - subSize.X * subScale / 2,
                        bounds.Center.Y - subSize.Y * subScale / 2 + 40),
                    color * 0.9f, 0f, Vector2.Zero, subScale, SpriteEffects.None, 1f);
            }
        }

        public static void DrawTreasureOverlay(SpriteBatch b, Rectangle bounds,
            string title, List<string> items)
        {
            b.Draw(Game1.fadeToBlackRect, bounds, Color.Black * 0.4f);

            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            b.DrawString(Game1.dialogueFont, title,
                new Vector2(bounds.Center.X - titleSize.X / 2, bounds.Y + 40),
                Color.Gold);

            float yPos = bounds.Y + 40 + titleSize.Y + 20;
            foreach (var item in items)
            {
                Vector2 itemSize = Game1.smallFont.MeasureString(item);
                b.DrawString(Game1.smallFont, item,
                    new Vector2(bounds.Center.X - itemSize.X / 2, yPos),
                    Color.White);
                yPos += itemSize.Y + 10;
            }
        }

        public static void DrawTransitionOverlay(SpriteBatch b, Rectangle bounds,
            string message, float countdown)
        {
            b.Draw(Game1.fadeToBlackRect, bounds, Color.Black * 0.3f);

            Vector2 msgSize = Game1.smallFont.MeasureString(message);
            b.DrawString(Game1.smallFont, message,
                new Vector2(bounds.Center.X - msgSize.X / 2, bounds.Center.Y - 30),
                Color.DarkGreen);

            string timerText = $"Next in {(int)System.Math.Ceiling(countdown)}s";
            Vector2 timerSize = Game1.smallFont.MeasureString(timerText);
            b.DrawString(Game1.smallFont, timerText,
                new Vector2(bounds.Center.X - timerSize.X / 2, bounds.Center.Y + 10),
                Color.DarkGreen);
        }
    }
}