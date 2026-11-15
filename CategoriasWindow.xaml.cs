using System;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    public partial class CategoriasWindow : Window
    {
        private readonly CategoriaDAO dao = new();
        private int idSelecionado;

        public CategoriasWindow()
        {
            InitializeComponent();
            ((FrameworkElement)Content).Loaded += (_, _) => Carregar();
        }

        private void Carregar()
        {
            try
            {
                gridItens.ItemsSource = dao.Listar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao consultar categorias: " + ex.Message,
                    "Pousada Jardim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridItens_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridItens.SelectedItem is not Categoria item) return;
            idSelecionado = item.IdCategoria;
            txtNome.Text = item.Nome;
            txtDescricao.Text = item.Descricao ?? string.Empty;
        }

        private void Novo_Click(object sender, RoutedEventArgs e)
        {
            gridItens.SelectedItem = null;
            idSelecionado = 0;
            txtNome.Clear();
            txtDescricao.Clear();
            txtNome.Focus();
        }

        private void Atualizar_Click(object sender, RoutedEventArgs e) => Carregar();

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            var item = new Categoria
            {
                IdCategoria = idSelecionado,
                Nome = txtNome.Text,
                Descricao = txtDescricao.Text
            };
            try
            {
                if (idSelecionado == 0) dao.Cadastrar(item);
                else dao.Atualizar(item);
                MessageBox.Show("Registro salvo com sucesso.", "Pousada Jardim",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Novo_Click(sender, e);
                Carregar();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show("Já existe um registro com esse nome.", "Cadastro",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível salvar: " + ex.Message, "Cadastro",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
