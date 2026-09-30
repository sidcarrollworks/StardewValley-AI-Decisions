using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewNpcMod;

/// <summary>
/// The morning wait (docs/spec/laya.md, "A morning wait"): a small menu opened at DayStarted while
/// the overnight plan is still running. In single player the game clock does not run while a menu
/// is open (Game1's update paths gate on activeClickableMenu), so the day simply waits. The menu
/// polls the plan job every frame — never blocking, never touching the model — and closes itself
/// when the work is done, when the deadline passes, or on Escape. The plan's own budget keeps
/// capping the remaining decisions after a close (they take their fallback).
/// </summary>
public sealed class MorningWaitMenu : IClickableMenu
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly int _deadlineMs;
    private readonly Func<bool> _isDone;
    private readonly Action _onFinished;

    /// <param name="deadlineMs">Close no later than this (the remaining decisions fall back).</param>
    /// <param name="isDone">Polled each frame; true when the overnight work finished.</param>
    /// <param name="onFinished">Runs once before the menu closes: collects the finished plan
    /// (or logs that it is still running, in the deadline case).</param>
    public MorningWaitMenu(int deadlineMs, Func<bool> isDone, Action onFinished)
    {
        _deadlineMs = deadlineMs;
        _isDone = isDone;
        _onFinished = onFinished;
    }

    public override void update(GameTime time)
    {
        base.update(time);
        if (_isDone() || _watch.ElapsedMilliseconds >= _deadlineMs)
        {
            _onFinished();
            exitThisMenu(playSound: false);
        }
    }

    public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
    {
        if (key == Microsoft.Xna.Framework.Input.Keys.Escape)
            exitThisMenu(playSound: false);
    }

    public override void draw(SpriteBatch b)
    {
        const int width = 520;
        const int height = 130;
        int x = (Game1.uiViewport.Width - width) / 2;
        int y = (Game1.uiViewport.Height - height) / 2;

        Game1.drawDialogueBox(x, y, width, height, speaker: false, drawOnlyBox: true);
        string dots = new string('.', (int)(_watch.ElapsedMilliseconds / 300) % 3 + 1);
        Utility.drawTextWithShadow(b, "The valley is waking up" + dots, Game1.dialogueFont,
            new Vector2(x + 48, y + 44), Game1.textColor);
        drawMouse(b);
    }
}
