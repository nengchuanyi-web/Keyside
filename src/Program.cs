using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows;

[assembly: AssemblyTitle("Keyside")]
[assembly: AssemblyDescription("Windows edge shortcut reference panel")]
[assembly: AssemblyVersion("0.6.0.0")]
[assembly: AssemblyFileVersion("0.6.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace ShortcutDock
{
    static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Keyside");
            string verification = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--data-dir" && i + 1 < args.Length) data = Path.GetFullPath(args[++i]);
                else if (args[i] == "--verify" && i + 1 < args.Length) verification = Path.GetFullPath(args[++i]);
                else if (args[i] == "--help")
                {
                    MessageBox.Show("Keyside.exe [--data-dir DIRECTORY] [--verify OUTPUT_DIRECTORY]\n\n拖动标题栏靠近屏幕边缘吸附；移开收起；悬停边缘展开。\nDrag title near an edge to dock; leave to hide; hover edge to open.", "Keyside"); return 0;
                }
            }
            if (verification != null) data = Path.Combine(verification, "data");
            Mutex mutex = null; bool ownsMutex = false;
            try
            {
                if (verification == null)
                {
                    // One instance per user session, independent of the executable's location.
                    mutex = new Mutex(false, @"Local\Keyside-" + Environment.UserName);
                    try { ownsMutex = mutex.WaitOne(0); } catch (AbandonedMutexException) { ownsMutex = true; }
                    if (!ownsMutex) { MessageBox.Show("Keyside 已在运行，请使用系统托盘展开。\nKeyside is running. Open it from the system tray.", "Keyside"); return 0; }
                }
                StateStore store = new StateStore(data);
                Application app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                if (verification != null) return Verification.Run(app, store, verification);
                MainWindow window = new MainWindow(store.Load(), store, false); app.MainWindow = window;
                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
                    File.AppendAllText(Path.Combine(data, "diagnostics.log"), DateTime.Now.ToString("s") + " " + e.Exception + Environment.NewLine);
                    // Preserve the last valid data and terminate after unexpected UI faults.
                    MessageBox.Show(window, "Keyside 遇到错误，详情已保存到 diagnostics.log。\nAn error was logged to diagnostics.log.", "Keyside");
                    e.Handled = true; app.Shutdown(1);
                };
                app.Run(window); return 0;
            }
            catch (Exception e)
            {
                try { Directory.CreateDirectory(data); File.AppendAllText(Path.Combine(data, "startup-error.log"), e + Environment.NewLine); } catch (Exception) { }
                if (verification != null)
                {
                    Directory.CreateDirectory(verification); File.WriteAllText(Path.Combine(verification, "verification-error.txt"), e.ToString());
                }
                else MessageBox.Show("Keyside 启动失败。\n" + e.Message + "\n\n" + Path.Combine(data, "startup-error.log"), "Keyside");
                return 1;
            }
            finally { if (mutex != null) { if (ownsMutex) mutex.ReleaseMutex(); mutex.Dispose(); } }
        }
    }
}
