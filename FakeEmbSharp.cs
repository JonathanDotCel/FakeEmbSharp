//
// MIT License - see LICENSE file
//
// HOME: https://github.com/JonathanDotCel/FakeEmbSharp
// BASED ON: https://github.com/vitorbalbio/GodotFakeEmbedded
// TARG: Godot 4.11
// REF: https://docs.godotengine.org/en/stable/tutorials/plugins/editor/making_plugins.html
//

#if TOOLS

using Godot;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

[Tool]
public partial class FakeEmbSharp : EditorPlugin
{


    Control dockControl;        // the FakeEmbSharp dock area
    Button refreshButton;       // the button on that

    // Previous "Playing" state
    protected static bool wasPlaying = false;

    // Class name of the engine window
    // Note: both the editor and game use this
    //       so some filtering is required.
    public static string engineClassName = "Engine";

    // Account for the titlebar height of MS Windows' windows.
    public static int windowsTitlebarOffset = 25;

    // Additional offset from the top of the dock area the game will be placed in
    // Also affects height
    public static int dockHeightOffset = 50;

    // The game window's HWND
    // cache it so we don't need to keep looking it
    // up from the process list.
    public static IntPtr cachedGameHwnd = IntPtr.Zero;

    // A sometimes food.
    public static bool debugMode = false;


    // Do the start thing
    public override void _EnterTree()
    {
        VBoxContainer mainContainer = GetEditorInterface().GetEditorMainScreen();
        mainContainer.Connect( "item_rect_changed", new Callable( this, nameof( SizeChanged ) ) );

        dockControl = (Control)GD.Load<PackedScene>( "addons/FakeEmbSharp/Scene_FakeEmbSharp.tscn" ).Instantiate();
        
        AddControlToDock( DockSlot.LeftUr, dockControl );

        refreshButton = dockControl.GetNode<Button>( "Button" );
        refreshButton.Pressed += RefreshButton_Pressed;
    }


    // Do the end thing
    public override void _ExitTree()
    {

        VBoxContainer mainContainer = GetEditorInterface().GetEditorMainScreen();
        mainContainer.Disconnect( "item_rect_changed", new Callable( this, nameof( SizeChanged ) ) );

        RemoveControlFromDocks( dockControl );
        dockControl.Free();
    }


    public override void _Ready()
    {
        if ( debugMode )
        {
            GD.Print( "FakeEmbSharp - Run" );
        }
        Updaterects();

    }


    // Main window or any panel changed size
    public void SizeChanged()
    {

        if ( debugMode )
        {
            GD.Print( "FakeEmbSharp - SizeChanged" );
        }

        Updaterects();

        if ( GetEditorInterface().IsPlayingScene() )
        {
            ReparentGameWindow();
        }

    }


    /// <summary>
    /// Sets the startup location of the game window
    /// Doesn't actually reposition or reparent the window
    /// but makes an effort to get it vaguely in the right place, 
    /// on the right screen, so accuracy isn't paramount.
    /// </summary>
    public void Updaterects()
    {

        VBoxContainer editor = GetEditorInterface().GetEditorMainScreen();
        Rect2 editorRect = editor.GetGlobalRect();

        Rect2 dockRect = dockControl.GetGlobalRect();

        ProjectSettings.SetSetting( "display/window/size/test_width", dockRect.Size.X );
        ProjectSettings.SetSetting( "display/window/size/test_height", dockRect.Size.Y - windowsTitlebarOffset );
        ProjectSettings.SetSetting( "display/window/size/fullscreen", false );

        // let the user decide with borderless, always on top, etc.
        //ProjectSettings.SetSetting( "display/window/size/borderless", true );
        //ProjectSettings.SetSetting( "display/window/size/always_on_top", false );

        GetEditorInterface().GetEditorSettings().Set( "run/window_placement/rect", 2 );
        GetEditorInterface().GetEditorSettings().Set( "run/window_placement/rect_custom_position", dockRect.Position );

        if ( debugMode )
        {
            GD.Print( "FakeEmbSharp - Update Rects" );
        }

    }


    // Thankfully there are no race conditions here
    // no extra 1-frame wait or anything
    // It just seems to work as of godot 4.11
    public override void _Process( double delta )
    {

        if ( !Engine.IsEditorHint() )
        {
            return;
        }

        bool playing = GetEditorInterface().IsPlayingScene();

        if ( playing && !wasPlaying )
        {
            if ( debugMode )
            {
                GD.Print( "FakeEmbSharp Entered play state" );
            }
            ReparentGameWindow();
        }
        wasPlaying = playing;

    }


    private void RefreshButton_Pressed()
    {
        GD.Print( "Refresh button pressed" );
        ReparentGameWindow();
    }


    #region Win API Imports n consts

    // Consts for window pos
    public const int SWP_NOSIZE = 0x0001;
    public const int SWP_NOMOVE = 0x0002;
    public const int SWP_NOZORDER = 0x0004;
    public const int SWP_NOACTIVATE = 0x0010;
    public const int SWP_FRAMECHANGED = 0x0020;
    public const int SWP_SHOWWINDOW = 0x0040;
    public const int SWP_HIDEWINDOW = 0x0080;

