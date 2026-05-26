using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using fishingWithStudy.Config;
using fishingWithStudy.Data;
using fishingWithStudy.Logic;

namespace fishingWithStudy.UI
{
    public class QuizMenu : IClickableMenu
    {
        // -----------------------------------------------------------------------
        // State machine
        // -----------------------------------------------------------------------
        private enum QuizState
        {
            Playing,           // Normal: showing question, options, timer
            CorrectFeedback,   // Showing "Correct!" feedback
            WrongFeedback,     // Showing "Wrong!" + correct answer
            TimeoutFeedback,   // Showing "Time's Up!" + correct answer
            TreasureShow,      // Showing treasure overlay
            Transition,        // "Next in Xs" between questions
        }

        // -----------------------------------------------------------------------
        // Dependencies
        // -----------------------------------------------------------------------
        private readonly ModConfig config;
        private readonly QuestionManager questionManager;
        private readonly StatsManager statsManager;
        private readonly string scopeKey;
        private readonly IMonitor monitor;

        // -----------------------------------------------------------------------
        // Quiz configuration (set once in constructor)
        // -----------------------------------------------------------------------
        private readonly bool isStudyMode;
        private readonly int requiredCorrect;
        private readonly int treasureThreshold;
        private int currentCorrect = 0;
        private bool fishRewarded = false;
        private bool treasureRewarded = false;

        // -----------------------------------------------------------------------
        // Current question state
        // -----------------------------------------------------------------------
        private Question? currentQuestion;
        private readonly HashSet<int> selectedOptions = new();

        // -----------------------------------------------------------------------
        // State machine runtime
        // -----------------------------------------------------------------------
        private QuizState state = QuizState.Playing;
        private float questionTimer;        // countdown for answering (seconds)
        private float stateTimer;           // generic timer for state transitions
        private bool pendingFinalize;       // true -> call FishRewarder.ApplyCorrect on state exit
        private bool pendingTransition;     // true -> load next question on state exit
        private bool markAnswerDone;        // guard to prevent duplicate answer recording

        // -----------------------------------------------------------------------
        // Feedback / transition data
        // -----------------------------------------------------------------------
        private List<string> correctAnswerTexts = new();

        // -----------------------------------------------------------------------
        // Time freeze
        // -----------------------------------------------------------------------
        private int frozenTimeInterval;

        // -----------------------------------------------------------------------
        // Layout rectangles (recomputed in ComputeLayout)
        // -----------------------------------------------------------------------
        private Rectangle contentRect;
        private Rectangle titleRect;
        private Rectangle progressRect;
        private Rectangle questionRect;
        private readonly List<Rectangle> optionRects = new();
        private Rectangle submitRect;
        private Rectangle categoryRect;

        // -----------------------------------------------------------------------
        // Wrapped text cache (rebuilt in ComputeLayout)
        // -----------------------------------------------------------------------
        private List<string> wrappedQuestionLines = new();
        private readonly List<List<string>> wrappedOptionLines = new();
        private float questionRenderScale = 1f;
        private float questionRenderY = 0f;

        // =======================================================================
        // Constructor
        // =======================================================================
        public QuizMenu(ModConfig config, QuestionManager questionManager, StatsManager statsManager,
            string scopeKey, bool isStudyMode, IMonitor monitor)
        {
            this.config = config;
            this.questionManager = questionManager;
            this.statsManager = statsManager;
            this.scopeKey = scopeKey;
            this.isStudyMode = isStudyMode;
            this.monitor = monitor;

            // -- requiredCorrect --
            if (!isStudyMode)
            {
                bool legendary = FishRewarder.IsLegendaryFish();
                requiredCorrect = legendary ? 5 : (FishRewarder.HasTreasure ? 2 : 1);
            }
            else
            {
                requiredCorrect = 1;
            }

            // -- treasureThreshold --
            if (!isStudyMode)
            {
                bool legendary = FishRewarder.IsLegendaryFish();
                bool hasTreasure = FishRewarder.HasTreasure;
                if (legendary && hasTreasure)
                    treasureThreshold = 3;
                else if (hasTreasure)
                    treasureThreshold = 1;
                else
                    treasureThreshold = -1;
            }
            else
            {
                treasureThreshold = -1;
            }

            // -- layout --
            width = (int)(Game1.viewport.Width * 0.6);
            height = (int)(Game1.viewport.Height * 0.55);
            xPositionOnScreen = (Game1.viewport.Width - width) / 2;
            yPositionOnScreen = (Game1.viewport.Height - height) / 2;
            ComputeLayout();

            // -- timer & first question --
            questionTimer = config.TimerSeconds;
            LoadNextQuestion();

            // Freeze time by preventing gameTimeInterval from accumulating.
            // We cannot use Game1.paused = true because SDV 1.6 skips input
            // dispatch to activeClickableMenu while paused.
            frozenTimeInterval = Game1.gameTimeInterval;

            Game1.playSound("openBox");

            monitor.Log($"QuizMenu constructor complete: state={state}, timer={questionTimer}, questions={questionManager.TotalQuestions}, isStudy={isStudyMode}", LogLevel.Info);
        }

