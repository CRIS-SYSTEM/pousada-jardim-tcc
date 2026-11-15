using System;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace CadastroProdutoWpf;

public partial class UsuariosWindow : Window
{
    private readonly Usuario administrador;
    private readonly UsuariosDAO dao = new();
    private int idSelecionado;
    public UsuariosWindow(Usuario usuario)
    {
        InitializeComponent();
        administrador = usuario;
        if (usuario.Perfil != "ADMINISTRADOR" || !usuario.Ativo)
            throw new UnauthorizedAccessException("Acesso restrito ao administrador.");
        cmbPerfil.SelectedIndex = 1;
        ((FrameworkElement)Content).Loaded += (_, _) => Carregar();
    }
    private void Carregar()
    {
        try { gridUsuarios.ItemsSource = dao.Listar(); }
        catch (Exception ex) { MessageBox.Show("Falha ao consultar usuários: " + ex.Message); }
    }
    private void GridUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (gridUsuarios.SelectedItem is not Usuario u) return;
        idSelecionado = u.IdUsuario;
        txtNome.Text = u.Nome;
        txtLogin.Text = u.Login;
        cmbPerfil.SelectedIndex = u.Perfil == "ADMINISTRADOR" ? 0 : 1;
        chkAtivo.IsChecked = u.Ativo;
        txtSenha.Clear();
    }
    private void BtnNovo_Click(object sender, RoutedEventArgs e)
    {
        gridUsuarios.SelectedItem = null;
        idSelecionado = 0;
        txtNome.Clear(); txtLogin.Clear(); txtSenha.Clear();
        cmbPerfil.SelectedIndex = 1;
        chkAtivo.IsChecked = true;
    }
    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var u = new Usuario
            {
                IdUsuario = idSelecionado, Nome = txtNome.Text,
                Login = txtLogin.Text,
                Perfil = (cmbPerfil.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "FUNCIONARIO",
                Ativo = chkAtivo.IsChecked == true
            };
            dao.Salvar(u, txtSenha.Password, administrador.IdUsuario);
            MessageBox.Show("Usuário salvo com sucesso.", "Pousada Jardim");
            BtnNovo_Click(sender, e);
            Carregar();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        { MessageBox.Show("Este login já está cadastrado."); }
        catch (Exception ex)
        { MessageBox.Show("Não foi possível salvar: " + ex.Message); }
    }
}
