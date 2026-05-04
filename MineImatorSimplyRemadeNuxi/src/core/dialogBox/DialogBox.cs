using System;
using SDL2;

namespace MineImatorSimplyRemadeNuxi.core.dialogBox;

public class DialogBox
{
    public string Title { get; set; }
    public string Message { get; set; }
    public bool IsOpen { get; private set; } = true;

    public DialogBox(string title, string message = "Operation Successful!")
    {
        Title = title;
        Message = message;
    }

    public void Draw()
    {
        if (!IsOpen) return;

        SDL.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_INFORMATION, Title, Message, IntPtr.Zero);
        IsOpen = false;
    }
}
