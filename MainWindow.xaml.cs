
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    public partial class MainWindow : Window
    {
        private readonly ProdutoDAO produtoDAO = new();

        private int? idProdutoEdicao = null;

        private List<ProdutoLinha> produtos = new();

        private List<CategoriaOpcao> categorias = new();



        public MainWindow()
        {
            InitializeComponent();

            ((FrameworkElement)Content).Loaded += MainWindow_Loaded;
        }


        private void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                // Primeiro, carrega as categorias.
                CarregarCategorias();

                // Depois, consulta os produtos.
                CarregarProdutos();

                // Mantém o formulário pronto
                // para um novo cadastro.
                LimparCampos();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao carregar os dados:\n\n" +
                    ex.ToString(),
                    "Erro no módulo Produtos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private class CategoriaOpcao
        {
            public int IdCategoria { get; set; }
            public string Nome { get; set; } = "";
        }

        private class ProdutoLinha
        {
            public Produto Produto { get; set; } = new();

            public int IdProduto => Produto.IdProduto;
            public string Nome => Produto.Nome;
            public double Preco => Produto.Preco;
            public int Quantidade => Produto.Quantidade;
            public string UnidadeMedida => Produto.UnidadeMedida;
            public DateTime? DataVencimento =>
                Produto.DataVencimento;
            public bool Situacao => Produto.Situacao;

            public string NomeCategoria { get; set; } = "";
        }

        // =====================================
        // CONEXÃO PARA CONSULTAR CATEGORIAS
        // =====================================

        private MySqlConnection CriarConexao() => ConexaoFactory.CriarConexao();

        // =====================================
        // CARREGAR CATEGORIAS
        // =====================================

        private void CarregarCategorias()
        {
            try
            {
                categorias.Clear();

                using var conexao = CriarConexao();
                conexao.Open();

                const string sql = @"
                    SELECT id_categoria, nome
                    FROM categoria
                    ORDER BY nome";

                using var comando =
                    new MySqlCommand(sql, conexao);

                using var leitor = comando.ExecuteReader();

                while (leitor.Read())
                {
                    categorias.Add(new CategoriaOpcao
                    {
                        IdCategoria =
                            leitor.GetInt32("id_categoria"),

                        Nome = leitor.GetString("nome")
                    });
                }

                cmbCategoria.ItemsSource =
                    categorias.ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao carregar categorias:\n" +
                    ex.Message,
                    "Categorias",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =====================================
        // CARREGAR PRODUTOS
        // =====================================

        private void CarregarProdutos()
        {
            try
            {
                var lista = produtoDAO.ListarProdutos();

                produtos = lista.Select(p => new ProdutoLinha
                {
                    Produto = p,
                    NomeCategoria = categorias
                        .FirstOrDefault(c =>
                            c.IdCategoria == p.IdCategoria)
                        ?.Nome ?? "Sem categoria"
                }).ToList();

                AplicarPesquisa();
            }
            catch (Exception ex)
            {
                gridProdutos.ItemsSource = null;

                MessageBox.Show(
                    "Erro ao carregar produtos:\n" +
                    ex.Message,
                    "Produtos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =====================================
        // PESQUISAR PRODUTOS
        // =====================================

        private void TxtPesquisa_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            AplicarPesquisa();
        }

        private void FiltroStatus_Changed(object sender, SelectionChangedEventArgs e) => AplicarPesquisa();

        private void AplicarPesquisa()
        {
            // O evento pode ocorrer durante
            // a inicialização da janela.
            if (gridProdutos == null ||
                txtPesquisa == null)
                return;

            string pesquisa =
                txtPesquisa.Text.Trim();

            var filtrados = produtos
                .Where(p =>
                    p.Nome.Contains(
                        pesquisa,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    p.NomeCategoria.Contains(
                        pesquisa,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            int status = cmbFiltroStatus?.SelectedIndex ?? 0;
            if (status == 1) filtrados = filtrados.Where(p => p.Situacao).ToList();
            else if (status == 2) filtrados = filtrados.Where(p => !p.Situacao).ToList();
            gridProdutos.ItemsSource = filtrados;
        }

        // =====================================
        // SELECIONAR PRODUTO PARA EDIÇÃO
        // =====================================

        private void GridProdutos_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (gridProdutos.SelectedItem
                is not ProdutoLinha linha)
                return;

            Produto produto = linha.Produto;

            idProdutoEdicao = produto.IdProduto;

            txtNome.Text = produto.Nome;

            txtPreco.Text = produto.Preco.ToString(
                "N2",
                CultureInfo.GetCultureInfo("pt-BR"));

            cmbCategoria.SelectedValue =
                produto.IdCategoria;

            foreach (ComboBoxItem item
                     in cmbUnidade.Items)
            {
                if (string.Equals(
                    item.Content?.ToString(),
                    produto.UnidadeMedida,
                    StringComparison.OrdinalIgnoreCase))
                {
                    cmbUnidade.SelectedItem = item;
                    break;
                }
            }

            chkAtivo.IsChecked =
                produto.Situacao;

            btnSalvar.Content = "Salvar alterações";

            btnInativar.IsEnabled =
                produto.Situacao;

            lblModo.Text =
                $"Modo: edição do produto #{produto.IdProduto}";
        }

        // =====================================
        // SALVAR CADASTRO OU ALTERAÇÃO
        // =====================================

        private void BtnSalvar_Click(
            object sender,
            RoutedEventArgs e)
        {
            string nome = txtNome.Text.Trim();

            bool precoValido = double.TryParse(
                txtPreco.Text,
                NumberStyles.Number,
                CultureInfo.GetCultureInfo("pt-BR"),
                out double preco);

            if (string.IsNullOrWhiteSpace(nome) ||
                !precoValido ||
                double.IsNaN(preco) ||
                double.IsInfinity(preco) ||
                preco < 0)
            {
                MessageBox.Show(
                    "Informe nome e preço válidos.",
                    "Validação",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (cmbCategoria.SelectedValue == null)
            {
                MessageBox.Show(
                    "Selecione uma categoria.",
                    "Validação",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string unidade =
                (cmbUnidade.SelectedItem as ComboBoxItem)
                ?.Content?.ToString() ?? "UN";

            Produto produto = new Produto
            {
                IdProduto = idProdutoEdicao ?? 0,
                Nome = nome,
                Preco = preco,
                Quantidade = 0,
                IdCategoria = Convert.ToInt32(
                    cmbCategoria.SelectedValue),
                UnidadeMedida = unidade,
                Situacao = chkAtivo.IsChecked == true
            };

            try
            {
                if (idProdutoEdicao.HasValue)
                {
                    bool atualizado =
                        produtoDAO.AtualizarProduto(produto);

                    if (!atualizado)
                    {
                        MessageBox.Show(
                            "O produto não foi encontrado.",
                            "Atualização");
                        return;
                    }

                    MessageBox.Show(
                        "Produto atualizado com sucesso!",
                        "Produtos");
                }
                else
                {
                    produtoDAO.CadastrarProduto(produto);

                    MessageBox.Show(
                        "Produto cadastrado com sucesso!",
                        "Produtos");
                }

                LimparCampos();
                CarregarProdutos();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Não foi possível salvar o produto:\n" +
                    ex.Message,
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =====================================
        // INATIVAR PRODUTO
        // =====================================

        private void BtnInativar_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!idProdutoEdicao.HasValue)
            {
                MessageBox.Show(
                    "Selecione um produto na tabela.");
                return;
            }

            var confirmacao = MessageBox.Show(
                "Deseja realmente inativar este produto?",
                "Confirmar inativação",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmacao != MessageBoxResult.Yes)
                return;

            try
            {
                bool inativado =
                    produtoDAO.InativarProduto(
                        idProdutoEdicao.Value);

                if (!inativado)
                {
                    MessageBox.Show(
                        "Produto não encontrado ou já inativo.");
                    return;
                }

                MessageBox.Show(
                    "Produto inativado com sucesso!");

                LimparCampos();
                CarregarProdutos();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao inativar produto:\n" +
                    ex.Message);
            }
        }

        // =====================================
        // LIMPAR FORMULÁRIO
        // =====================================

        private void BtnLimpar_Click(
            object sender,
            RoutedEventArgs e)
        {
            LimparCampos();
        }

        private void LimparCampos()
        {
            idProdutoEdicao = null;

            gridProdutos.SelectedItem = null;

            txtNome.Clear();
            txtPreco.Clear();

            cmbCategoria.SelectedIndex = -1;
            cmbUnidade.SelectedIndex = 0;

            chkAtivo.IsChecked = true;

            btnSalvar.Content = "Salvar produto";
            btnInativar.IsEnabled = false;

            lblModo.Text = "Modo: novo cadastro";
        }

        // =====================================
        // ATUALIZAR LISTAGEM
        // =====================================


        private void BtnAtualizarLista_Click(
            object sender,
            RoutedEventArgs e)
        {
            txtPesquisa.Clear();

            CarregarCategorias();
            CarregarProdutos();

            LimparCampos();
        }


        // =====================================
        // VOLTAR AO PAINEL
        // =====================================

        private void BtnVoltar_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is ModulosWindow painel)
                painel.VoltarAoPainel();
            else
                Close();
        }
    }
}