        // =======================================================================
        // Question loading
        // =======================================================================
        private void LoadNextQuestion()
        {
            currentQuestion = questionManager.GetNextQuestion();
            if (currentQuestion == null)
            {
                monitor.Log("No more questions available, closing quiz.", LogLevel.Warn);
                ExitQuizMenu();
                return;
            }

            selectedOptions.Clear();
            markAnswerDone = false;
            pendingFinalize = false;
            pendingTransition = false;
            correctAnswerTexts.Clear();
            questionTimer = config.TimerSeconds;
            questionRenderScale = 1f;
            questionRenderY = 0f;

            // Pre-compute correct answer texts for feedback display
            if (currentQuestion.Answer != null)
            {
                foreach (var tag in currentQuestion.Answer)
                {
                    var opt = currentQuestion.Options.FirstOrDefault(o => o.Tag == tag);
                    if (opt != null)
                        correctAnswerTexts.Add(opt.Text);
                }
            }

            ComputeLayout();
        }

        // =======================================================================
        // Font metrics
        // =======================================================================
        private static float LineHeight(SpriteFont font, float scale = 1f) => (font.MeasureString("A").Y + 2f) * scale;

        /// <summary>Wrap text to fit within maxWidth, breaking at word boundaries.</summary>
        private static List<string> WrapText(string text, float maxWidth, SpriteFont font)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            string[] words = text.Split(' ');
            string line = "";

            foreach (string word in words)
            {
                string test = line.Length == 0 ? word : line + " " + word;
                if (font.MeasureString(test).X > maxWidth && line.Length > 0)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = test;
                }
            }

            if (line.Length > 0)
                lines.Add(line);

