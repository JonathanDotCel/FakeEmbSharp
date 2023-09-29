#if TOOLS

namespace Addons.FakeEmbed
{
	using Godot;
	using System;
	using System.Diagnostics;

	internal static partial class Extensions
	{
		/// <summary>
		/// check the stuff
		/// </summary>
		public static bool IsWindow(this IntPtr ptr)
		{
			return ptr.ToInt64() != 0 && W32.IsWindow(ptr);
		}

		/// <summary>
		/// do a thing with the thang
		/// </summary>
		public static Process GetParentProcess(this Process process, bool debug = true)
		{
			uint parentProcessId;
			IntPtr hwnd = process.MainWindowHandle;

			if (debug) { GD.Print($"Getting parent process hwnd is {hwnd.ToInt64():X}"); }

			if (hwnd != IntPtr.Zero)
			{
				if (debug) { GD.Print("Checking hwnd"); }
				W32.GetWindowThreadProcessId(hwnd, out parentProcessId);
				if (debug) { GD.Print("The id is " + parentProcessId); }
				if (parentProcessId != 0)
				{
					try
					{
						IntPtr hProcess = W32.OpenProcess((W32.PFlags.PROCESS_QUERY_INFORMATION | W32.PFlags.PROCESS_VM_READ), false, parentProcessId);
						if (hProcess != IntPtr.Zero)
						{
							try
							{
								return Process.GetProcessById((int)parentProcessId);
							}
							finally
							{
								W32.CloseHandle(hProcess);
							}
						}
					}
					catch (Exception e)
					{
						// Handle exceptions if the parent process no longer exists
						GD.PrintErr("Exception: " + e);
					}
				}
			}

			return null;
		}
	}
}

#endif