    // Consts for window size
    private const int GWL_STYLE = -16;
    private const int WS_BORDER = 0x00800000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        PROCESS_QUERY_INFORMATION = 0x0400,
        PROCESS_VM_READ = 0x0010,
    }

    [DllImport( "user32.dll", SetLastError = true )]
    static extern IntPtr Findwindow( string lpClassName, string lpWindowName );

    [DllImport( "user32.dll" )]
    static extern IntPtr GetParent( IntPtr hWnd );


    [DllImport( "kernel32.dll", SetLastError = true )]
    [return: MarshalAs( UnmanagedType.Bool )]
    private static extern bool CloseHandle( IntPtr hObject );

    [DllImport( "user32.dll", CharSet = CharSet.Unicode )]
    public static extern IntPtr SetParent( IntPtr hWndChild, IntPtr hWndNewParent );

    [DllImport( "user32.dll", SetLastError = true, CharSet = CharSet.Auto )]
    static extern int GetClassName( IntPtr hWnd, StringBuilder lpClassName, int nMaxCount );

    public static string GetWindowClass( IntPtr hwnd )
    {
        StringBuilder className = new StringBuilder( 256 );
        int len = GetClassName( hwnd, className, className.Capacity );
        return className.ToString();
    }

    [DllImport( "user32.dll", CharSet = CharSet.Unicode )]
    public static extern IntPtr GetWindowThreadProcessId( IntPtr hWnd, out uint ProcessId );

    [DllImport( "user32.dll" )]
    [return: MarshalAs( UnmanagedType.Bool )]
    public static extern bool SetWindowPos( IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags );

    [DllImport( "user32.dll" )]
    public static extern int GetWindowLong( IntPtr hWnd, int nIndex );

    [DllImport( "user32.dll" )]
    public static extern int SetWindowLong( IntPtr hWnd, int nIndex, int dwNewLong );

    [DllImport( "user32.dll" )]
    [return: MarshalAs( UnmanagedType.Bool )]
    public static extern bool IsWindow( IntPtr hWnd );

    [DllImport( "kernel32.dll" )]
    private static extern IntPtr OpenProcess( ProcessAccessFlags dwDesiredAccess, [MarshalAs( UnmanagedType.Bool )] bool bInheritHandle, uint dwProcessId );

    #endregion Win API Imports n consts


    public static Process GetParentProcess( Process process )
    {
        uint parentProcessId;
        IntPtr hwnd = process.MainWindowHandle;

        GD.Print( $"Getting parent process hwnd is {hwnd.ToInt64():X}" );

        if ( hwnd != IntPtr.Zero )
        {

            GD.Print( "Checking hwnd" );

            GetWindowThreadProcessId( hwnd, out parentProcessId );

            GD.Print( "The id is " + parentProcessId );

            if ( parentProcessId != 0 )
            {
                try
                {
                    IntPtr hProcess = OpenProcess( (ProcessAccessFlags.PROCESS_QUERY_INFORMATION | ProcessAccessFlags.PROCESS_VM_READ), false, parentProcessId );
                    if ( hProcess != IntPtr.Zero )
                    {
                        try
                        {
                            return Process.GetProcessById( (int)parentProcessId );
                        }
                        finally
                        {
                            CloseHandle( hProcess );
                        }
                    }
                }
                catch ( Exception e )
                {
                    // Handle exceptions if the parent process no longer exists
                    GD.Print( "Exception: " + e );
                }
            }
        }

        return null;
    }


    /// <summary>
    /// Reparent the game window into the Editor window
    /// Attempt to pin it into the dock area we just created
    /// Remove borders n stuff
    /// Try to cache the game window to prevent future lookups
    /// </summary>
    protected void ReparentGameWindow()
    {

        Process editorProcess = Process.GetCurrentProcess();
        IntPtr editorHwnd = editorProcess.MainWindowHandle;

        if ( debugMode )
        {
            GD.Print( "Cached hwnd = " + cachedGameHwnd.ToInt64() );
        }

        bool gotCachedHWND = cachedGameHwnd.ToInt64() != 0 && IsWindow( cachedGameHwnd );

        if ( !gotCachedHWND )
        {

            if ( debugMode )
            {
                GD.Print( "Searching for game..." );
            }

            Process[] procs = Process.GetProcesses();
            foreach ( Process p in procs )
            {

                IntPtr targetHwnd = p.MainWindowHandle;

                // Ignore any windowless processes
                if ( targetHwnd == IntPtr.Zero )
                    continue;

                // Ignore the editor's window
                if ( targetHwnd == editorHwnd )
                    continue;

                // Does this window belong to a godot game?
                string winClass = GetWindowClass( targetHwnd );

                // Godot? Godot.
                if ( winClass == engineClassName )
                {
                    if ( debugMode )
                    {
                        GD.Print( "Found the game..." );
                    }
                    cachedGameHwnd = targetHwnd;
                    break;
                }

            }

        } // cached value still relevant

        if ( cachedGameHwnd == IntPtr.Zero )
        {
            // TODO: not sure if this has the potentially to be extra super duper spammy.
            GD.Print( "FakeEmbSharp can't find the game window." );
            return;
        }

        // We've found the game window, or have it cached
        // Finally, reparent it.

        if ( debugMode )
        {
            GD.Print( $"Found godot game window @ {cachedGameHwnd.ToInt64():X}" );
        }
        SetParent( cachedGameHwnd, editorHwnd );

        // Remove the border, buttons, stuff.
        int style = GetWindowLong( cachedGameHwnd, GWL_STYLE );
        style &= ~(WS_BORDER | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
        SetWindowLong( cachedGameHwnd, GWL_STYLE, style & ~WS_SYSMENU );

        // Combine 2 calls:
        // - reposition the window
        // - SWP_FRAMECHANGED for the SetWindowLong call above

        Rect2 dockRect = dockControl.GetGlobalRect();
        SetWindowPos( cachedGameHwnd, IntPtr.Zero, (int)dockRect.Position.X, (int)dockRect.Position.Y + dockHeightOffset, (int)dockRect.Size.X, (int)dockRect.Size.Y - dockHeightOffset, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED );

    }



}

#endif