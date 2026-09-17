using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using DukkaniPOS.Data;

namespace DukkaniPOS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Register Native DllImportResolver to map e_sqlite3 -> winsqlite3.dll on Windows
            NativeLibrary.SetDllImportResolver(typeof(System.Data.SQLite.SQLiteConnection).Assembly, SQLiteNativeDllResolver);

            // Initialize SQLite Database & Tables
            DatabaseHelper.InitializeDatabase();

            base.OnStartup(e);
        }

        private static IntPtr SQLiteNativeDllResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName.Equals("e_sqlite3", StringComparison.OrdinalIgnoreCase) ||
                libraryName.Equals("sqlite3", StringComparison.OrdinalIgnoreCase))
            {
                if (NativeLibrary.TryLoad("winsqlite3.dll", assembly, searchPath, out IntPtr handle))
                {
                    return handle;
                }
            }
            return IntPtr.Zero;
        }
    }
}