            return lines;
        }
        private void ComputeLayout()
        {
            int padX = width / 20;
            int padY = height / 15;
            int cx = xPositionOnScreen + padX;
            int cy = yPositionOnScreen + padY;
            int cw = width - 2 * padX;
            int ch = height - 2 * padY;
            contentRect = new Rectangle(cx, cy, cw, ch);

            int y = cy;

            // Title
            int titleH = 44;
            titleRect = new Rectangle(cx, y, cw, titleH);
            y += titleH + 6;

            // Progress / countdown line
            int progH = 28;
            progressRect = new Rectangle(cx, y, cw, progH);
            y += progH + 8;

            // Question area (auto-scaled text)
            int remainingTop = y;
            int remainingH = (cy + ch) - y;

            // Reserve room for options + submit + category
            bool hasOptions = currentQuestion != null && currentQuestion.Options.Count > 0;
            int optCount = hasOptions ? currentQuestion!.Options.Count : 0;
            int submitH = 36;
            int catH = 24;

            // Word-wrap question text at full scale, then compute best scale to fit
            wrappedQuestionLines.Clear();
            float qWrapW = cw - 20;
            if (currentQuestion != null)
                wrappedQuestionLines = WrapText(currentQuestion.QuestionText, qWrapW, Game1.dialogueFont);
            if (wrappedQuestionLines.Count == 0) wrappedQuestionLines.Add(" ");
            float qLineH1 = LineHeight(Game1.dialogueFont);
            int qNeedH = (int)(wrappedQuestionLines.Count * qLineH1) + 10;

            // Word-wrap option texts, compute dynamic heights
            wrappedOptionLines.Clear();
            int[] optLineHeights = new int[optCount];
            float optWrapW = cw - 30 - LineHeight(Game1.smallFont);
            for (int i = 0; i < optCount; i++)
            {
                var wrapped = WrapText(currentQuestion!.Options[i].Text, optWrapW, Game1.smallFont);
                wrappedOptionLines.Add(wrapped);
                optLineHeights[i] = (int)(wrapped.Count * LineHeight(Game1.smallFont)) + 6;
            }
            int optBlockH = optLineHeights.Sum() + (optCount > 0 ? 8 : 0);

            // Compute question height: try to fit at full scale, shrink if needed
            int maxQ = remainingH - optBlockH - submitH - catH - 20;
            if (maxQ < 40) maxQ = 40;

            questionRenderScale = 1f;
            questionRenderY = 0f;
            int questionH;

            if (qNeedH <= maxQ)
            {
                // Fits at full scale
                questionH = qNeedH;
            }
            else if (maxQ >= 40)
            {
                // Shrink to fit: scale = available / needed, min 0.5f
                questionRenderScale = Math.Max(0.5f, (float)maxQ / qNeedH);
                questionH = maxQ;
                // Center the shrunken text vertically
                float scaledH = wrappedQuestionLines.Count * qLineH1 * questionRenderScale;
                questionRenderY = Math.Max(0, (maxQ - scaledH) / 2);
            }
            else
            {
                questionH = maxQ;
            }

            questionRect = new Rectangle(cx, remainingTop, cw, questionH);
            y = remainingTop + questionH + 10;

            // Options
            optionRects.Clear();
            for (int i = 0; i < optCount; i++)
            {
                var optRect = new Rectangle(cx + 10, y, cw - 20, optLineHeights[i]);
                optionRects.Add(optRect);
                y += optLineHeights[i] + 2;
            }
            y += 8;

            // Submit button
            int btnW = 140;
            int btnH = submitH;
            submitRect = new Rectangle(cx + (cw - btnW) / 2, y, btnW, btnH);
            y += btnH + 10;

            // Category label
            categoryRect = new Rectangle(cx, y, cw, catH);
        }

        // =======================================================================
        // update
        // =======================================================================
        public override void update(GameTime gameTime)
        {
            base.update(gameTime);

            if (currentQuestion == null)
            {
                ExitQuizMenu();
                return;
            }

            // Freeze game time each frame so the clock doesn't advance.
            // (Game1.paused = true would also freeze time, but SDV 1.6 skips
            // input dispatch while paused, which prevents clicks from working.)
            Game1.gameTimeInterval = frozenTimeInterval;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            monitor.Log($"QuizMenu.update: state={state}, dt={dt:F4}, timer={questionTimer:F2}", LogLevel.Trace);

            switch (state)
            {
                case QuizState.Playing:
                    if (config.TimerEnabled)
                    {
                        questionTimer -= dt;
                        if (questionTimer <= 0f)
                        {
                            questionTimer = 0f;
                            monitor.Log($"QuizMenu.update: Timer expired!", LogLevel.Info);
                            HandleTimeout();
                        }
                    }
                    break;

                case QuizState.CorrectFeedback:
                case QuizState.WrongFeedback:
                case QuizState.TimeoutFeedback:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        OnFeedbackExpired();
                    }
                    break;

                case QuizState.TreasureShow:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        OnTreasureExpired();
                    }
                    break;

                case QuizState.Transition:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        LoadNextQuestion();
                        state = QuizState.Playing;
                        // Timer already reset in LoadNextQuestion
                    }
                    break;
            }
        }

        // =======================================================================
        // State transition helpers
        // =======================================================================
        private void OnFeedbackExpired()
        {
            if (isStudyMode)
            {
                state = QuizState.Transition;
                stateTimer = 0.5f;
                pendingFinalize = false;
                pendingTransition = false;
                return;
            }

            if (pendingFinalize)
            {
                FishRewarder.ApplyCorrect(statsManager, scopeKey);
				ExitQuizMenu();
            }
            else if (pendingTransition)
            {
                state = QuizState.Transition;
                stateTimer = 3f;
            }
            else
            {
                // Wrong / timeout feedback only -> close
                ExitQuizMenu();
            }
        }

        private void OnTreasureExpired()
        {
            if (currentCorrect >= requiredCorrect)
            {
                // Treasure shown, but also completed -> show correct feedback then finalize
                state = QuizState.CorrectFeedback;
                stateTimer = 1.5f;
                pendingFinalize = true;
                pendingTransition = false;
            }
            else
            {
                // Treasure shown, need more questions -> transition
                state = QuizState.Transition;
                stateTimer = 3f;
            }
        }

        // =======================================================================
        // Answer handling
        // =======================================================================
        private void SubmitAnswer()
        {
            if (markAnswerDone) return;

            var selectedTags = selectedOptions
                .Select(i => currentQuestion!.Options[i].Tag)
                .ToList();
            var correctTags = currentQuestion!.Answer;
            if (correctTags == null || correctTags.Count == 0)
            {
                monitor.Log($"Question '{currentQuestion.Id}' has no answer defined.", LogLevel.Warn);
                return;
            }

            bool isCorrect;
            if (currentQuestion.Type == "single")
            {
                isCorrect = selectedTags.Count == 1 && selectedTags[0] == correctTags[0];
            }
            else
            {
                isCorrect = selectedTags.Count == correctTags.Count &&
                            !selectedTags.Except(correctTags).Any();
            }

            markAnswerDone = true;
            questionManager.RecordAnswer(currentQuestion.Id, isCorrect);
            statsManager.RecordAnswer(scopeKey, isCorrect);

            if (isCorrect)
                HandleCorrectAnswer();
            else
                HandleWrongAnswer();
        }

        private void HandleCorrectAnswer()
        {
            currentCorrect++;

            bool reachedFish = !fishRewarded && ShouldRewardFishNow();
            if (reachedFish)
                fishRewarded = true;

            bool reachedTreasure = !treasureRewarded && ShouldRewardTreasureNow();
            bool completed = currentCorrect >= requiredCorrect;

            if (completed)
            {
                if (reachedTreasure)
                {
                    treasureRewarded = true;
                    FishRewarder.SetTreasureCaught(true);
                }

                state = reachedTreasure ? QuizState.TreasureShow : QuizState.CorrectFeedback;
                stateTimer = reachedTreasure ? 1f : 1.5f;
                pendingFinalize = !reachedTreasure;
                pendingTransition = false;
                Game1.playSound(reachedTreasure ? "coin" : "achievement");
            }
            else if (reachedTreasure)
            {
                treasureRewarded = true;
                FishRewarder.SetTreasureCaught(true);
                Game1.playSound("coin");
                state = QuizState.TreasureShow;
                stateTimer = 1f;
                // Don't set pending* flags yet — will decide after treasure expires
            }
            else
            {
                state = QuizState.CorrectFeedback;
                stateTimer = 1.5f;
                pendingFinalize = false;
                pendingTransition = true;
                Game1.playSound("coin");
            }
        }

        private bool ShouldRewardFishNow()
        {
            if (isStudyMode)
                return false;

            if (FishRewarder.IsLegendaryFish())
                return currentCorrect >= requiredCorrect;

            return currentCorrect >= 1;
        }

        private bool ShouldRewardTreasureNow()
        {
            if (isStudyMode || treasureThreshold <= 0)
                return false;

            if (FishRewarder.IsLegendaryFish())
                return currentCorrect == treasureThreshold;

            return currentCorrect == requiredCorrect;
        }

        private void HandleWrongAnswer()
        {
            if (isStudyMode)
            {
                state = QuizState.WrongFeedback;
                stateTimer = 1.5f;
                pendingFinalize = false;
                pendingTransition = false;
                Game1.playSound("fishEscape");
                return;
            }

            if (treasureRewarded && !fishRewarded)
            {
                FishRewarder.SetManualTreasureOnly(true);
                FishRewarder.SetFishCaught(false);
            }
            else if (fishRewarded)
                FishRewarder.ApplyCorrect(statsManager, scopeKey);
            else if (FishRewarder.IsLegendaryFish())
                FishRewarder.ApplyLegendaryEscape();
            else
                FishRewarder.ApplyWrong();

            state = QuizState.WrongFeedback;
            stateTimer = 1.5f;
            pendingFinalize = false;
            pendingTransition = false;
            Game1.playSound("fishEscape");
        }

        private void HandleTimeout()
        {
            if (markAnswerDone) return;
            markAnswerDone = true;

            questionManager.RecordAnswer(currentQuestion!.Id, false);
            statsManager.RecordAnswer(scopeKey, false);

            if (isStudyMode)
            {
                state = QuizState.TimeoutFeedback;
                stateTimer = 1.5f;
                pendingFinalize = false;
                pendingTransition = false;
                Game1.playSound("fishEscape");
                return;
            }

            if (treasureRewarded && !fishRewarded)
            {
                FishRewarder.SetManualTreasureOnly(true);
                FishRewarder.SetFishCaught(false);
            }
            else if (fishRewarded)
                FishRewarder.ApplyCorrect(statsManager, scopeKey);
            else if (FishRewarder.IsLegendaryFish())
                FishRewarder.ApplyLegendaryEscape();
            else
                FishRewarder.ApplyTimeout();

            state = QuizState.TimeoutFeedback;
            stateTimer = 1.5f;
            pendingFinalize = false;
            pendingTransition = false;
            Game1.playSound("fishEscape");
        }

        // =======================================================================
        // draw
        // =======================================================================
        public override void draw(SpriteBatch b)
        {
            // Dim background
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height),
                Color.Black * 0.5f);

            // Menu frame using SDV's standard dialogue-box texture (parchment + shadow)
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                xPositionOnScreen, yPositionOnScreen, width, height, Color.White, 1f, true);

            // Ensure layout is up-to-date (viewport may not change, but safe)
            ComputeLayout();

            // -- Title --
            string title = isStudyMode
                ? Logic.Translation.Get("ui.study_mode_title")
                : Logic.Translation.Get("ui.title");
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            float titleX = titleRect.X + (titleRect.Width - titleSize.X) / 2;
            float titleY = titleRect.Y;
            b.DrawString(Game1.dialogueFont, title,
                new Vector2(titleX, titleY), Color.Black);

            // -- Progress & Countdown --
            float progX = progressRect.X;
            float progY = progressRect.Y;

            if (requiredCorrect > 1)
            {
                string progress = $"{Logic.Translation.Get("ui.complete")} {currentCorrect}/{requiredCorrect}";
                b.DrawString(Game1.smallFont, progress,
                    new Vector2(progX, progY), Color.DarkSlateGray);
            }

            if (config.TimerEnabled)
            {
                string timerStr = $"{(int)Math.Ceiling(questionTimer)}s";
                Vector2 timerSize = Game1.smallFont.MeasureString(timerStr);
                Color timerColor = questionTimer <= 5f ? Color.Red : Color.DarkSlateGray;
                int iconSize = 20;
                int gap = 6;
                float timerX = progressRect.Right - timerSize.X;
                float iconX = timerX - gap - iconSize;
                float iconY = progY + (timerSize.Y - iconSize) / 2f + 1f;
                DrawClockIcon(b, new Rectangle((int)iconX, (int)iconY, iconSize, iconSize), timerColor);
                b.DrawString(Game1.smallFont, timerStr,
                    new Vector2(timerX, progY), timerColor);
            }

            // -- State-specific drawing --
            switch (state)
            {
                case QuizState.Playing:
                    DrawPlaying(b);
                    break;
                case QuizState.CorrectFeedback:
                    DrawPlaying(b);
                    DrawFeedbackOverlay(b, Logic.Translation.Get("ui.correct"), Color.Green);
                    break;
                case QuizState.WrongFeedback:
                    DrawPlaying(b);
                    DrawFeedbackOverlay(b, Logic.Translation.Get("ui.wrong"), Color.Red);
                    DrawCorrectAnswer(b);
                    break;
                case QuizState.TimeoutFeedback:
                    DrawPlaying(b);
                    DrawFeedbackOverlay(b, Logic.Translation.Get("ui.timeout"), Color.Red);
                    DrawCorrectAnswer(b);
                    break;
                case QuizState.TreasureShow:
                    DrawPlaying(b);
                    DrawTreasureOverlay(b);
                    break;
                case QuizState.Transition:
                    DrawTransition(b);
                    break;
            }

            // -- Category label (always visible) --
            DrawCategoryLabel(b);

            // -- Mouse cursor --
            drawMouse(b);

            // Don't call base.draw(b) — we've drawn everything manually
        }

        // =======================================================================
        // Draw helpers: Playing state
        // =======================================================================
        private void DrawPlaying(SpriteBatch b)
        {
            if (currentQuestion == null) return;

            // Question text (wrapped, auto-scaled)
            float qLineH = LineHeight(Game1.dialogueFont, questionRenderScale);
            float qDrawX = questionRect.X + 10;
            float qDrawY = questionRect.Y + 4 + questionRenderY;
            foreach (string line in wrappedQuestionLines)
            {
                b.DrawString(Game1.dialogueFont, line,
                    new Vector2(qDrawX, qDrawY), Color.Black, 0f, Vector2.Zero, questionRenderScale, SpriteEffects.None, 0f);
                qDrawY += qLineH;
            }

            DrawQuestionTypeBadge(b);

            // Options
            for (int i = 0; i < currentQuestion.Options.Count && i < optionRects.Count; i++)
            {
                var opt = currentQuestion.Options[i];
                string optText = opt.Text;
                var r = optionRects[i];
                bool selected = selectedOptions.Contains(i);

                // Background highlight
                if (selected)
                {
                    b.Draw(Game1.staminaRect, r, Color.LightBlue * 0.4f);
                }

                int selectorSize = 28;
                var selectorRect = new Rectangle(r.X, r.Y + (r.Height - selectorSize) / 2, selectorSize, selectorSize);
                DrawOptionSelector(b, selectorRect, selected);

                // Option text (wrapped)
                float oLineH = LineHeight(Game1.smallFont);
                float oX = r.X + selectorSize + 8;
                float oY = r.Y + 2;
                var wrappedOpt = i < wrappedOptionLines.Count ? wrappedOptionLines[i] : new List<string> { optText };
                foreach (string line in wrappedOpt)
                {
                    b.DrawString(Game1.smallFont, line,
                        new Vector2(oX, oY), Color.Black);
                    oY += oLineH;
                }
            }

            // Submit button
            string btnText;
            if (currentQuestion.Type == "multiple")
            {
                btnText = Logic.Translation.Get("ui.submit", selectedOptions.Count);
            }
            else
            {
                btnText = Logic.Translation.Get("ui.confirm");
            }

            DrawSubmitButton(b, btnText);
        }

        private static void DrawOptionSelector(SpriteBatch b, Rectangle rect, bool selected)
        {
            Color border = new Color(92, 61, 35);
            Color fill = new Color(255, 246, 207);
            Color shadow = new Color(87, 49, 23) * 0.35f;

            b.Draw(Game1.staminaRect, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width, rect.Height), shadow);
            b.Draw(Game1.staminaRect, rect, border);
            b.Draw(Game1.staminaRect, new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6), fill);

            if (!selected) return;

            Color check = new Color(60, 180, 45);
            int thickness = 4;
            DrawLine(b,
                new Vector2(rect.X + 7, rect.Y + 7),
                new Vector2(rect.Right - 7, rect.Bottom - 7),
                check, thickness);
            DrawLine(b,
                new Vector2(rect.Right - 7, rect.Y + 7),
                new Vector2(rect.X + 7, rect.Bottom - 7),
                check, thickness);
        }

        private static void DrawClockIcon(SpriteBatch b, Rectangle rect, Color color)
        {
            Color outline = color;
            Color face = Color.White * 0.75f;
            int cx = rect.X + rect.Width / 2;
            int cy = rect.Y + rect.Height / 2;
            int radius = rect.Width / 2 - 2;

            DrawCircle(b, new Vector2(cx, cy), radius, outline, 2);
            b.Draw(Game1.staminaRect, new Rectangle(cx - 1, cy - radius + 3, 2, 4), outline);
            DrawLine(b, new Vector2(cx, cy), new Vector2(cx, cy - radius + 5), outline, 2);
            DrawLine(b, new Vector2(cx, cy), new Vector2(cx + radius - 5, cy), outline, 2);
            b.Draw(Game1.staminaRect, new Rectangle(cx - 1, cy - 1, 3, 3), face);
        }

        private static void DrawCircle(SpriteBatch b, Vector2 center, int radius, Color color, int thickness)
        {
            const int segments = 24;
            Vector2 previous = center + new Vector2(radius, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 next = center + new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius);
                DrawLine(b, previous, next, color, thickness);
                previous = next;
            }
        }

        private static void DrawLine(SpriteBatch b, Vector2 start, Vector2 end, Color color, int thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            b.Draw(Game1.staminaRect, start, null, color, angle, Vector2.Zero,
                new Vector2(edge.Length(), thickness), SpriteEffects.None, 0f);
        }

        private void DrawQuestionTypeBadge(SpriteBatch b)
        {
            if (currentQuestion == null) return;

            bool isMultiple = currentQuestion.Type == "multiple";
            string label = Logic.Translation.Get(isMultiple ? "ui.type_multiple" : "ui.type_single");
            Vector2 labelSize = Game1.smallFont.MeasureString(label);
            int badgeWidth = (int)labelSize.X + 28;
            int badgeHeight = 30;
            var badgeRect = new Rectangle(questionRect.Right - badgeWidth - 4, questionRect.Y, badgeWidth, badgeHeight);

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                badgeRect.X, badgeRect.Y, badgeRect.Width, badgeRect.Height,
                Color.White, 3f, false);

            b.DrawString(Game1.smallFont, label,
                new Vector2(badgeRect.X + (badgeRect.Width - labelSize.X) / 2,
                    badgeRect.Y + (badgeRect.Height - labelSize.Y) / 2 + 1),
                isMultiple ? new Color(95, 54, 27) : new Color(74, 61, 38));
        }

        private void DrawSubmitButton(SpriteBatch b, string btnText)
        {
            bool hovered = submitRect.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color tint = hovered ? Color.White : new Color(245, 222, 166);

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                submitRect.X, submitRect.Y, submitRect.Width, submitRect.Height,
                tint, 4f, false);

            Vector2 btnSize = Game1.smallFont.MeasureString(btnText);
            b.DrawString(Game1.smallFont, btnText,
                new Vector2(submitRect.X + (submitRect.Width - btnSize.X) / 2,
                    submitRect.Y + (submitRect.Height - btnSize.Y) / 2),
                hovered ? new Color(86, 43, 22) : new Color(64, 35, 20));
        }

        // =======================================================================
        // Draw helpers: overlays
        // =======================================================================
        private void DrawFeedbackOverlay(SpriteBatch b, string message, Color color)
        {
            Vector2 msgSize = Game1.dialogueFont.MeasureString(message);
            float msgX = xPositionOnScreen + (width - msgSize.X) / 2;
            float msgY = yPositionOnScreen + height / 2 - msgSize.Y / 2;

            // Semi-transparent background strip
            b.Draw(Game1.staminaRect,
                new Rectangle((int)msgX - 20, (int)msgY - 10,
                    (int)msgSize.X + 40, (int)msgSize.Y + 20),
                Color.Black * 0.6f);

            b.DrawString(Game1.dialogueFont, message,
                new Vector2(msgX, msgY), color);
        }

        private void DrawCorrectAnswer(SpriteBatch b)
        {
            if (correctAnswerTexts.Count == 0) return;

            string answerLabel = Logic.Translation.Get("ui.answer_is", string.Join(", ", correctAnswerTexts));
            Vector2 ansSize = Game1.smallFont.MeasureString(answerLabel);
            float ansX = xPositionOnScreen + (width - ansSize.X) / 2;
            float ansY = yPositionOnScreen + height / 2 + 30;

            b.DrawString(Game1.smallFont, answerLabel,
                new Vector2(ansX, ansY), Color.Yellow);
        }

        private void DrawTreasureOverlay(SpriteBatch b)
        {
            string msg = Logic.Translation.Get("ui.treasure_acquired");
            Vector2 msgSize = Game1.dialogueFont.MeasureString(msg);
            float msgX = xPositionOnScreen + (width - msgSize.X) / 2;
            float msgY = yPositionOnScreen + height / 2 - msgSize.Y / 2;

            // Gold background
            b.Draw(Game1.staminaRect,
                new Rectangle((int)msgX - 30, (int)msgY - 15,
                    (int)msgSize.X + 60, (int)msgSize.Y + 30),
                Color.Gold * 0.8f);

            // Border
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(128, 256, 64, 64),
                (int)msgX - 30, (int)msgY - 15,
                (int)msgSize.X + 60, (int)msgSize.Y + 30,
                Color.Gold);

            b.DrawString(Game1.dialogueFont, msg,
                new Vector2(msgX, msgY), Color.DarkGoldenrod);
        }

        // =======================================================================
        // Draw helpers: Transition state
        // =======================================================================
        private void DrawTransition(SpriteBatch b)
        {
            int remain = (int)Math.Ceiling(stateTimer);
            string msg;
            if (pendingFinalize || currentCorrect >= requiredCorrect)
            {
                msg = Logic.Translation.Get("ui.complete");
            }
            else
            {
                msg = Logic.Translation.Get("ui.next_in", remain);
            }

            Vector2 msgSize = Game1.dialogueFont.MeasureString(msg);
            float msgX = xPositionOnScreen + (width - msgSize.X) / 2;
            float msgY = yPositionOnScreen + height / 2 - msgSize.Y / 2;

            b.DrawString(Game1.dialogueFont, msg,
                new Vector2(msgX, msgY), Color.Black);
        }

        // =======================================================================
        // Draw: Category label
        // =======================================================================
        private void DrawCategoryLabel(SpriteBatch b)
        {
            string bankId = questionManager.GetCurrentBankId();
            string category = config.SelectedCategory;
            if (string.IsNullOrEmpty(category)) return;

            string? catName = questionManager.GetCategoryI18n(bankId, category);
            if (catName == null) return;

            string label = Logic.Translation.Get("ui.category", catName);
            Vector2 labSize = Game1.smallFont.MeasureString(label);
            float labX = categoryRect.X + (categoryRect.Width - labSize.X) / 2;
            b.DrawString(Game1.smallFont, label,
                new Vector2(labX, categoryRect.Y), Color.Gray);
        }

        // =======================================================================
        // Input handling
        // =======================================================================
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            monitor.Log($"QuizMenu.receiveLeftClick: x={x}, y={y}, state={state}", LogLevel.Trace);
            if (state != QuizState.Playing)
            {
                monitor.Log($"QuizMenu.receiveLeftClick: ignored (state={state})", LogLevel.Trace);
                return;
            }
            if (currentQuestion == null) return;

            // Check option clicks
            for (int i = 0; i < optionRects.Count && i < currentQuestion.Options.Count; i++)
            {
                if (optionRects[i].Contains(x, y))
                {
                    if (currentQuestion.Type == "single")
                    {
                        // Single select: toggle only this one
                        if (selectedOptions.Contains(i))
                            selectedOptions.Remove(i);
                        else
                        {
                            selectedOptions.Clear();
                            selectedOptions.Add(i);
                        }
                    }
                    else
                    {
                        // Multiple select: toggle
                        if (selectedOptions.Contains(i))
                            selectedOptions.Remove(i);
                        else
                            selectedOptions.Add(i);
                    }
                    Game1.playSound("smallSelect");
                    return;
                }
            }

            // Check submit button
            if (submitRect.Contains(x, y) && selectedOptions.Count > 0)
            {
                SubmitAnswer();
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            // No-op per spec
        }

        public override void receiveKeyPress(Keys key)
        {
            monitor.Log($"QuizMenu.receiveKeyPress: key={key}, state={state}", LogLevel.Trace);
            if (key == Keys.Escape && state == QuizState.Playing)
            {
                if (!isStudyMode)
                    FishRewarder.ApplyCancel();

                ExitQuizMenu();
            }
        }

        // =======================================================================
        // Clean exit
        // =======================================================================

        public bool IsStudyMode => isStudyMode;

        public void ExitQuizMenu()
        {
            // Study mode: prevent stamina death on close
            if (isStudyMode && Game1.player.Stamina <= 0)
                Game1.player.Stamina = 1;

            monitor.Log("QuizMenu.ExitQuizMenu: finalizing fishing result immediately", LogLevel.Info);
            FishRewarder.FinishFishingImmediately();
        }
    }
}
