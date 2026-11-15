
using System;
using System.Windows;

namespace CadastroProdutoWpf
{
    public partial class LoginWindow : Window
    {
        // Mantém o usuário lembrado durante a execução.
        private static string usuarioLembrado = "";

        public LoginWindow()
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(usuarioLembrado))
            {
                txtLogin.Text = usuarioLembrado;
                chkLembrar.IsChecked = true;
            }

            txtLogin.Focus();
        }

        // Alterna a visualização sem perder o valor digitado.
        private void BtnMostrarSenha_Click(object sender, RoutedEventArgs e)
        {
            if (txtSenhaVisivel.Visibility == Visibility.Visible)
            {
                txtSenha.Password = txtSenhaVisivel.Text;
                txtSenhaVisivel.Visibility = Visibility.Collapsed;
                txtSenha.Visibility = Visibility.Visible;
                iconeOlho.Text = "◉";
                txtSenha.Focus();
            }
            else
            {
                txtSenhaVisivel.Text = txtSenha.Password;
                txtSenha.Visibility = Visibility.Collapsed;
                txtSenhaVisivel.Visibility = Visibility.Visible;
                iconeOlho.Text = "◎";
                txtSenhaVisivel.Focus();
            }
        }

        // Executado quando o usuário clica em ENTRAR.
        private void BtnEntrar_Click(
            object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string senha = txtSenhaVisivel.Visibility == Visibility.Visible
                ? txtSenhaVisivel.Text
                : txtSenha.Password;

            // Verifica o preenchimento.
            if (string.IsNullOrWhiteSpace(login) ||
                string.IsNullOrWhiteSpace(senha))
            {
                MessageBox.Show(
                    "Informe seu usuário e senha.",
                    "Campos obrigatórios",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                // Consulta o usuário no MySQL.
                UsuarioDAO dao = new UsuarioDAO();

                Usuario? usuario = dao.Autenticar(
                    login, senha);

                // Verifica as credenciais.
                if (usuario == null)
                {
                    MessageBox.Show(
                        "Usuário ou senha inválidos.",
                        "Acesso negado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    txtSenha.Clear();
                    txtSenhaVisivel.Clear();
                    txtSenha.Focus();
                    return;
                }

                // Guarda somente o login, nunca a senha.
                usuarioLembrado =
                    chkLembrar.IsChecked == true
                    ? login
                    : "";

                // Abre o painel de módulos.
                ModulosWindow painel =
                    new ModulosWindow(usuario);

                Application.Current.MainWindow = painel;

                painel.Show();

                // Fecha a janela de login.
                this.Close();
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Não foi possível acessar o sistema. " +
                    "Verifique a conexão e tente novamente.",
                    "Erro de autenticação",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
