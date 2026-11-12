using System;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("video2gif")]
[assembly: System.Reflection.AssemblyDescription("video2gif — видео и картинки в GIF, скачивание TikTok, YouTube и музыки")]
[assembly: System.Reflection.AssemblyProduct("video2gif")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]

namespace Video2Gif
{
    internal static class Program
    {
        public const string AppName = "video2gif";
        public const string Version = "3.0";

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
