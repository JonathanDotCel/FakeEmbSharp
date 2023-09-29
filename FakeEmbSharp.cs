//
// MIT License - see LICENSE file
//
// HOME: https://github.com/JonathanDotCel/FakeEmbSharp
// BASED ON: https://github.com/vitorbalbio/GodotFakeEmbedded
// TARG: Godot 4.11
// REF: https://docs.godotengine.org/en/stable/tutorials/plugins/editor/making_plugins.html
//

// TODO:
// https://docs.godotengine.org/en/stable/tutorials/plugins/editor/making_main_screen_plugins.html

#if TOOLS

namespace Addons.FakeEmbed
{
	using Godot;
	using System;
	using System.Diagnostics;

	[Tool]
	public partial class FakeEmbSharp : EditorPlugin
	{
		public const string TAB_TITLE = "Game";
		public const string GUI_SCENE_PATH = "addons/FakeEmbSharp/Scene_FakeEmbSharp.tscn";
		public const string RECT_EVENT = "item_rect_changed";

		private Control dockControl;        // the FakeEmbSharp dock area
		private Button refreshButton;       // the button on that

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
			VBoxContainer mainContainer = GetMainContainer();
			mainContainer.Connect(RECT_EVENT, new Callable(this, nameof(SizeChanged)));

			dockControl = (Control)GD.Load<PackedScene>(GUI_SCENE_PATH).Instantiate();
			dockControl.Name = TAB_TITLE;

			AddControlToDock(DockSlot.LeftUr, dockControl);

			refreshButton = dockControl.GetNode<Button>("Button");
			refreshButton.Pressed += RefreshButton_Pressed;
		}

		public override void _MakeVisible(bool visible)
		{
			// can we fake-hide the window when visible=false?
		}

		public override void _EnablePlugin()
		{
			// add custom settings here?
		}


		public override Texture2D _GetPluginIcon()
		{
			return GetEditorInterface().GetBaseControl().GetThemeIcon("Node", "EditorIcons");
		}

		public override bool _HasMainScreen()
		{
			return true;
		}

		public override string _GetPluginName()
		{
			return "Game";
		}

		// Do the end thing
		public override void _ExitTree()
		{
			VBoxContainer mainContainer = GetMainContainer();
			mainContainer.Disconnect(RECT_EVENT, new Callable(this, nameof(SizeChanged)));
			GD.Print(mainContainer.Name);
			RemoveControlFromDocks(dockControl);
			dockControl.Free();
		}

		public override void _Ready()
		{
			Log("FakeEmbSharp - Run");
			Updaterects();
			ReparentGameWindow();
		}

		// Main window or any panel changed size
		public void SizeChanged()
		{
			Log("FakeEmbSharp - SizeChanged");

			Updaterects();

			if (GetEditorInterface().IsPlayingScene())
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

			VBoxContainer editor = GetMainContainer();
			Rect2 editorRect = editor.GetGlobalRect();

			Rect2 dockRect = dockControl.GetGlobalRect();

			// are these settings actually used?
			ProjectSettings.SetSetting("display/window/size/test_width", dockRect.Size.X);
			ProjectSettings.SetSetting("display/window/size/test_height", dockRect.Size.Y - windowsTitlebarOffset);
			ProjectSettings.SetSetting("display/window/size/fullscreen", false);
			GetEditorInterface().GetEditorSettings().Set("run/window_placement/rect", 2);
			GetEditorInterface().GetEditorSettings().Set("run/window_placement/rect_custom_position", dockRect.Position);
			// let the user decide with borderless, always on top, etc.
			//ProjectSettings.SetSetting( "display/window/size/borderless", true );
			//ProjectSettings.SetSetting( "display/window/size/always_on_top", false );

			Log("FakeEmbSharp - Update Rects");

		}


		// Thankfully there are no race conditions here
		// no extra 1-frame wait or anything
		// It just seems to work as of godot 4.11
		public override void _Process(double delta)
		{
			// to we need this check?
			if (!Engine.IsEditorHint()) { return; }

			// check init
			bool playing = GetEditorInterface().IsPlayingScene();
			if (playing && !wasPlaying) { OnPlayStart(); }
			wasPlaying = playing;
		}

		private void OnPlayStart()
		{
			Log("FakeEmbSharp Entered play state");
			ReparentGameWindow();
		}

		private void Log(string v)
		{
			// by doing the check here, you can save having to write more lines elsewhere and use less energy than bitcoin
			if (debugMode) { GD.Print(v); }
		}

		private void RefreshButton_Pressed()
		{
			Log("Refresh button pressed");
			ReparentGameWindow();
		}

		private VBoxContainer GetMainContainer()
		{
			return GetEditorInterface().GetEditorMainScreen();
		}

		/// <summary>
		/// 
		/// </summary>
		private static IntPtr FindGameWindow(Process editorProcess, bool debug = false)
		{
			IntPtr editorHwnd = editorProcess.MainWindowHandle;
			Process[] procs = Process.GetProcesses();
			foreach (Process p in procs)
			{
				IntPtr targetHwnd = p.MainWindowHandle;
				// Ignore any windowless processes
				if (targetHwnd == IntPtr.Zero) continue;
				// Ignore the editor's window
				if (targetHwnd == editorHwnd) continue;
				// Does this window belong to a godot game?
				string winClass = W32.GetWindowClass(targetHwnd);
				// Godot? Godot.
				if (winClass == engineClassName)
				{
					if (debug) { GD.Print("Found the game..."); }
					return targetHwnd;
					
				}
			}
			return IntPtr.Zero;
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

			Log("Cached hwnd = " + cachedGameHwnd.ToInt64());

			if (!cachedGameHwnd.IsWindow())
			{
				Log("Searching for game...");
				cachedGameHwnd = FindGameWindow(Process.GetCurrentProcess(), debugMode);

			} // cached value still relevant

			if (cachedGameHwnd == IntPtr.Zero)
			{
				// TODO: not sure if this has the potentially to be extra super duper spammy.
				Log("FakeEmbSharp can't find the game window.");
				return;
			}

			// We've found the game window, or have it cached
			// Finally, reparent it.

			Log($"Found godot game window @ {cachedGameHwnd.ToInt64():X}");
			W32.SetParent(cachedGameHwnd, editorHwnd);

			// Remove the border, buttons, stuff.
			cachedGameHwnd.HideWindowButtons();

			// Combine 2 calls:
			// - reposition the window
			// - SWP_FRAMECHANGED for the SetWindowLong call above

			Rect2 dockRect = dockControl.GetGlobalRect();
			W32.SetWindowPos
			(
				// in *the biz*, we call this way of supplying arguments the stallmann sandwich
				cachedGameHwnd,
				IntPtr.Zero,
				(int)dockRect.Position.X,
				(int)dockRect.Position.Y + dockHeightOffset,
				(int)dockRect.Size.X,
				(int)dockRect.Size.Y - dockHeightOffset,
				W32.SWP_NOZORDER | W32.SWP_NOACTIVATE | W32.SWP_FRAMECHANGED
			);

		}

	}
}

#endif