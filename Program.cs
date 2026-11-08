using System;
using System.Windows.Forms;

#if LEGACY
[assembly: System.Reflection.AssemblyTitle("video2gif")]
[assembly: System.Reflection.AssemblyDescription("Видео и картинки в GIF")]
[assembly: System.Reflection.AssemblyProduct("video2gif")]
#elif NOTIKTOK
[assembly: System.Reflection.AssemblyTitle("Friday")]
[assembly: System.Reflection.AssemblyDescription("Friday — видео и картинки в GIF, без скачивания TikTok")]
[assembly: System.Reflection.AssemblyProduct("Friday")]
#else
[assembly: System.Reflection.AssemblyTitle("Jarvis")]
[assembly: System.Reflection.AssemblyDescription("Jarvis — видео и картинки в GIF, скачивание TikTok без водяного знака")]
[assembly: System.Reflection.AssemblyProduct("Jarvis")]
#endif
[assembly: System.Reflection.AssemblyVersion("2.4.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.4.0.0")]

namespace Video2Gif
{
    internal static class Program
    {
#if LEGACY
        public const string AppName = "video2gif";
#elif NOTIKTOK
        public const string AppName = "Friday";
#else
        public const string AppName = "Jarvis";
#endif
        public const string Version = "2.4";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                return SelfTest.Run();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось запустить приложение.\n\n" + ex.Message,
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
            return 0;
        }
    }
}
