
using System.Windows;

namespace CadastroProdutoWpf
{
    public partial class InicialWindow : Window
    {
        public InicialWindow()
        {
            InitializeComponent();
        }

        private void BtnAcessar_Click(
            object sender, RoutedEventArgs e)
        {
            // Cria a janela de login.
            LoginWindow telaLogin = new LoginWindow();

            // Define o login como janela principal.
            Application.Current.MainWindow = telaLogin;

            // Exibe o login.
            telaLogin.Show();

            // Fecha a tela de apresentação.
            this.Close();
        }
    }
}
