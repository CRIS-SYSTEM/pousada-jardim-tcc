
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace CadastroProdutoWpf
{
    public partial class App : Application
    {
        public App()
        {
            // Captura erros não tratados na interface WPF.
            DispatcherUnhandledException +=
                App_DispatcherUnhandledException;

            // Captura outras exceções não tratadas.
            AppDomain.CurrentDomain.UnhandledException +=
                CurrentDomain_UnhandledException;
        }

        private static void RegistrarErro(Exception ex)
        {
            try
            {
                string pasta = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "PousadaJardim");

                Directory.CreateDirectory(pasta);

                string arquivo = Path.Combine(
                    pasta,
                    "erros.log");

                File.AppendAllText(
                    arquivo,
                    $"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                    ex.ToString() +
                    "\n-------------------------\n");
            }
            catch
            {
                // Evita uma segunda falha ao registrar o erro.
            }
        }

        private void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            RegistrarErro(e.Exception);

            MessageBox.Show(
                e.Exception.ToString(),
                "Erro na aplicação",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Não impede o encerramento após erro grave.
        }

        private void CurrentDomain_UnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                RegistrarErro(ex);
            }
        }
    }
}
